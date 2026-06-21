using System.IO;
using System.IO.Compression;
using LegacyEditor.Models;

namespace LegacyEditor.Services;

public class WorldWiperService
{
    public async Task<WipeSummary> ProcessWorld(string inputPath, string outputPath,
        WipeConfig config, IProgress<string> progress, CancellationToken ct = default)
    {
        progress.Report($"Reading {inputPath}...");
        var rawData = await File.ReadAllBytesAsync(inputPath, ct);
        rawData = MaybeDecompressMsStatic(rawData, out var wasCompressed);
        return await ProcessWorldImpl(rawData, outputPath, config, progress, wasCompressed, inputPath, ct);
    }

    async Task<WipeSummary> ProcessWorldImpl(byte[] rawData, string outputPath,
        WipeConfig config, IProgress<string> progress, bool wasCompressed, string logInputPath,
        CancellationToken ct = default)
    {
        var summary = new WipeSummary();

        var archive = MsArchive.Parse(rawData);
        progress.Report($"Save version: {archive.SaveVer}, Files: {archive.Entries.Count}");

        var fileData = new Dictionary<string, byte[]>();
        foreach (var ent in archive.Entries)
        {
            if (ent.StartOffset + ent.Length <= rawData.Length)
            {
                var buf = new byte[ent.Length];
                Array.Copy(rawData, ent.StartOffset, buf, 0, ent.Length);
                fileData[ent.Filename] = buf;
            }
        }

        var regionJobs = new List<(string filename, byte[] data)>();

        foreach (var ent in archive.Entries)
        {
            var fn = ent.Filename;
            if (!fn.EndsWith(".mcr", StringComparison.OrdinalIgnoreCase) &&
                !fn.EndsWith(".mca", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!fileData.TryGetValue(fn, out var region) || region.Length < RegionFile.SectorBytes * 2)
                continue;

            bool include = false;
            if (fn.StartsWith("DIM-1", StringComparison.OrdinalIgnoreCase))
                include = config.WipeNether;
            else if (fn.StartsWith("DIM1/", StringComparison.OrdinalIgnoreCase) || fn.StartsWith("DIM1\\", StringComparison.OrdinalIgnoreCase))
                include = config.WipeEnd;
            else if (!fn.Contains("DIM", StringComparison.OrdinalIgnoreCase))
                include = config.WipeOverworld;

            if (include)
                regionJobs.Add((fn, region));
        }

        progress.Report($"Processing {regionJobs.Count} region file(s)...");

        var modifiedRegions = new Dictionary<string, byte[]>();
        var allRemoved = new List<WipeResult>();

        foreach (var (fn, region) in regionJobs)
        {
            ct.ThrowIfCancellationRequested();
            progress.Report($"  {fn}: {region.Length} bytes");

            var result = ProcessRegion(fn, region, config, progress);
            if (result.newRegion != null)
            {
                modifiedRegions[fn] = result.newRegion;
                allRemoved.AddRange(result.removed);
            }
            summary.TotalChunks += result.totalChunks;
            summary.ReadFailures += result.readFailures;
        }

        summary.TotalRemoved = allRemoved.Count;

        if (allRemoved.Count == 0)
        {
            progress.Report("No entities found to remove.");
        }
        else
        {
            progress.Report($"Total entities removed: {allRemoved.Count}");
            foreach (var group in allRemoved.GroupBy(r => r.EntityId).OrderByDescending(g => g.Count()))
            {
                var displayName = EntityRegistry.GetDisplayName(group.Key);
                progress.Report($"  {group.Key} ({displayName}): {group.Count()}");
                foreach (var r in group.Take(10))
                {
                    var pos = r.PosX != null ? $" @ {r.PosX:F1},{r.PosY:F1},{r.PosZ:F1}" : "";
                    progress.Report($"    {r.RegionFile} [{r.ChunkX},{r.ChunkZ}] ({r.ListKey}){pos}");
                }
                if (group.Count() > 10)
                    progress.Report($"    ... and {group.Count() - 10} more");
            }
        }

        progress.Report("Writing output file...");
        var resultData = archive.Rebuild(rawData, modifiedRegions);
        progress.Report($"  Rebuilt archive ({resultData.Length} bytes)");

        if (wasCompressed)
        {
            progress.Report("  Re-wrapping with compression...");
            using var compStream = new MemoryStream();
            using (var deflate = new ZLibStream(compStream, CompressionLevel.Optimal))
                deflate.Write(resultData);
            var comp = compStream.ToArray();
            var flagBytes = BitConverter.GetBytes(0);           // LE on x86
            var sizeBytes = BitConverter.GetBytes(resultData.Length); // LE on x86
            resultData = [.. flagBytes, .. sizeBytes, .. comp];
        }

        await File.WriteAllBytesAsync(outputPath, resultData, ct);
        progress.Report($"Done -> {outputPath}");

        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        var logName = "log-" + Path.GetFileNameWithoutExtension(outputPath) + ".log";
        var logPath = Path.Combine(logDir, logName);
        await WriteLog(logPath, logInputPath, outputPath, allRemoved);
        progress.Report($"Log written -> {logPath}");

        summary.Removed = allRemoved;
        return summary;
    }

    (byte[]? newRegion, List<WipeResult> removed, int totalChunks, int readFailures) ProcessRegion(
        string fn, byte[] region, WipeConfig config, IProgress<string> progress)
    {
        bool regionChanged = false;
        int chunksTouched = 0;
        int totalChunks = 0;
        int readFailures = 0;
        var allRemoved = new List<WipeResult>();

        for (int cz = 0; cz < 32; cz++)
        {
            for (int cx = 0; cx < 32; cx++)
            {
                int idx = cx + cz * 32;
                int rawOff = region[idx * 4] | (region[idx * 4 + 1] << 8) |
                             (region[idx * 4 + 2] << 16) | (region[idx * 4 + 3] << 24);
                if (rawOff == 0) continue;
                totalChunks++;

                var rawChunk = RegionFile.ReadChunk(region, cx, cz);
                if (rawChunk == null) { readFailures++; continue; }

                try
                {
                    var (newRaw, removed) = ChunkProcessor.ProcessChunk(rawChunk,
                        config.EntitiesToWipe, config.Mode);
                    if (newRaw != null)
                    {
                        region = RegionFile.WriteChunk(region, cx, cz, newRaw);
                        regionChanged = true;
                        chunksTouched++;
                        foreach (var r in removed)
                        {
                            r.RegionFile = fn;
                            r.ChunkX = cx;
                            r.ChunkZ = cz;
                            allRemoved.Add(r);
                        }
                    }
                }
                catch
                {
                    readFailures++;
                }
            }
        }

        progress.Report($"  {fn}: {totalChunks} chunks, {readFailures} failures, {chunksTouched} modified");
        return (regionChanged ? region : null, allRemoved, totalChunks, readFailures);
    }

    public static byte[] MaybeDecompressMsStatic(byte[] data, out bool wasCompressed)
    {
        wasCompressed = false;
        if (data.Length >= 8)
        {
            int flag = BitConverter.ToInt32(data, 0);
            if (flag == 0)
            {
                try
                {
                    using var compStream = new MemoryStream(data, 8, data.Length - 8);
                    using var deflate = new ZLibStream(compStream, CompressionMode.Decompress);
                    using var result = new MemoryStream();
                    deflate.CopyTo(result);
                    wasCompressed = true;
                    return result.ToArray();
                }
                catch { }
            }
        }
        return data;
    }

    static async Task WriteLog(string path, string inputPath, string outputPath, List<WipeResult> removed)
    {
        using var w = new StreamWriter(path, false);
        await w.WriteLineAsync($"Entities erased from: {inputPath}");
        await w.WriteLineAsync($"Output file: {outputPath}");
        await w.WriteLineAsync($"Total removed: {removed.Count}");
        await w.WriteLineAsync("\nBy type:");
        foreach (var group in removed.GroupBy(r => r.EntityId).OrderByDescending(g => g.Count()))
        {
            var display = EntityRegistry.GetDisplayName(group.Key);
            await w.WriteLineAsync($"  {group.Key,-24} {display,-24} {group.Count()}");
        }
        await w.WriteLineAsync();
    }
}

public class WipeSummary
{
    public int TotalRemoved { get; set; }
    public int TotalChunks { get; set; }
    public int ReadFailures { get; set; }
    public List<WipeResult> Removed { get; set; } = [];
}
