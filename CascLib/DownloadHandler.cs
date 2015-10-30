using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CASCExplorer
{
    public class DownloadEntry
    {
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

        public int Count
        {
            get { return DownloadData.Count; }
        }

        public DownloadHandler(MMStream stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"download\"...");

            stream.Skip(2); // DL

            byte b1 = stream.ReadByte();
            byte b2 = stream.ReadByte();
            byte b3 = stream.ReadByte();

            int numFiles = stream.ReadInt32BE();

            short numTags = stream.ReadInt16BE();

            int numMaskBytes = numFiles / 8 + (numFiles % 8 > 0 ? 1 : 0);

            List<DownloadEntry> entries = new List<DownloadEntry>();

            for (int i = 0; i < numFiles; i++)
            {
                byte[] key = stream.ReadBytes(16);

                byte[] unk = stream.ReadBytes(10);

                var entry = new DownloadEntry() { Unk = unk };

                DownloadData.Add(key, entry);
                entries.Add(entry);
            }

            Dictionary<string, DownloadTag> tags = new Dictionary<string, DownloadTag>();

            for (int i = 0; i < numTags; i++)
            {
                DownloadTag tag = new DownloadTag();
                string name = stream.ReadCString();
                tag.Type = stream.ReadInt16BE();

                byte[] bits = stream.ReadBytes(numMaskBytes);

                for (int j = 0; j < numMaskBytes; j++)
                    bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                tag.Bits = new BitArray(bits);

                tags.Add(name, tag);
            }

            for (int i = 0; i < numFiles; i++)
            {
                entries[i].Tags = tags.Where(kv => kv.Value.Bits[i]).ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            //foreach (var entry in DownloadData)
            //{
            //    Logger.WriteLine("{0} {1} {2}", entry.Key.ToHexString(), entry.Value.Unk.ToHexString(), string.Join(",", entry.Value.Tags.Select(tag => tag.Key)));
            //}
        }

        public DownloadEntry GetEntry(byte[] key)
        {
            DownloadEntry entry;
            DownloadData.TryGetValue(key, out entry);
            return entry;
        }
    }
}
