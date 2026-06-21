using System.IO;
using System.Text;

namespace LegacyEditor.Services;

public static class NbtParser
{
    public const byte TagEnd = 0;
    public const byte TagByte = 1;
    public const byte TagShort = 2;
    public const byte TagInt = 3;
    public const byte TagLong = 4;
    public const byte TagFloat = 5;
    public const byte TagDouble = 6;
    public const byte TagByteArray = 7;
    public const byte TagString = 8;
    public const byte TagList = 9;
    public const byte TagCompound = 10;
    public const byte TagIntArray = 11;

    public class NbtTag
    {
        public byte Type { get; set; }
        public string Name { get; set; } = "";
        public object? Value { get; set; }
    }

    public static NbtTag? Parse(byte[] data)
    {
        var offset = 0;
        return ReadAny(data, ref offset);
    }

    public static byte[] Write(NbtTag tag)
    {
        using var ms = new MemoryStream();
        WriteNamed(ms, tag);
        return ms.ToArray();
    }

    static NbtTag? ReadAny(byte[] data, ref int offset, int depth = 0)
    {
        if (depth > 512) throw new InvalidOperationException("NBT depth exceeded 512");
        if (offset >= data.Length) return null;
        byte type = data[offset++];
        if (type == TagEnd) return null;
        var name = ReadString(data, ref offset);
        var value = ReadPayload(type, data, ref offset, depth);
        return new NbtTag { Type = type, Name = name, Value = value };
    }

    static void WriteNamed(MemoryStream ms, NbtTag tag)
    {
        if (tag.Type == TagEnd)
        {
            ms.WriteByte(TagEnd);
            return;
        }
        ms.WriteByte(tag.Type);
        WriteString(ms, tag.Name);
        WritePayload(ms, tag);
    }

    static string ReadString(byte[] data, ref int offset)
    {
        int len = (data[offset] << 8) | data[offset + 1];
        offset += 2;
        var s = Encoding.UTF8.GetString(data, offset, len);
        offset += len;
        return s;
    }

    static void WriteString(MemoryStream ms, string s)
    {
        var encoded = Encoding.UTF8.GetBytes(s);
        ms.WriteByte((byte)(encoded.Length >> 8));
        ms.WriteByte((byte)encoded.Length);
        ms.Write(encoded);
    }

    static object? ReadPayload(byte type, byte[] data, ref int offset, int depth)
    {
        switch (type)
        {
            case TagByte: return data[offset++];
            case TagShort:
                short s = (short)((data[offset] << 8) | data[offset + 1]);
                offset += 2; return s;
            case TagInt:
                int i = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
                offset += 4; return i;
            case TagLong:
                long l = ((long)data[offset] << 56) | ((long)data[offset + 1] << 48) | ((long)data[offset + 2] << 40) | ((long)data[offset + 3] << 32)
                       | ((long)data[offset + 4] << 24) | ((long)data[offset + 5] << 16) | ((long)data[offset + 6] << 8) | (long)data[offset + 7];
                offset += 8; return l;
            case TagFloat:
                var fbytes = new byte[4];
                Array.Copy(data, offset, fbytes, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(fbytes);
                offset += 4;
                return BitConverter.ToSingle(fbytes, 0);
            case TagDouble:
                var dbytes = new byte[8];
                Array.Copy(data, offset, dbytes, 0, 8);
                if (BitConverter.IsLittleEndian) Array.Reverse(dbytes);
                offset += 8;
                return BitConverter.ToDouble(dbytes, 0);
            case TagByteArray:
                int blen = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
                offset += 4;
                var ba = new byte[blen];
                Array.Copy(data, offset, ba, 0, blen);
                offset += blen; return ba;
            case TagString: return ReadString(data, ref offset);
            case TagList:
                byte elemType = data[offset++];
                int count = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
                offset += 4;
                var items = new List<object?>(count);
                for (int j = 0; j < count; j++)
                    items.Add(ReadPayload(elemType, data, ref offset, depth + 1));
                return new ListTag { ElementType = elemType, Items = items };
            case TagCompound:
                var dict = new Dictionary<string, NbtTag>();
                while (true)
                {
                    var tag = ReadAny(data, ref offset, depth + 1);
                    if (tag == null) break;
                    dict[tag.Name] = tag;
                }
                return dict;
            case TagIntArray:
                int alen = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
                offset += 4;
                var arr = new int[alen];
                for (int j = 0; j < alen; j++)
                {
                    arr[j] = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
                    offset += 4;
                }
                return arr;
            default:
                throw new InvalidOperationException($"Unknown NBT tag type: {type}");
        }
    }

