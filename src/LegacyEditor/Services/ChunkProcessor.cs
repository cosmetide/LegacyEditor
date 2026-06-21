using NbtParser = LegacyEditor.Services.NbtParser;
using LegacyEditor.Models;

namespace LegacyEditor.Services;

public static class ChunkProcessor
{
    public static (byte[]? newRaw, List<WipeResult> removed) ProcessChunk(
        byte[] raw, HashSet<string> wipeSet, WipeMode mode = WipeMode.Whitelist)
    {
        if (raw.Length == 0) return (null, []);

        if (raw[0] == NbtParser.TagCompound)
        {
            var root = NbtParser.Parse(raw);
            if (root == null) return (null, []);

            var level = GetCompoundValue(root, "Level") ?? root;
            var removed = FilterEntities(level.Value as Dictionary<string, NbtParser.NbtTag>, wipeSet, mode);
            if (removed.Count == 0) return (null, []);

            return (NbtParser.Write(root), removed);
        }

        return ProcessBinaryChunk(raw, wipeSet, mode);
    }

    static (byte[]? newRaw, List<WipeResult> removed) ProcessBinaryChunk(
        byte[] raw, HashSet<string> wipeSet, WipeMode mode = WipeMode.Whitelist)
    {
        int offset = 0;
        if (raw.Length < 18) return (null, []);

        offset += 2; // fmt_ver
        offset += 8; // cx, cz
        offset += 8; // gameTime

        int fmtVer = (raw[0] << 8) | raw[1];
        if (fmtVer >= 9) offset += 8;

        offset = SkipCompressedTileStorage(raw, offset);
        if (offset < 0) return (null, []);
        offset = SkipCompressedTileStorage(raw, offset);
        if (offset < 0) return (null, []);

        for (int i = 0; i < 6; i++)
        {
            offset = SkipSparseStorage(raw, offset);
            if (offset < 0) return (null, []);
        }

        if (offset + 256 + 2 + 256 > raw.Length) return (null, []);
        offset += 256 + 2 + 256;

        if (offset >= raw.Length || raw[offset] != NbtParser.TagCompound) return (null, []);

        int nbtStart = offset;
        var tag = NbtParser.Parse(raw[offset..]);
        if (tag == null) return (null, []);

        int nbtEnd = offset + FindNbtEnd(raw, offset);
        var target = tag.Value as Dictionary<string, NbtParser.NbtTag>;
        var removed = FilterEntities(target, wipeSet, mode);
        if (removed.Count == 0) return (null, []);

        var newNbt = NbtParser.Write(tag);
        var newRaw = new byte[nbtStart + newNbt.Length + (raw.Length - nbtEnd)];
        Array.Copy(raw, newRaw, nbtStart);
        Array.Copy(newNbt, 0, newRaw, nbtStart, newNbt.Length);
        Array.Copy(raw, nbtEnd, newRaw, nbtStart + newNbt.Length, raw.Length - nbtEnd);

        return (newRaw, removed);
    }

    static List<WipeResult> FilterEntities(
        Dictionary<string, NbtParser.NbtTag>? compound, HashSet<string> wipeSet, WipeMode mode)
    {
        var removed = new List<WipeResult>();
        if (compound == null) return removed;

        foreach (var listKey in new[] { "Entities", "TileEntities" })
        {
            if (!compound.TryGetValue(listKey, out var lst)) continue;
            if (lst.Type != NbtParser.TagList) continue;

            var list = (ListTag?)lst.Value;
            if (list == null) continue;

            var newItems = new List<object?>();
            foreach (var itemObj in list.Items)
            {
                var item = itemObj as Dictionary<string, NbtParser.NbtTag>;
                if (item == null) { newItems.Add(itemObj); continue; }

                if (item.TryGetValue("id", out var idTag) && idTag.Type == NbtParser.TagString)
                {
                    var ename = (string)idTag.Value!;
                    bool isSelected = wipeSet.Contains(ename);

                    bool shouldKeep = mode == WipeMode.Whitelist ? !isSelected : isSelected;
                    if (shouldKeep)
                    {
                        newItems.Add(itemObj);
                    }
                    else
                    {
                        var pos = GetEntityPos(item);
                        removed.Add(new WipeResult
                        {
                            ListKey = listKey,
                            EntityId = ename,
                            PosX = pos?.x,
                            PosY = pos?.y,
                            PosZ = pos?.z
                        });
                    }
                }
                else
                    newItems.Add(itemObj);
            }

            if (newItems.Count < list.Items.Count)
                list.Items = newItems;
        }

        return removed;
    }

    static NbtParser.NbtTag? GetCompoundValue(NbtParser.NbtTag tag, string name)
    {
        if (tag.Value is Dictionary<string, NbtParser.NbtTag> dict &&
            dict.TryGetValue(name, out var found))
            return found;
        return null;
    }

