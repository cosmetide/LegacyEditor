using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using LegacyEditor.Services;

namespace LegacyEditor.Services;

public class WorldInfo
{
    public string LevelName { get; set; } = "Unknown";
    public long RandomSeed { get; set; }
    public int XZSize { get; set; }
    public int HellScale { get; set; }
    public int OverworldRegions { get; set; }
    public int NetherRegions { get; set; }
    public int EndRegions { get; set; }
}

public static class WorldInfoService
{
    public static readonly (string Name, int Chunks, int HellScale, int RegionRadius)[] WorldSizes =
    [
        ("Classic",  54, 3, 1),
        ("Small",    64, 3, 1),
        ("Medium",  192, 6, 3),
        ("Large",   320, 8, 5)
    ];

    public static WorldInfo? GetWorldInfo(byte[] archiveData)
    {
        var archive = MsArchive.Parse(archiveData);
        var levelEntry = archive.Entries.FirstOrDefault(e =>
            e.Filename.Equals("level.dat", StringComparison.OrdinalIgnoreCase));

        if (levelEntry == null) return null;

        var raw = MsArchive.ExtractFile(archiveData, levelEntry);
        if (raw == null || raw.Length == 0) return null;

        var decompressed = TryDecompressLevelDat(raw);
        if (decompressed == null || decompressed.Length == 0) return null;

        // Try parsing NBT at different offsets (some formats have header bytes)
        NbtParser.NbtTag? tag = null;
        Dictionary<string, NbtParser.NbtTag>? root = null;

        for (int skip = 0; skip <= 8; skip++)
        {
            if (skip >= decompressed.Length) break;
            tag = NbtParser.Parse(decompressed[skip..]);
            if (tag?.Value is Dictionary<string, NbtParser.NbtTag> r)
            {
                root = r;
                break;
            }
        }

        if (root == null) return null;

        // Try multiple strategies to find the data compound
        Dictionary<string, NbtParser.NbtTag>? data = null;

        // Strategy 1: "Data" wrapper (standard Minecraft)
        if (root.TryGetValue("Data", out var dt) &&
            dt.Value is Dictionary<string, NbtParser.NbtTag> dataDict1)
            data = dataDict1;

        // Strategy 2: root level (some platforms store data directly)
        if (data == null && (root.ContainsKey("LevelName") || root.ContainsKey("RandomSeed")))
            data = root;

        // Strategy 3: look for any compound child that contains known keys
        if (data == null)
        {
            foreach (var kvp in root)
            {
                if (kvp.Value.Type == NbtParser.TagCompound &&
                    kvp.Value.Value is Dictionary<string, NbtParser.NbtTag> child &&
                    (child.ContainsKey("LevelName") || child.ContainsKey("RandomSeed") ||
                     child.ContainsKey("XZSize") || child.ContainsKey("gameType") ||
                     child.ContainsKey("GameType")))
                {
                    data = child;
                    break;
                }
            }
        }

        if (data == null) return null;

        var info = new WorldInfo();

        if (data.TryGetValue("LevelName", out var nameTag) && nameTag.Type == NbtParser.TagString)
            info.LevelName = (string)nameTag.Value!;

        // Try both "RandomSeed" and "random_seed" key variants
        if (data.TryGetValue("RandomSeed", out var seedTag) && seedTag.Type == NbtParser.TagLong)
            info.RandomSeed = (long)seedTag.Value!;
        else if (data.TryGetValue("random_seed", out var seedTag2) && seedTag2.Type == NbtParser.TagLong)
            info.RandomSeed = (long)seedTag2.Value!;

        if (data.TryGetValue("XZSize", out var sizeTag) && sizeTag.Type == NbtParser.TagInt)
            info.XZSize = (int)sizeTag.Value!;
        else
            info.XZSize = 64;

        if (data.TryGetValue("HellScale", out var hellTag) && hellTag.Type == NbtParser.TagInt)
            info.HellScale = (int)hellTag.Value!;
        else
            info.HellScale = 3;

        // Count region files per dimension
        foreach (var entry in archive.Entries)
        {
            var fn = entry.Filename;
            if (!fn.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase) &&
                !fn.EndsWith(".mca", StringComparison.OrdinalIgnoreCase))
                continue;

            if (fn.StartsWith("DIM-1", StringComparison.OrdinalIgnoreCase) ||
                fn.StartsWith("DIM-1\\", StringComparison.OrdinalIgnoreCase))
                info.NetherRegions++;
            else if (fn.StartsWith("DIM1", StringComparison.OrdinalIgnoreCase) ||
                     fn.StartsWith("DIM1\\", StringComparison.OrdinalIgnoreCase))
                info.EndRegions++;
            else
                info.OverworldRegions++;
        }

