using System.IO;
using System.IO.Compression;

namespace LegacyEditor.Services;

public static class Compression
{
    public static byte[] RleDecode(byte[] data, int destSize)
    {
        using var ms = new MemoryStream(destSize);
        int i = 0;
        while (i < data.Length && ms.Position < destSize)
        {
            byte b = data[i++];
            if (b == 0xFF)
            {
                if (i >= data.Length) break;
                int count = data[i++];
                if (count < 3)
                {
                    count += 1;
                    for (int j = 0; j < count; j++) ms.WriteByte(0xFF);
                }
                else
                {
                    count += 1;
                    if (i >= data.Length) break;
                    byte val = data[i++];
                    for (int j = 0; j < count; j++) ms.WriteByte(val);
                }
            }
            else
            {
                ms.WriteByte(b);
            }
        }
        byte[] result = new byte[destSize];
        int copied = (int)ms.Position;
        Array.Copy(ms.GetBuffer(), result, Math.Min(copied, destSize));
        return result;
    }

    public static byte[] RleEncode(byte[] data)
    {
        using var ms = new MemoryStream();
        int i = 0;
        int n = data.Length;
        while (i < n)
        {
            byte b = data[i];
            int count = 1;
            while (i + count < n && data[i + count] == b && count < 256)
                count++;
            if (count <= 3)
            {
                if (b == 0xFF)
                {
                    ms.WriteByte(0xFF);
                    ms.WriteByte((byte)(count - 1));
                }
                else
                {
                    for (int j = 0; j < count; j++) ms.WriteByte(b);
                }
            }
            else
            {
                ms.WriteByte(0xFF);
                ms.WriteByte((byte)(count - 1));
                ms.WriteByte(b);
            }
            i += count;
        }
        return ms.ToArray();
    }

    public static byte[] DecompressChunk(byte[] compressed, int decompLen)
    {
        var rleData = ZLibDecompress(compressed);
        return RleDecode(rleData, decompLen);
    }

    public static byte[] CompressChunk(byte[] raw)
    {
        var rleData = RleEncode(raw);
        return ZLibCompress(rleData);
    }

    public static byte[] ZLibDecompress(byte[] data)
    {
        using var compressedStream = new MemoryStream(data);
        using var deflateStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        deflateStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    public static byte[] ZLibCompress(byte[] data)
    {
        using var resultStream = new MemoryStream();
        using (var deflateStream = new ZLibStream(resultStream, CompressionLevel.Optimal))
        {
            deflateStream.Write(data, 0, data.Length);
        }
        return resultStream.ToArray();
    }
}
