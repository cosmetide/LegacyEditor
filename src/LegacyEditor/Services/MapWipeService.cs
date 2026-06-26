using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LegacyEditor.Models;

namespace LegacyEditor.Services;

public static class MapWipeService
{
    static readonly HashSet<string> ContainerIds =
    [
        "Chest", "TrappedChest", "Hopper", "Dropper", "Dispenser",
        "Furnace", "BrewingStand", "Beacon", "Cauldron"
    ];

    public static byte[] WipePlacedMaps(byte[] archiveData, List<PlayerData> players,
        IProgress<(int current, int total, string status)>? progress = null)
        => WipePlacedMapsFiltered(archiveData, players, true, true, true, null, progress);

    public static byte[] WipeAllMaps(byte[] archiveData, List<PlayerData> players,
        IProgress<(int current, int total, string status)>? progress = null)
    {
        var result = WipePlacedMapsFiltered(archiveData, players, true, false, false, null, progress);
        return RemoveAllMapFiles(result);
    }

    public static byte[] WipePlacedMapsFiltered(byte[] archiveData, List<PlayerData> players,
        bool wipeInventory, bool wipeContainers, bool wipeFrames,
        HashSet<int>? onlyMapIds = null,
        IProgress<(int current, int total, string status)>? progress = null)
    {
        var archive = MsArchive.Parse(archiveData);
        var updatedFiles = new Dictionary<string, byte[]>();

        // 1. Remove map items from player data
        if (wipeInventory)
        {
            int playerTotal = players.Count;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                progress?.Report((i + 1, playerTotal + 100, $"Cleaning player {p.XUID}..."));

                var playerEntry = archive.Entries.FirstOrDefault(e =>
                    e.Filename.Equals($"players\\{p.XUID}.dat", StringComparison.OrdinalIgnoreCase));
                if (playerEntry == null) continue;

                var raw = MsArchive.ExtractFile(archiveData, playerEntry);
                if (raw == null || raw.Length == 0) continue;

                var decompressed = TryDecompress(raw);
                if (decompressed == null) continue;

                var tag = NbtParser.Parse(decompressed);
                if (tag?.Value is not Dictionary<string, NbtParser.NbtTag> root) continue;

                var modified = RemovePlayerMapItems(root, onlyMapIds);
                if (modified)
                {
                    var newData = WriteNbtSafe(tag, $"{playerEntry.Filename} (player inventory)");
                    if (newData != null)
                        updatedFiles[playerEntry.Filename] = newData;
                }
            }
        }