        return info;
    }

    public static (byte[] newArchive, WorldInfo newInfo) TrimWorld(
        byte[] archiveData, int newXZSize, int newHellScale)
    {
        var archive = MsArchive.Parse(archiveData);
        var levelEntry = archive.Entries.FirstOrDefault(e =>
            e.Filename.Equals("level.dat", StringComparison.OrdinalIgnoreCase));

        // Build exclusion set for region files outside new boundary
        var toRemove = new HashSet<string>();

        // Calculate valid region ranges (32-chunk regions, centered on origin)
        int halfChunks = newXZSize / 2;
        int minChunk = -halfChunks;
        int maxChunk = halfChunks - 1;

        int minRegion = minChunk >> 5;
        int maxRegion = maxChunk >> 5;

        // Nether size = XZSize / 8 * HellScale
        int netherChunks = newXZSize * newHellScale / 8;
        int netherHalf = netherChunks / 2;
        int netherMinRegion = (-netherHalf) >> 5;
        int netherMaxRegion = (netherHalf - 1) >> 5;

        foreach (var entry in archive.Entries)
        {
            var fn = entry.Filename;
            if (!fn.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase) &&
                !fn.EndsWith(".mca", StringComparison.OrdinalIgnoreCase))
                continue;

            // Parse region coords from filename: r.X.Z.mcr
            var parts = System.IO.Path.GetFileNameWithoutExtension(fn).Split('.');
            if (parts.Length < 3 || !int.TryParse(parts[1], out var rx) || !int.TryParse(parts[2], out var rz))
                continue;

            bool isNether = fn.StartsWith("DIM-1", StringComparison.OrdinalIgnoreCase);
            bool isEnd = fn.StartsWith("DIM1", StringComparison.OrdinalIgnoreCase) ||
                         fn.StartsWith("DIM1/", StringComparison.OrdinalIgnoreCase);

            if (isEnd)
            {
                // End is always 18 chunks
                int endHalf = 18 / 2;
                int endMinR = (-endHalf) >> 5;
                int endMaxR = (endHalf - 1) >> 5;
                if (rx < endMinR || rx > endMaxR || rz < endMinR || rz > endMaxR)
                    toRemove.Add(fn);
            }
            else if (isNether)
            {
                if (rx < netherMinRegion || rx > netherMaxRegion ||
                    rz < netherMinRegion || rz > netherMaxRegion)
                    toRemove.Add(fn);
            }
            else
            {
                if (rx < minRegion || rx > maxRegion ||
                    rz < minRegion || rz > maxRegion)
                    toRemove.Add(fn);
            }
        }

        // Update level.dat
        if (levelEntry != null)
        {
            var raw = MsArchive.ExtractFile(archiveData, levelEntry);
            if (raw != null && raw.Length > 0)
            {
                var decompressed = TryDecompressLevelDat(raw);
                if (decompressed != null)
                {
                    var tag = NbtParser.Parse(decompressed);
                    if (tag?.Value is Dictionary<string, NbtParser.NbtTag> root)
                    {
                        NbtParser.NbtTag? dataTag = null;
                        if (root.TryGetValue("Data", out var dt))
                            dataTag = dt;
                        else
                            dataTag = tag;

                        if (dataTag.Value is Dictionary<string, NbtParser.NbtTag> data)
                        {
                            data["XZSize"] = new NbtParser.NbtTag
                            {
                                Type = NbtParser.TagInt,
                                Name = "XZSize",
                                Value = newXZSize
                            };
                            data["HellScale"] = new NbtParser.NbtTag
                            {
                                Type = NbtParser.TagInt,
                                Name = "HellScale",
                                Value = newHellScale
                            };
                        }

                        var newLevelData = NbtParser.Write(tag);
                        var updatedFiles = new Dictionary<string, byte[]>
                        {
                            [levelEntry.Filename] = newLevelData
                        };

                        var newArchive = archive.Rebuild(archiveData, updatedFiles);
                        // Remove region files from rebuilt archive
                        if (toRemove.Count > 0)
                        {
                            var archive2 = MsArchive.Parse(newArchive);
                            newArchive = archive2.Rebuild(newArchive, toRemove);
                        }

                        var newInfo = new WorldInfo
                        {
                            LevelName = GetLevelName(tag),
                            RandomSeed = GetRandomSeed(tag),
                            XZSize = newXZSize,
                            HellScale = newHellScale
                        };

                        // Count remaining regions
                        var finalArchive = MsArchive.Parse(newArchive);
                        foreach (var e in finalArchive.Entries)
                        {
                            var fn = e.Filename;
                            if (!fn.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase) &&
                                !fn.EndsWith(".mca", StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (fn.StartsWith("DIM-1", StringComparison.OrdinalIgnoreCase))
                                newInfo.NetherRegions++;
                            else if (fn.StartsWith("DIM1", StringComparison.OrdinalIgnoreCase) ||
                                     fn.StartsWith("DIM1/", StringComparison.OrdinalIgnoreCase))
                                newInfo.EndRegions++;
                            else
                                newInfo.OverworldRegions++;
                        }

                        return (newArchive, newInfo);
                    }
                }
            }
        }

        // Fallback: just remove region files without updating level.dat
        var newData = archive.Rebuild(archiveData, toRemove);
        return (newData, new WorldInfo { XZSize = newXZSize, HellScale = newHellScale });
    }

    static string GetLevelName(NbtParser.NbtTag tag)
    {
        if (tag.Value is Dictionary<string, NbtParser.NbtTag> root)
        {
            if (root.TryGetValue("Data", out var dt) &&
                dt.Value is Dictionary<string, NbtParser.NbtTag> data &&
                data.TryGetValue("LevelName", out var name) &&
                name.Type == NbtParser.TagString)
                return (string)name.Value!;
        }
        return "Unknown";
    }

    static long GetRandomSeed(NbtParser.NbtTag tag)
    {
        if (tag.Value is Dictionary<string, NbtParser.NbtTag> root)
        {
            if (root.TryGetValue("Data", out var dt) &&
                dt.Value is Dictionary<string, NbtParser.NbtTag> data &&
                data.TryGetValue("RandomSeed", out var seed) &&
                seed.Type == NbtParser.TagLong)
                return (long)seed.Value!;
        }
        return 0;
    }

    static byte[]? TryDecompressLevelDat(byte[] data)
    {
        // Try ZLib (standard Minecraft Java)
        try { return ZLibDecompress(data); } catch { }
        // Try GZip (some Bedrock/console editions)
        try { return GZipDecompress(data); } catch { }
        // Try raw Deflate (some console editions like PS3/Wii U)
        try { return DeflateDecompress(data); } catch { }
        // Return raw as last resort
        return data;
    }

    static byte[] ZLibDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var zlib = new ZLibStream(compStream, CompressionMode.Decompress);
        using var result = new MemoryStream();
        zlib.CopyTo(result);
        return result.ToArray();
    }

    static byte[] GZipDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var gzip = new GZipStream(compStream, CompressionMode.Decompress);
        using var result = new MemoryStream();
        gzip.CopyTo(result);
        return result.ToArray();
    }

    static byte[] DeflateDecompress(byte[] compressed)
    {
        using var compStream = new MemoryStream(compressed);
        using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
        using var result = new MemoryStream();
        deflate.CopyTo(result);
        return result.ToArray();
    }
}
