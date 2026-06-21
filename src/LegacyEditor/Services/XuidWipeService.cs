using System;
using System.IO;
using LegacyEditor.Models;
using Microsoft.Data.Sqlite;

namespace LegacyEditor.Services;

public static class XuidWipeService
{
    public static HashSet<ulong> LoadFromTextFile(string path)
    {
        var xuids = new HashSet<ulong>();
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed.StartsWith("0x") || trimmed.StartsWith("0X"))
            {
                if (ulong.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out var x))
                    xuids.Add(x);
            }
            else
            {
                if (ulong.TryParse(trimmed, out var x))
                    xuids.Add(x);
            }
        }
        return xuids;
    }

    public static HashSet<ulong> LoadFromAuthyDb(string dbPath)
    {
        var xuids = new HashSet<ulong>();
        var connStr = $"Data Source={dbPath}";
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT XUID FROM LinkedAccounts WHERE XUID IS NOT NULL AND XUID != ''";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var val = reader.GetString(0);
            if (ulong.TryParse(val, out var x))
                xuids.Add(x);
        }

        return xuids;
    }

    public static int CountKept(HashSet<ulong> allXuidsInSave, HashSet<ulong> importedXuids)
    {
        return allXuidsInSave.Count(x => importedXuids.Contains(x));
    }

    public static byte[] WipePlayers(byte[] archiveData, HashSet<ulong> keepXuids, IProgress<(int current, int total)>? progress = null)
    {
        var archive = MsArchive.Parse(archiveData);
        var toRemove = new HashSet<string>();

        foreach (var entry in archive.Entries)
        {
            if (!IsPlayerFile(entry.Filename)) continue;
            var xuid = ParseXuid(entry.Filename);
            if (xuid == null || !keepXuids.Contains(xuid.Value))
                toRemove.Add(entry.Filename);
        }

        if (toRemove.Count == 0) return archiveData;

        return archive.Rebuild(archiveData, toRemove, progress);
    }

    public static byte[] DeletePlayers(byte[] archiveData, List<PlayerData> toDelete)
    {
        var toRemove = toDelete.Select(p => $"players\\{p.XUID}.dat").ToHashSet();
        if (toRemove.Count == 0) return archiveData;
        var archive = MsArchive.Parse(archiveData);
        return archive.Rebuild(archiveData, toRemove);
    }

    public static byte[] WipeEmptyPlayers(byte[] archiveData, List<PlayerData> players, IProgress<(int current, int total)>? progress = null)
    {
        var empty = players
            .Where(p => p.TotalItems == 0 && p.XpLevel == 0 && p.EnderChest.Count == 0)
            .Select(p => $"players\\{p.XUID}.dat")
            .ToHashSet();

        if (empty.Count == 0) return archiveData;
        var archive = MsArchive.Parse(archiveData);
        return archive.Rebuild(archiveData, empty, progress);
    }

    static bool IsPlayerFile(string filename)
    {
        var norm = filename.Replace("/", "\\");
        return norm.StartsWith("players\\") && norm.EndsWith(".dat");
    }

    static ulong? ParseXuid(string filename)
    {
        var match = System.Text.RegularExpressions.Regex.Match(filename, @"players[\\/](\d+)\.dat");
        if (match.Success && ulong.TryParse(match.Groups[1].Value, out var xuid))
            return xuid;
        return null;
    }
}