        // 2. Remove map items from chunks
        var regionEntries = archive.Entries
            .Where(e => e.Filename.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase) ||
                        e.Filename.EndsWith(".mca", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int totalRegions = regionEntries.Count;
        for (int i = 0; i < totalRegions; i++)
        {
            var entry = regionEntries[i];
            progress?.Report((players.Count + i + 1, players.Count + totalRegions, $"Cleaning {entry.Filename}..."));

            var regionData = MsArchive.ExtractFile(archiveData, entry);
            if (regionData == null || regionData.Length < 8192) continue;

            bool regionChanged = false;
            for (int cz = 0; cz < 32; cz++)
            {
                for (int cx = 0; cx < 32; cx++)
                {
                    var rawChunk = RegionFile.ReadChunk(regionData, cx, cz);
                    if (rawChunk == null) continue;
                    if (rawChunk[0] != NbtParser.TagCompound) continue;

                    var tag = NbtParser.Parse(rawChunk);
                    if (tag?.Value is not Dictionary<string, NbtParser.NbtTag>) continue;

                    var level = GetCompoundValue(tag, "Level") ?? tag;
                    if (level.Value is not Dictionary<string, NbtParser.NbtTag> levelData) continue;

                    bool chunkChanged = false;

                    // Remove maps from container TileEntities
                    if (wipeContainers &&
                        levelData.TryGetValue("TileEntities", out var teList) &&
                        teList.Type == NbtParser.TagList &&
                        teList.Value is ListTag teTagList)
                    {
                        foreach (var teObj in teTagList.Items)
                        {
                            if (teObj is not Dictionary<string, NbtParser.NbtTag> te) continue;
                            if (!te.TryGetValue("id", out var teId) || teId.Type != NbtParser.TagString) continue;
                            if (!ContainerIds.Contains((string)teId.Value!)) continue;
                            if (!te.TryGetValue("Items", out var itemsTag) ||
                                itemsTag.Type != NbtParser.TagList ||
                                itemsTag.Value is not ListTag itemsList) continue;

                            var newItems = new List<object?>();
                            foreach (var itemObj in itemsList.Items)
                            {
                                if (itemObj is Dictionary<string, NbtParser.NbtTag> item &&
                                    item.TryGetValue("id", out var idTag) &&
                                    idTag.Type == NbtParser.TagShort &&
                                    (short)idTag.Value! == 358)
                                {
                                    if (onlyMapIds != null)
                                    {
                                        if (item.TryGetValue("Damage", out var dmg) && dmg.Type == NbtParser.TagShort)
                                        {
                                            if (onlyMapIds.Contains((short)dmg.Value!))
                                                continue; // skip this map item (orphaned)
                                            else { newItems.Add(itemObj); continue; }
                                        }
                                        // If no Damage tag, keep it (can't determine map ID)
                                        newItems.Add(itemObj);
                                        continue;
                                    }
                                    continue; // skip all map items
                                }
                                newItems.Add(itemObj);
                            }

                            if (newItems.Count < itemsList.Items.Count)
                            {
                                itemsList.Items = newItems;
                                chunkChanged = true;
                            }
                        }
                    }

                    // Remove maps from ItemFrame entities
                    if (wipeFrames &&
                        levelData.TryGetValue("Entities", out var entList) &&
                        entList.Type == NbtParser.TagList &&
                        entList.Value is ListTag entTagList)
                    {
                        foreach (var entObj in entTagList.Items)
                        {
                            if (entObj is not Dictionary<string, NbtParser.NbtTag> ent) continue;
                            if (!ent.TryGetValue("id", out var entId) || entId.Type != NbtParser.TagString) continue;
                            if ((string)entId.Value! != "ItemFrame") continue;
                            if (!ent.TryGetValue("Item", out var itemTag) ||
                                itemTag.Type != NbtParser.TagCompound ||
                                itemTag.Value is not Dictionary<string, NbtParser.NbtTag> frameItem) continue;

                            if (frameItem.TryGetValue("id", out var fid) && fid.Type == NbtParser.TagShort &&
                                (short)fid.Value! == 358)
                            {
                                bool shouldRemove = true;
                                if (onlyMapIds != null)
                                {
                                    if (frameItem.TryGetValue("Damage", out var dmg) && dmg.Type == NbtParser.TagShort)
                                        shouldRemove = onlyMapIds.Contains((short)dmg.Value!);
                                    else
                                        shouldRemove = false;
                                }

                                if (shouldRemove)
                                {
                                    frameItem["id"] = new NbtParser.NbtTag
                                    {
                                        Type = NbtParser.TagShort,
                                        Name = "id",
                                        Value = (short)0
                                    };
                                    chunkChanged = true;
                                }
                            }
                        }
                    }

                    if (chunkChanged)
                    {
                        var newRaw = WriteNbtSafe(tag, $"{entry.Filename} chunk ({cx},{cz})");
                        if (newRaw != null)
                        {
                            regionData = RegionFile.WriteChunk(regionData, cx, cz, newRaw);
                            regionChanged = true;
                        }
                    }
                }
            }

            if (regionChanged)
                updatedFiles[entry.Filename] = regionData;
        }

        if (updatedFiles.Count == 0) return archiveData;
        return archive.Rebuild(archiveData, updatedFiles);
    }

