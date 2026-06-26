using System.Text.RegularExpressions;
using LegacyEditor.Models;

namespace LegacyEditor.Services;

public static class PlayerDataService
{
    public static List<PlayerData> LoadPlayers(byte[] archiveData, IProgress<string>? progress = null)
    {
        var archive = MsArchive.Parse(archiveData);
        var players = new List<PlayerData>();
        var playerFiles = archive.Entries
            .Where(e => e.Filename.StartsWith("players\\") && e.Filename.EndsWith(".dat"))
            .ToList();

        if (playerFiles.Count == 0)
            playerFiles = archive.Entries
                .Where(e => e.Filename.StartsWith("players/") && e.Filename.EndsWith(".dat"))
                .ToList();

        var total = playerFiles.Count;
        var mapCount = 0;
        progress?.Report($"Scanning {total} player(s)...");

        for (int i = 0; i < total; i++)
        {
            var entry = playerFiles[i];
            var xuid = ExtractXuid(entry.Filename);
            if (xuid == null) continue;

            try
            {
                var raw = new byte[entry.Length];
                Array.Copy(archiveData, entry.StartOffset, raw, 0, entry.Length);
                var decompressed = TryDecompress(raw);
                if (decompressed == null) continue;
                var tag = NbtParser.Parse(decompressed);
                if (tag == null) continue;

                var player = ParsePlayerNbt(tag, xuid.Value);
                if (player != null)
                {
                    players.Add(player);
                    mapCount += player.MapCount;
                    progress?.Report($"Player {i + 1}/{total}: {player.Username} — {player.MapCount} map(s), {player.TotalItemCount} item(s)");
                }
            }
            catch { }
        }

        progress?.Report($"Loaded {players.Count} player(s) with {mapCount} total map(s)");

        return players;
    }

    static byte[]? TryDecompress(byte[] data)
    {
        try { return ZLibDecompress(data); } catch { }
        try { return GZipDecompress(data); } catch { }
        return data;
    }

    static ulong? ExtractXuid(string filename)
    {
        var match = Regex.Match(filename, @"players[\\/](\d+)\.dat");
        if (match.Success && ulong.TryParse(match.Groups[1].Value, out var xuid))
            return xuid;
        return null;
    }

    static PlayerData? ParsePlayerNbt(NbtParser.NbtTag root, ulong xuid)
    {
        if (root.Type != NbtParser.TagCompound) return null;

        var dict = root.Value as Dictionary<string, NbtParser.NbtTag>;
        if (dict == null) return null;

        var player = new PlayerData { XUID = xuid };

        player.Username = GetString(dict, "LastKnownName");
        if (string.IsNullOrEmpty(player.Username))
        {
            // Try display->Name structure
            var disp = dict.GetValueOrDefault("display");
            if (disp?.Type == NbtParser.TagCompound)
            {
                var dispDict = disp.Value as Dictionary<string, NbtParser.NbtTag>;
                if (dispDict != null)
                    player.Username = GetString(dispDict, "Name");
            }
        }
        if (string.IsNullOrEmpty(player.Username))
            player.Username = xuid.ToString();

        player.Health = GetDouble(dict, "Health");
        player.Hunger = (int)GetDouble(dict, "foodLevel");
        player.XpLevel = GetInt(dict, "XpLevel");
        player.XpTotal = GetInt(dict, "XpTotal");
        player.Score = GetInt(dict, "Score");

        var pos = GetList(dict, "Pos");
        if (pos?.Items.Count >= 3)
        {
            player.PosX = Convert.ToDouble(pos.Items[0]);
            player.PosY = Convert.ToDouble(pos.Items[1]);
            player.PosZ = Convert.ToDouble(pos.Items[2]);
        }

        ParseInventory(dict, player);
        ParseEnderChest(dict, player);
        return player;
    }

    static void ParseInventory(Dictionary<string, NbtParser.NbtTag> dict, PlayerData player)
    {
        var invTag = dict.GetValueOrDefault("Inventory");
        if (invTag?.Type != NbtParser.TagList) return;

        var list = invTag.Value as ListTag;
        if (list == null) return;

        foreach (var item in list.Items)
        {
            var itemDict = item as Dictionary<string, NbtParser.NbtTag>;
            if (itemDict == null) continue;

            var name = ResolveItemName(itemDict);
            if (name == null) continue;

            var count = GetCount(itemDict, "Count");
            var damage = GetShort(itemDict, "Damage");
            var slot = GetByteInt(itemDict, "Slot");
            var id = GetShort(itemDict, "id");

            if (id == 0) id = GetByte(itemDict, "id");

            if (slot >= 100 && slot <= 103)
            {
                player.Armor.Add(new ItemStack { Name = name, Id = id, Count = count, Damage = damage });
            }
            else
            {
                player.Inventory.Add(new ItemStack { Name = name, Id = id, Count = count, Damage = damage });
                player.TotalItems += 1;
            }
            player.TotalItemCount += count;
        }
    }

