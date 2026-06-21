using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LegacyEditor.Services;

public class MsArchive
{
    public int HeaderOffset { get; set; }
    public int HeaderSize { get; set; }
    public ushort OrigVer { get; set; }
    public ushort SaveVer { get; set; }
    public List<FileEntry> Entries { get; set; } = [];

    public class FileEntry
    {
        public string Filename { get; set; } = "";
        public int Length { get; set; }
        public int StartOffset { get; set; }
        public long LastModified { get; set; }
    }

    public static MsArchive Parse(byte[] data)
    {
        var hoff = ReadI32LE(data, 0);
        var hsize = ReadI32LE(data, 4);
        var over = ReadU16LE(data, 8);
        var sver = ReadU16LE(data, 10);

        var archive = new MsArchive
        {
            HeaderOffset = hoff,
            HeaderSize = hsize,
            OrigVer = over,
            SaveVer = sver
        };

        int pos = hoff;
        for (int i = 0; i < hsize; i++)
        {
            var fnRaw = new byte[128];
            Array.Copy(data, pos, fnRaw, 0, 128);
            var fn = Encoding.Unicode.GetString(fnRaw).TrimEnd('\0').Replace('/', '\\');
            pos += 128;

            var length = ReadI32LE(data, pos);
            var startOff = ReadI32LE(data, pos + 4);
            pos += 8;
            var lm = ReadI64LE(data, pos);
            pos += 8;

            archive.Entries.Add(new FileEntry
            {
                Filename = fn,
                Length = length,
                StartOffset = startOff,
                LastModified = lm
            });
        }

        return archive;
    }

    public byte[] Rebuild(byte[] originalData, Dictionary<string, byte[]> updatedFiles)
    {
        var dataBlob = new MemoryStream();
        foreach (var ent in Entries)
        {
            byte[] content;
            if (updatedFiles.TryGetValue(ent.Filename, out var updated))
                content = updated;
            else
                content = ExtractFile(originalData, ent);

            ent.StartOffset = 12 + (int)dataBlob.Position;
            ent.Length = content.Length;
            dataBlob.Write(content);
        }

        HeaderOffset = 12 + (int)dataBlob.Position;
        HeaderSize = Entries.Count;

        var buf = new MemoryStream();
        WriteI32LE(buf, HeaderOffset);
        WriteI32LE(buf, HeaderSize);
        WriteU16LE(buf, OrigVer);
        WriteU16LE(buf, SaveVer);
        buf.Write(dataBlob.ToArray());

        foreach (var ent in Entries)
        {
            var fnEnc = Encoding.Unicode.GetBytes(ent.Filename);
            var fnPadded = new byte[128];
            Array.Copy(fnEnc, fnPadded, Math.Min(fnEnc.Length, 128));
            buf.Write(fnPadded);
            WriteI32LE(buf, ent.Length);
            WriteI32LE(buf, ent.StartOffset);
            WriteI64LE(buf, ent.LastModified);
        }

        return buf.ToArray();
    }

    public byte[] Rebuild(byte[] originalData, HashSet<string> excludeFilenames, IProgress<(int current, int total)>? progress = null)
    {
        var keep = Entries.Where(e => !excludeFilenames.Contains(e.Filename)).ToList();
        int total = keep.Count;
        var dataBlob = new MemoryStream();
        for (int i = 0; i < keep.Count; i++)
        {
            var ent = keep[i];
            byte[] content = ExtractFile(originalData, ent);
            ent.StartOffset = 12 + (int)dataBlob.Position;
            ent.Length = content.Length;
            dataBlob.Write(content);
            progress?.Report((i + 1, total));
        }

        HeaderOffset = 12 + (int)dataBlob.Position;
        HeaderSize = keep.Count;

        var buf = new MemoryStream();
        WriteI32LE(buf, HeaderOffset);
        WriteI32LE(buf, HeaderSize);
        WriteU16LE(buf, OrigVer);
        WriteU16LE(buf, SaveVer);
        buf.Write(dataBlob.ToArray());

        foreach (var ent in keep)
        {
            var fnEnc = Encoding.Unicode.GetBytes(ent.Filename);
            var fnPadded = new byte[128];
            Array.Copy(fnEnc, fnPadded, Math.Min(fnEnc.Length, 128));
            buf.Write(fnPadded);
            WriteI32LE(buf, ent.Length);
            WriteI32LE(buf, ent.StartOffset);
            WriteI64LE(buf, ent.LastModified);
        }

        Entries.Clear();
        Entries.AddRange(keep);
        return buf.ToArray();
    }

    public static byte[] ExtractFile(byte[] data, FileEntry ent)
    {
        if (ent.StartOffset + ent.Length <= data.Length)
        {
            var buf = new byte[ent.Length];
            Array.Copy(data, ent.StartOffset, buf, 0, ent.Length);
            return buf;
        }
        return [];
    }

    static int ReadI32LE(byte[] d, int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);
    static ushort ReadU16LE(byte[] d, int o) => (ushort)(d[o] | (d[o + 1] << 8));
    static long ReadI64LE(byte[] d, int o) =>
        (long)d[o] | ((long)d[o + 1] << 8) | ((long)d[o + 2] << 16) | ((long)d[o + 3] << 24) |
        ((long)d[o + 4] << 32) | ((long)d[o + 5] << 40) | ((long)d[o + 6] << 48) | ((long)d[o + 7] << 56);
    static void WriteI32LE(MemoryStream ms, int v)
    {
        ms.WriteByte((byte)v); ms.WriteByte((byte)(v >> 8));
        ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 24));
    }
    static void WriteU16LE(MemoryStream ms, ushort v)
    {
        ms.WriteByte((byte)v); ms.WriteByte((byte)(v >> 8));
    }
    static void WriteI64LE(MemoryStream ms, long v)
    {
        ms.WriteByte((byte)v); ms.WriteByte((byte)(v >> 8));
        ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 24));
        ms.WriteByte((byte)(v >> 32)); ms.WriteByte((byte)(v >> 40));
        ms.WriteByte((byte)(v >> 48)); ms.WriteByte((byte)(v >> 56));
    }
}