    public static byte[] WipeUnusedMaps(byte[] archiveData, List<PlayerData>? players = null)
    {
        var archive = MsArchive.Parse(archiveData);

        var allMapFiles = archive.Entries
            .Where(e => e.Filename.Contains("map_", StringComparison.OrdinalIgnoreCase) &&
                        e.Filename.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (allMapFiles.Count == 0) return archiveData;

        var usedMapIds = players != null
            ? FindUsedMapIds(archiveData, players)
            : new HashSet<int>();

        var toRemove = new HashSet<string>();
        foreach (var entry in allMapFiles)
        {
            var idStr = Path.GetFileNameWithoutExtension(entry.Filename).Replace("map_", "");
            if (int.TryParse(idStr, out var id) && !usedMapIds.Contains(id))
                toRemove.Add(entry.Filename);
        }

        if (toRemove.Count == 0) return archiveData;
        return archive.Rebuild(archiveData, toRemove);
    }

    public static Dictionary<int, ulong> ParseLargeMapMappings(byte[] archiveData)
    {
        var archive = MsArchive.Parse(archiveData);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.Filename.Contains("largeMapDataMappings", StringComparison.OrdinalIgnoreCase));
        if (entry == null) return [];

        var raw = MsArchive.ExtractFile(archiveData, entry);
        if (raw == null || raw.Length < 16) return [];

        int totalRecords = (raw.Length - 16) / 24;
        if ((raw.Length - 16) % 24 != 0) return [];

        var result = new Dictionary<int, ulong>(totalRecords);
        for (int i = 0; i < totalRecords; i++)
        {
            int pos = 16 + i * 24;
            int mapId = (raw[pos + 8] << 24) | (raw[pos + 9] << 16) | (raw[pos + 10] << 8) | raw[pos + 11];
            ulong xuid = ((ulong)raw[pos + 12] << 56) | ((ulong)raw[pos + 13] << 48) |
                         ((ulong)raw[pos + 14] << 40) | ((ulong)raw[pos + 15] << 32) |
                         ((ulong)raw[pos + 16] << 24) | ((ulong)raw[pos + 17] << 16) |
                         ((ulong)raw[pos + 18] << 8) | raw[pos + 19];
            // Keep first occurrence if duplicate map IDs exist
            if (!result.ContainsKey(mapId))
                result[mapId] = xuid;
        }
        return result;
    }

    public static byte[] CleanLargeMapMappings(byte[] archiveData, HashSet<int> mapIdsToRemove)
    {
        if (mapIdsToRemove == null || mapIdsToRemove.Count == 0) return archiveData;

        var archive = MsArchive.Parse(archiveData);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.Filename.Contains("largeMapDataMappings", StringComparison.OrdinalIgnoreCase));
        if (entry == null) return archiveData;

        var raw = MsArchive.ExtractFile(archiveData, entry);
        if (raw == null || raw.Length < 16) return archiveData;

        int totalRecords = (raw.Length - 16) / 24;
        if ((raw.Length - 16) % 24 != 0) return archiveData;

        var keptRecords = new List<byte>();
        keptRecords.AddRange(raw.Take(16)); // header

        for (int i = 0; i < totalRecords; i++)
        {
            int pos = 16 + i * 24;
            int mapId = (raw[pos + 8] << 24) | (raw[pos + 9] << 16) | (raw[pos + 10] << 8) | raw[pos + 11];
            if (!mapIdsToRemove.Contains(mapId))
            {
                keptRecords.AddRange(raw.Skip(pos).Take(24));
            }
        }

        if (keptRecords.Count == 16) // only header left
        {
            // Remove the file entirely
            return archive.Rebuild(archiveData, new HashSet<string> { entry.Filename });
        }