    static void ParseEnderChest(Dictionary<string, NbtParser.NbtTag> dict, PlayerData player)
    {
        var ecTag = dict.GetValueOrDefault("EnderItems");
        if (ecTag?.Type != NbtParser.TagList) return;

        var list = ecTag.Value as ListTag;
        if (list == null) return;

        foreach (var item in list.Items)
        {
            var itemDict = item as Dictionary<string, NbtParser.NbtTag>;
            if (itemDict == null) continue;

            var name = ResolveItemName(itemDict);
            if (name == null) continue;

            var count = GetCount(itemDict, "Count");
            var damage = GetShort(itemDict, "Damage");
            var slot = GetByteInt(itemDict, "Slot");
            var id = GetShort(itemDict, "id");

            if (id == 0) id = GetByte(itemDict, "id");

            player.EnderChest.Add(new ItemStack { Name = name, Id = id, Count = count, Damage = damage });
            player.TotalItemCount += count;
        }
    }

    static string? ResolveItemName(Dictionary<string, NbtParser.NbtTag> itemDict)
    {
        var idStr = GetString(itemDict, "id");
        if (!string.IsNullOrEmpty(idStr))
        {
            if (int.TryParse(idStr, out var parsedId))
                return ItemRegistry.GetItemName(parsedId);
            return idStr;
        }
        var idNum = GetShort(itemDict, "id");
        if (idNum == 0)
        {
            var idByte = GetByte(itemDict, "id");
            if (idByte == 0) return null;
            idNum = idByte;
        }
        return ItemRegistry.GetItemName(idNum);
    }

    static int GetCount(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (!dict.TryGetValue(key, out var tag)) return 0;
        if (tag.Value is byte b) return b;
        if (tag.Value is short s) return s;
        if (tag.Value is int i) return i;
        return 0;
    }

    static int GetByteInt(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (!dict.TryGetValue(key, out var tag)) return 0;
        if (tag.Value is byte b) return b;
        if (tag.Value is short s) return s;
        if (tag.Value is int i) return i;
        return 0;
    }

    static double GetDouble(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (!dict.TryGetValue(key, out var tag)) return 0;
        if (tag.Value is double d) return d;
        if (tag.Value is float f) return f;
        if (tag.Value is int i) return i;
        if (tag.Value is short s) return s;
        if (tag.Value is byte b) return b;
        if (tag.Value is long l) return l;
        return 0;
    }

    static int GetInt(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (!dict.TryGetValue(key, out var tag)) return 0;
        if (tag.Value is int i) return i;
        if (tag.Value is short s) return s;
        if (tag.Value is byte b) return b;
        if (tag.Value is long l) return (int)l;
        return 0;
    }

    static short GetShort(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (!dict.TryGetValue(key, out var tag)) return 0;
        if (tag.Value is short s) return s;
        if (tag.Value is byte b) return b;
        if (tag.Value is int i) return (short)i;
        return 0;
    }

    static byte GetByte(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (!dict.TryGetValue(key, out var tag)) return 0;
        if (tag.Value is byte b) return b;
        if (tag.Value is short s) return (byte)s;
        if (tag.Value is int i) return (byte)i;
        return 0;
    }

    static string GetString(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (dict.TryGetValue(key, out var tag) && tag.Value is string s) return s;
        return "";
    }

    static ListTag? GetList(Dictionary<string, NbtParser.NbtTag> dict, string key)
    {
        if (dict.TryGetValue(key, out var tag) && tag.Type == NbtParser.TagList)
            return tag.Value as ListTag;
        return null;
    }

    static byte[] GZipDecompress(byte[] data)
    {
        using var comp = new System.IO.MemoryStream(data);
        using var gzip = new System.IO.Compression.GZipStream(comp, System.IO.Compression.CompressionMode.Decompress);
        using var result = new System.IO.MemoryStream();
        gzip.CopyTo(result);
        return result.ToArray();
    }

    static byte[] ZLibDecompress(byte[] data)
    {
        using var comp = new System.IO.MemoryStream(data);
        using var deflate = new System.IO.Compression.ZLibStream(comp, System.IO.Compression.CompressionMode.Decompress);
        using var result = new System.IO.MemoryStream();
        deflate.CopyTo(result);
        return result.ToArray();
    }
}
