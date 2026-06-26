using System.IO;

namespace LegacyEditor.Services;

public static class RegionFile
{
    public const int SectorBytes = 4096;

    public static byte[]? ReadChunk(byte[] region, int cx, int cz, Action<string>? log = null)
    {
        int idx = cx + cz * 32;
        if (idx < 0 || idx >= 1024) return null;

        int rawOffset = ReadI32LE(region, idx * 4);
        if (rawOffset == 0) return null;

        int sector = rawOffset >> 8;
        int numSec = rawOffset & 0xFF;
        if (sector == 0) return null;

        int chunkStart = sector * SectorBytes;
        if (chunkStart + 8 > region.Length) return null;

        int compLen = ReadI32LE(region, chunkStart);
        int decompLen = ReadI32LE(region, chunkStart + 4);
        bool useRle = (compLen & 0x80000000) != 0;
        compLen &= 0x7FFFFFFF;

        if (compLen == 0 || compLen > numSec * SectorBytes - 8) return null;

        var compressed = new byte[compLen];
        Array.Copy(region, chunkStart + 8, compressed, 0, compLen);

        try
        {
            byte[] raw;
            if (useRle)
                raw = Compression.DecompressChunk(compressed, decompLen);
            else
                raw = Compression.ZLibDecompress(compressed);
            return raw[..decompLen];
        }
        catch
        {
            return null;
        }
    }

    public static byte[] WriteChunk(byte[] region, int cx, int cz, byte[] newRaw)
    {
        var compressed = Compression.CompressChunk(newRaw);
        int compLen = compressed.Length;
        int fullCompLen = compLen | int.MinValue; // 0x80000000 as signed int
        int decompLen = newRaw.Length;
        int sectorsNeeded = (compLen + 8 + SectorBytes - 1) / SectorBytes;

        var header = new byte[8];
        WriteI32LE(header, 0, fullCompLen);
        WriteI32LE(header, 4, decompLen);
        var chunkEntry = new byte[8 + compLen];
        Array.Copy(header, chunkEntry, 8);
        Array.Copy(compressed, 0, chunkEntry, 8, compLen);

        int paddedLen = sectorsNeeded * SectorBytes;
        var chunkPadded = new byte[paddedLen];
        Array.Copy(chunkEntry, chunkPadded, chunkEntry.Length);

        const int headerSize = 8192;
        int firstDataSector = headerSize / SectorBytes;
        var arr = new byte[Math.Max(region.Length, (sectorsNeeded + firstDataSector) * SectorBytes)];
        Array.Copy(region, arr, region.Length);

        int idx = cx + cz * 32;

        int oldRawOffset = ReadI32LE(region, idx * 4);
        if (oldRawOffset != 0)
        {
            int oldSector = oldRawOffset >> 8;
            int oldNum = oldRawOffset & 0xFF;
            for (int s = 0; s < oldNum; s++)
            {
                int sstart = (oldSector + s) * SectorBytes;
                if (sstart < arr.Length)
                    Array.Clear(arr, sstart, Math.Min(SectorBytes, arr.Length - sstart));
            }
        }

        int numSectors = (arr.Length + SectorBytes - 1) / SectorBytes;
        var occupancy = new int[Math.Max(numSectors, firstDataSector)];
        for (int k = 0; k < 1024; k++)
        {
            int v = ReadI32LE(arr, k * 4);
            if (v != 0)
            {
                int sn = v >> 8;
                int nn = v & 0xFF;
                for (int s = 0; s < nn; s++)
                    if (sn + s < occupancy.Length)
                        occupancy[sn + s]++;
            }
        }

        int sector = -1;
        for (int candidate = firstDataSector; candidate <= occupancy.Length - sectorsNeeded; candidate++)
        {
            bool free = true;
            for (int j = 0; j < sectorsNeeded && free; j++)
                if (occupancy[candidate + j] != 0) free = false;
            if (free) { sector = candidate; break; }
        }

        if (sector < 0)
            sector = numSectors;

        int newEnd = (sector + sectorsNeeded) * SectorBytes;
        if (newEnd > arr.Length)
            Array.Resize(ref arr, newEnd);

        int start = sector * SectorBytes;
        Array.Copy(chunkPadded, 0, arr, start, chunkPadded.Length);

        int val = (sector << 8) | sectorsNeeded;
        WriteI32LE(arr, idx * 4, val);

        return arr;
    }

    static int ReadI32LE(byte[] d, int o) =>
        d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

    static void WriteI32LE(byte[] d, int o, int v)
    {
        d[o] = (byte)v;
        d[o + 1] = (byte)(v >> 8);
        d[o + 2] = (byte)(v >> 16);
        d[o + 3] = (byte)(v >> 24);
    }

    public static bool TryValidate(byte[] region, out string? error)
    {
        if (region == null || region.Length < 8192)
        {
            error = "Region data too short (less than 8192 bytes)";
            return false;
        }

        int totalSectors = (region.Length + SectorBytes - 1) / SectorBytes;

        // Build occupancy map from sector table (first 1024 entries = 8192 bytes)
        int[] occupancy = new int[totalSectors];
        for (int i = 0; i < 1024; i++)
        {
            int v = ReadI32LE(region, i * 4);
            if (v == 0) continue;

            int sector = v >> 8;
            int count = v & 0xFF;
            if (sector < 2)
            {
                error = $"Entry {i}: sector {sector} overlaps with header region";
                return false;
            }
            if (count == 0)
            {
                error = $"Entry {i}: sector count is zero";
                return false;
            }
            if (sector + count > totalSectors)
            {
                error = $"Entry {i}: sector {sector} + count {count} = {sector + count} exceeds total sectors {totalSectors}";
                return false;
            }
            for (int s = 0; s < count; s++)
                occupancy[sector + s]++;
        }

        // Check for sector overlap
        for (int s = 2; s < totalSectors; s++)
        {
            if (occupancy[s] > 1)
            {
                error = $"Sector {s} is claimed by {occupancy[s]} entries";
                return false;
            }
        }

        error = null;
        return true;
    }
}