        return archive.Rebuild(archiveData, new Dictionary<string, byte[]> { { entry.Filename, keptRecords.ToArray() } });
    }

    public static HashSet<int> FindUnlinkedMapIds(byte[] archiveData, List<PlayerData> players)
    {
        var mappings = ParseLargeMapMappings(archiveData);
        if (mappings.Count == 0) return [];

        var activeXuids = players.Select(p => p.XUID).ToHashSet();
        var unlinked = new HashSet<int>();
        foreach (var kv in mappings)
        {
            if (!activeXuids.Contains(kv.Value))
                unlinked.Add(kv.Key);
        }
        return unlinked;
    }

    static HashSet<int> FindUsedMapIds(byte[] archiveData, List<PlayerData> players)
    {
        var used = new HashSet<int>();
        var archive = MsArchive.Parse(archiveData);
        var activeXuids = players.Select(p => p.XUID).ToHashSet();

        // Check largeMapDataMappings.dat — maps owned by current players are "used"
        var mappings = ParseLargeMapMappings(archiveData);
        foreach (var kv in mappings)
            if (activeXuids.Contains(kv.Value))
                used.Add(kv.Key);

        // Check player inventories
        foreach (var p in players)
        {
            foreach (var item in p.Inventory)
                if (item.Id == 358) used.Add(item.Damage);
            foreach (var item in p.Armor)
                if (item.Id == 358) used.Add(item.Damage);
            foreach (var item in p.EnderChest)
                if (item.Id == 358) used.Add(item.Damage);
        }

        return used;
    }

    static byte[] RemoveAllMapFiles(byte[] archiveData)
    {
        var archive = MsArchive.Parse(archiveData);
        var toRemove = new HashSet<string>();
        foreach (var entry in archive.Entries)
        {
            if (!entry.Filename.Contains("map_", StringComparison.OrdinalIgnoreCase) ||
                !entry.Filename.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                continue;

            toRemove.Add(entry.Filename);
        }
        if (toRemove.Count == 0) return archiveData;
        return archive.Rebuild(archiveData, toRemove);
    }

    public static bool RemovePlayerMapItems(Dictionary<string, NbtParser.NbtTag> root, HashSet<int>? onlyMapIds = null)
    {
        bool modified = false;

        foreach (var listKey in new[] { "Inventory", "EnderItems", "Armor" })
        {
            if (!root.TryGetValue(listKey, out var listTag) ||
                listTag.Type != NbtParser.TagList ||
                listTag.Value is not ListTag items) continue;

            var newItems = new List<object?>();
            foreach (var itemObj in items.Items)
            {
                if (itemObj is Dictionary<string, NbtParser.NbtTag> item &&
                    item.TryGetValue("id", out var idTag) &&
                    idTag.Type == NbtParser.TagShort &&
                    (short)idTag.Value! == 358)
                {
                    if (onlyMapIds != null)
                    {
                        if (item.TryGetValue("Damage", out var dmg) && dmg.Type == NbtParser.TagShort)
                        {
                            if (onlyMapIds.Contains((short)dmg.Value!))
                                continue; // removed
                        }
                        // If no Damage, keep it (can't verify)
                        else { newItems.Add(itemObj); continue; }
                    }
                    else
                    {
                        continue; // remove all map items
                    }
                }
                newItems.Add(itemObj);
            }

            if (newItems.Count < items.Items.Count)
            {
                items.Items = newItems;
                modified = true;
            }
        }

        return modified;
    }

    static NbtParser.NbtTag? GetCompoundValue(NbtParser.NbtTag tag, string name)
    {
        if (tag.Value is Dictionary<string, NbtParser.NbtTag> dict &&
            dict.TryGetValue(name, out var found))
            return found;
        return null;
    }

    static byte[] ZLibCompress(byte[] data)
    {
        using var compStream = new MemoryStream();
        using (var zlib = new ZLibStream(compStream, CompressionLevel.Optimal))
            zlib.Write(data);
        return compStream.ToArray();
    }

    static byte[]? TryDecompress(byte[] data)
    {
        try { return ZLibDecompress(data); } catch { }
        try { return GZipDecompress(data); } catch { }
        try { return DeflateDecompress(data); } catch { }
        return data;
    }

    static byte[] ZLibDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var zlib = new System.IO.Compression.ZLibStream(compStream, System.IO.Compression.CompressionMode.Decompress);
        using var result = new MemoryStream();
        zlib.CopyTo(result);
        return result.ToArray();
    }

    static byte[] GZipDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var gzip = new System.IO.Compression.GZipStream(compStream, System.IO.Compression.CompressionMode.Decompress);
        using var result = new MemoryStream();
        gzip.CopyTo(result);
        return result.ToArray();
    }

    static byte[] DeflateDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var deflate = new System.IO.Compression.DeflateStream(compStream, System.IO.Compression.CompressionMode.Decompress);
        using var result = new MemoryStream();
        deflate.CopyTo(result);
        return result.ToArray();
    }

    public static byte[] WriteNbtSafe(NbtParser.NbtTag tag, string context)
    {
        var data = NbtParser.Write(tag);
        var reParsed = NbtParser.Parse(data);
        if (reParsed == null)
        {
            System.Diagnostics.Debug.WriteLine($"NBT write produced unparseable data for {context}");
            return null!;
        }
        return data;
    }
}
