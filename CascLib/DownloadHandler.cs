using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CASCExplorer
{
    public class DownloadEntry
    {
        public byte[] Key;
        public byte[] Unk;

        public List<DownloadTag> Tags;
    }

    public class DownloadTag
    {
        public string Name;
        public short Type;
        public BitArray Bits;
    }

    class DownloadHandler
    {
        private readonly List<DownloadEntry> DownloadData = new List<DownloadEntry>();
        private readonly List<DownloadTag> Tags = new List<DownloadTag>();

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

            for (int i = 0; i < numFiles; i++)
            {
                byte[] key = stream.ReadBytes(16);

                byte[] unk = stream.ReadBytes(10);

                DownloadData.Add(new DownloadEntry() { Key = key, Unk = unk });
            }

            for (int i = 0; i < numTags; i++)
            {
                DownloadTag tag = new DownloadTag();
                tag.Name = stream.ReadCString();
                tag.Type = stream.ReadInt16BE();

                byte[] bits = stream.ReadBytes(numMaskBytes);

                for (int j = 0; j < numMaskBytes; j++)
                    bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                tag.Bits = new BitArray(bits);

                Tags.Add(tag);
            }

            for (int i = 0; i < numFiles; i++)
            {
                DownloadData[i].Tags = Tags.FindAll(tag => tag.Bits[i]);

                Logger.WriteLine("{0} {1} {2}", DownloadData[i].Key.ToHexString(), DownloadData[i].Unk.ToHexString(), string.Join(",", DownloadData[i].Tags.Select(tag => tag.Name)));
            }
        }
    }
}