    static (double x, double y, double z)? GetEntityPos(Dictionary<string, NbtParser.NbtTag> item)
    {
        if (item.TryGetValue("Pos", out var posTag) && posTag.Type == NbtParser.TagList)
        {
            var list = (ListTag?)posTag.Value;
            if (list?.Items.Count >= 3)
                return (Convert.ToDouble(list.Items[0]), Convert.ToDouble(list.Items[1]), Convert.ToDouble(list.Items[2]));
        }
        double? x = null, y = null, z = null;
        if (item.TryGetValue("x", out var xt) && xt.Type == NbtParser.TagInt) x = (int)xt.Value!;
        if (item.TryGetValue("y", out var yt) && yt.Type == NbtParser.TagInt) y = (int)yt.Value!;
        if (item.TryGetValue("z", out var zt) && zt.Type == NbtParser.TagInt) z = (int)zt.Value!;
        if (x != null && y != null && z != null) return (x.Value, y.Value, z.Value);
        return null;
    }

    static int SkipCompressedTileStorage(byte[] raw, int offset)
    {
        if (offset + 4 > raw.Length) return -1;
        int size = (raw[offset] << 24) | (raw[offset + 1] << 16) | (raw[offset + 2] << 8) | raw[offset + 3];
        offset += 4;
        if (size > 0) offset += size;
        return offset > raw.Length ? -1 : offset;
    }

    static int SkipSparseStorage(byte[] raw, int offset)
    {
        if (offset + 4 > raw.Length) return -1;
        int count = (raw[offset] << 24) | (raw[offset + 1] << 16) | (raw[offset + 2] << 8) | raw[offset + 3];
        offset += 4;
        offset += count * 128 + 128;
        return offset > raw.Length ? -1 : offset;
    }

    static int FindNbtEnd(byte[] raw, int start)
    {
        int i = start;
        if (i >= raw.Length) return raw.Length - start;
        byte rootType = raw[i++];
        if (rootType == NbtParser.TagEnd) return 1;
        if (i + 2 > raw.Length) return raw.Length - start;
        int nlen = (raw[i] << 8) | raw[i + 1];
        i += 2 + nlen;
        if (i > raw.Length) return raw.Length - start;
        int depth = 0;
        i = SkipPayload(raw, i, rootType, ref depth);
        if (i < 0) return raw.Length - start;
        return i - start;
    }

    static int SkipPayload(byte[] raw, int offset, byte type, ref int depth)
    {
        switch (type)
        {
            case NbtParser.TagByte: return offset + 1;
            case NbtParser.TagShort: return offset + 2;
            case NbtParser.TagInt: return offset + 4;
            case NbtParser.TagLong: return offset + 8;
            case NbtParser.TagFloat: return offset + 4;
            case NbtParser.TagDouble: return offset + 8;
            case NbtParser.TagByteArray:
                if (offset + 4 > raw.Length) return -1;
                int blen = (raw[offset] << 24) | (raw[offset + 1] << 16) | (raw[offset + 2] << 8) | raw[offset + 3];
                return offset + 4 + blen;
            case NbtParser.TagString:
                if (offset + 2 > raw.Length) return -1;
                int slen = (raw[offset] << 8) | raw[offset + 1];
                return offset + 2 + slen;
            case NbtParser.TagList:
                if (offset + 5 > raw.Length) return -1;
                byte elemType = raw[offset];
                int count = (raw[offset + 1] << 24) | (raw[offset + 2] << 16) | (raw[offset + 3] << 8) | raw[offset + 4];
                offset += 5;
                for (int i = 0; i < count; i++)
                {
                    offset = SkipPayload(raw, offset, elemType, ref depth);
                    if (offset < 0) return -1;
                }
                return offset;
            case NbtParser.TagCompound:
                depth++;
                while (offset < raw.Length)
                {
                    byte childType = raw[offset++];
                    if (childType == NbtParser.TagEnd) { depth--; return offset; }
                    if (offset + 2 > raw.Length) return -1;
                    int cnlen = (raw[offset] << 8) | raw[offset + 1];
                    offset += 2 + cnlen;
                    if (offset > raw.Length) return -1;
                    offset = SkipPayload(raw, offset, childType, ref depth);
                    if (offset < 0) return -1;
                }
                return offset;
            case NbtParser.TagIntArray:
                if (offset + 4 > raw.Length) return -1;
                int alen = (raw[offset] << 24) | (raw[offset + 1] << 16) | (raw[offset + 2] << 8) | raw[offset + 3];
                return offset + 4 + alen * 4;
            default: return -1;
        }
    }
}
