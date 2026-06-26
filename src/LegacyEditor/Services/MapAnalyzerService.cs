using System.Collections.Generic;
using System.Linq;
using LegacyEditor.Models;

namespace LegacyEditor.Services;

public class MapAnalysis
{
    public int TotalMapFiles { get; set; }
    public int InPlayerInventories { get; set; }
    public int InPlayerEnderChest { get; set; }
    public int InWorldContainers { get; set; }
    public int InItemFrames { get; set; }
    public int PlacedTotal => InPlayerInventories + InPlayerEnderChest + InWorldContainers + InItemFrames;
    public int UnusedMaps { get; set; }
    public int InaccessibleChunks { get; set; }
    public int UnlinkedMaps { get; set; }
    public int StaleMappingEntries { get; set; }
    public int MappingEntries { get; set; }
    public int KnownPlayerMaps { get; set; }
}

public static class MapAnalyzerService
{
    static readonly HashSet<string> ContainerIds =
    [
        "Chest", "TrappedChest", "Hopper", "Dropper", "Dispenser",
        "Furnace", "BrewingStand", "Beacon", "Cauldron"
    ];

    public static MapAnalysis Analyze(byte[] archiveData, List<PlayerData> players,
        IProgress<(int current, int total, string status)>? progress = null)
    {
        var archive = MsArchive.Parse(archiveData);
        var result = new MapAnalysis();
        var usedMapIds = new HashSet<int>();

        // 1. Count map_*.dat files
        var mapFileIds = new HashSet<int>();
        foreach (var entry in archive.Entries)
        {
            var fn = entry.Filename;
            if (fn.Contains("map_", System.StringComparison.OrdinalIgnoreCase) &&
                fn.EndsWith(".dat", System.StringComparison.OrdinalIgnoreCase))
            {
                var idStr = fn.Replace("map_", "").Replace(".dat", "");
                var slashIdx = idStr.LastIndexOfAny(['/', '\\']);
                if (slashIdx >= 0) idStr = idStr[(slashIdx + 1)..];
                if (int.TryParse(idStr, out var id))
                    mapFileIds.Add(id);
            }
        }
        result.TotalMapFiles = mapFileIds.Count;

        // 2. Scan players for maps
        foreach (var p in players)
        {
            foreach (var item in p.Inventory)
                if (item.Id == 358) { result.InPlayerInventories++; usedMapIds.Add(item.Damage); }
            foreach (var item in p.Armor)
                if (item.Id == 358) { result.InPlayerInventories++; usedMapIds.Add(item.Damage); }
            foreach (var item in p.EnderChest)
                if (item.Id == 358) { result.InPlayerEnderChest++; usedMapIds.Add(item.Damage); }
        }

        // 3. Scan chunks for maps in containers and item frames
        var regionEntries = archive.Entries
            .Where(e => e.Filename.EndsWith(".mcr", System.StringComparison.OrdinalIgnoreCase) ||
                        e.Filename.EndsWith(".mca", System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        int totalRegions = regionEntries.Count;
        int processed = 0;

        foreach (var entry in regionEntries)
        {
            processed++;
            progress?.Report((processed, totalRegions, $"Scanning {entry.Filename}..."));

            var regionData = MsArchive.ExtractFile(archiveData, entry);
            if (regionData == null || regionData.Length < 8192) continue;

            for (int cz = 0; cz < 32; cz++)
            {
                for (int cx = 0; cx < 32; cx++)
                {
                    var rawChunk = RegionFile.ReadChunk(regionData, cx, cz);
                    if (rawChunk == null) continue;

                    if (rawChunk[0] != NbtParser.TagCompound)
                    {
                        result.InaccessibleChunks++;
                        continue;
                    }

                    var tag = NbtParser.Parse(rawChunk);
                    if (tag?.Value is not System.Collections.Generic.Dictionary<string, NbtParser.NbtTag>) continue;

                    var level = GetCompoundValue(tag, "Level") ?? tag;
                    if (level.Value is not System.Collections.Generic.Dictionary<string, NbtParser.NbtTag> levelData) continue;

                    // Scan TileEntities for containers with maps
                    if (levelData.TryGetValue("TileEntities", out var teList) &&
                        teList.Type == NbtParser.TagList &&
                        teList.Value is ListTag teTagList)
                    {
                        foreach (var teObj in teTagList.Items)
                        {
                            if (teObj is not System.Collections.Generic.Dictionary<string, NbtParser.NbtTag> te) continue;
                            if (!te.TryGetValue("id", out var teId) || teId.Type != NbtParser.TagString) continue;
                            if (!ContainerIds.Contains((string)teId.Value!)) continue;
                            if (!te.TryGetValue("Items", out var itemsTag) ||
                                itemsTag.Type != NbtParser.TagList ||
                                itemsTag.Value is not ListTag itemsList) continue;

                            foreach (var itemObj in itemsList.Items)
                            {
                                if (itemObj is not System.Collections.Generic.Dictionary<string, NbtParser.NbtTag> item) continue;
                                if (!item.TryGetValue("id", out var idTag) || idTag.Type != NbtParser.TagShort) continue;
                                if ((short)idTag.Value! != 358) continue;
                                result.InWorldContainers++;
                                if (item.TryGetValue("Damage", out var dmg) && dmg.Type == NbtParser.TagShort)
                                    usedMapIds.Add((short)dmg.Value!);
                            }
                        }
                    }

                    // Scan Entities for ItemFrame with maps
                    if (levelData.TryGetValue("Entities", out var entList) &&
                        entList.Type == NbtParser.TagList &&
                        entList.Value is ListTag entTagList)
                    {
                        foreach (var entObj in entTagList.Items)
                        {
                            if (entObj is not System.Collections.Generic.Dictionary<string, NbtParser.NbtTag> ent) continue;
                            if (!ent.TryGetValue("id", out var entId) || entId.Type != NbtParser.TagString) continue;
                            if ((string)entId.Value! != "ItemFrame") continue;
                            if (!ent.TryGetValue("Item", out var itemTag) ||
                                itemTag.Type != NbtParser.TagCompound ||
                                itemTag.Value is not System.Collections.Generic.Dictionary<string, NbtParser.NbtTag> frameItem) continue;

                            if (!frameItem.TryGetValue("id", out var fid) || fid.Type != NbtParser.TagShort) continue;
                            if ((short)fid.Value! != 358) continue;
                            result.InItemFrames++;
                            if (frameItem.TryGetValue("Damage", out var dmg) && dmg.Type == NbtParser.TagShort)
                                usedMapIds.Add((short)dmg.Value!);
                        }
                    }
                }
            }
        }

        // 4. Check largeMapDataMappings.dat — maps owned by current players are "used"
        var mappings = MapWipeService.ParseLargeMapMappings(archiveData);
        var activeXuids = players.Select(p => p.XUID).ToHashSet();
        foreach (var kv in mappings)
            if (activeXuids.Contains(kv.Value))
                usedMapIds.Add(kv.Key);

        // 5. Calculate unused maps
        result.UnusedMaps = mapFileIds.Count(id => !usedMapIds.Contains(id));

        // 6. Unlinked map analysis
        result.MappingEntries = mappings.Count;
        result.StaleMappingEntries = 0;
        foreach (var kv in mappings)
        {
            if (!activeXuids.Contains(kv.Value))
            {
                result.StaleMappingEntries++;
                if (mapFileIds.Contains(kv.Key))
                    result.UnlinkedMaps++;
            }
        }
        result.KnownPlayerMaps = mappings.Count(m => activeXuids.Contains(m.Value));

        return result;
    }

    static NbtParser.NbtTag? GetCompoundValue(NbtParser.NbtTag tag, string name)
    {
        if (tag.Value is System.Collections.Generic.Dictionary<string, NbtParser.NbtTag> dict &&
            dict.TryGetValue(name, out var found))
            return found;
        return null;
    }
}
