using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class DownloadEntry
    {
        public int Index;
        public byte[] Unk;

        public Dictionary<string, DownloadTag> Tags;
    }

    public class DownloadTag
    {
        public short Type;
        public BitArray Bits;
    }

    public class DownloadHandler
    {
        private static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        private readonly Dictionary<byte[], DownloadEntry> DownloadData = new Dictionary<byte[], DownloadEntry>(comparer);
        Dictionary<string, DownloadTag> Tags = new Dictionary<string, DownloadTag>();

        public int Count
        {
            get { return DownloadData.Count; }
        }

        public DownloadHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"download\"...");

            stream.Skip(2); // DL

            byte b1 = stream.ReadByte();
            byte b2 = stream.ReadByte();
            byte b3 = stream.ReadByte();

            int numFiles = stream.ReadInt32BE();

            short numTags = stream.ReadInt16BE();

            int numMaskBytes = (numFiles + 7) / 8;

            for (int i = 0; i < numFiles; i++)
            {
                byte[] key = stream.ReadBytes(0x10);

                byte[] unk = stream.ReadBytes(0xA);

                var entry = new DownloadEntry() { Index = i, Unk = unk };

                DownloadData.Add(key, entry);

                worker?.ReportProgress((int)((i + 1) / (float)numFiles * 100));
            }

            for (int i = 0; i < numTags; i++)
            {
                DownloadTag tag = new DownloadTag();
                string name = stream.ReadCString();
                tag.Type = stream.ReadInt16BE();

                byte[] bits = stream.ReadBytes(numMaskBytes);

                for (int j = 0; j < numMaskBytes; j++)
                    bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                tag.Bits = new BitArray(bits);

                Tags.Add(name, tag);
            }
        }

        public void Dump()
        {
            foreach (var entry in DownloadData)
            {
                if (entry.Value.Tags == null)
                    entry.Value.Tags = Tags.Where(kv => kv.Value.Bits[entry.Value.Index]).ToDictionary(kv => kv.Key, kv => kv.Value);

                Logger.WriteLine("{0} {1} {2}", entry.Key.ToHexString(), entry.Value.Unk.ToHexString(), string.Join(",", entry.Value.Tags.Select(tag => tag.Key)));
            }
        }

        public DownloadEntry GetEntry(byte[] key)
        {
            DownloadEntry entry;
            DownloadData.TryGetValue(key, out entry);

            if (entry != null && entry.Tags == null)
                entry.Tags = Tags.Where(kv => kv.Value.Bits[entry.Index]).ToDictionary(kv => kv.Key, kv => kv.Value);

            return entry;
        }

        public void Clear()
        {
            DownloadData.Clear();
        }
    }
}