    static void WritePayload(MemoryStream ms, NbtTag tag)
    {
        switch (tag.Type)
        {
            case TagByte: ms.WriteByte((byte)tag.Value!); break;
            case TagShort:
                short sv = (short)tag.Value!;
                ms.WriteByte((byte)(sv >> 8)); ms.WriteByte((byte)sv); break;
            case TagInt:
                int iv = (int)tag.Value!;
                ms.WriteByte((byte)(iv >> 24)); ms.WriteByte((byte)(iv >> 16));
                ms.WriteByte((byte)(iv >> 8)); ms.WriteByte((byte)iv); break;
            case TagLong:
                long lv = (long)tag.Value!;
                ms.WriteByte((byte)(lv >> 56)); ms.WriteByte((byte)(lv >> 48));
                ms.WriteByte((byte)(lv >> 40)); ms.WriteByte((byte)(lv >> 32));
                ms.WriteByte((byte)(lv >> 24)); ms.WriteByte((byte)(lv >> 16));
                ms.WriteByte((byte)(lv >> 8)); ms.WriteByte((byte)lv); break;
            case TagFloat:
                var fbytes = BitConverter.GetBytes((float)tag.Value!);
                if (BitConverter.IsLittleEndian) Array.Reverse(fbytes);
                ms.Write(fbytes); break;
            case TagDouble:
                var dbytes = BitConverter.GetBytes((double)tag.Value!);
                if (BitConverter.IsLittleEndian) Array.Reverse(dbytes);
                ms.Write(dbytes); break;
            case TagByteArray:
                var ba = (byte[])tag.Value!;
                ms.WriteByte((byte)(ba.Length >> 24)); ms.WriteByte((byte)(ba.Length >> 16));
                ms.WriteByte((byte)(ba.Length >> 8)); ms.WriteByte((byte)ba.Length);
                ms.Write(ba); break;
            case TagString:
                WriteString(ms, (string)tag.Value!); break;
            case TagList:
                var list = (ListTag)tag.Value!;
                ms.WriteByte(list.ElementType);
                int lc = list.Items.Count;
                ms.WriteByte((byte)(lc >> 24)); ms.WriteByte((byte)(lc >> 16));
                ms.WriteByte((byte)(lc >> 8)); ms.WriteByte((byte)lc);
                foreach (var item in list.Items)
                    WritePayloadValue(ms, list.ElementType, item);
                break;
            case TagCompound:
                var dict = (Dictionary<string, NbtTag>)tag.Value!;
                foreach (var kv in dict.Values)
                    WriteNamed(ms, kv);
                ms.WriteByte(TagEnd);
                break;
            case TagIntArray:
                var arr = (int[])tag.Value!;
                ms.WriteByte((byte)(arr.Length >> 24)); ms.WriteByte((byte)(arr.Length >> 16));
                ms.WriteByte((byte)(arr.Length >> 8)); ms.WriteByte((byte)arr.Length);
                foreach (int av in arr)
                {
                    ms.WriteByte((byte)(av >> 24)); ms.WriteByte((byte)(av >> 16));
                    ms.WriteByte((byte)(av >> 8)); ms.WriteByte((byte)av);
                }
                break;
        }
    }

    static void WritePayloadValue(MemoryStream ms, byte type, object? value)
    {
        var tag = new NbtTag { Type = type, Value = value };
        WritePayload(ms, tag);
    }
}

public class ListTag
{
    public byte ElementType { get; set; }
    public List<object?> Items { get; set; } = [];
}
