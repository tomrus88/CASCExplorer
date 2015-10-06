using System.Collections;
using System.Collections.Generic;

namespace CASCExplorer
{
    public class DownloadEntry
    {
        public byte[] MD5;
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

            short numMasks = stream.ReadInt16BE();

            int numMaskBytes = numFiles / 8 + (numFiles % 8 > 0 ? 1 : 0);

            for (int i = 0; i < numFiles; i++)
            {
                byte[] hash = stream.ReadBytes(16);

                byte[] unk = stream.ReadBytes(10); // probably it's data index and offset, may be something else

                Logger.WriteLine("{0} {1}", hash.ToHexString(), unk.ToHexString());

                DownloadData.Add(new DownloadEntry() { MD5 = hash, Unk = unk });
            }

            for (int i = 0; i < numMasks; i++)
            {
                DownloadTag mask = new DownloadTag();
                mask.Name = stream.ReadCString();
                mask.Type = stream.ReadInt16BE();

                byte[] bits = stream.ReadBytes(numMaskBytes);

                //for (int j = 0; j < numMaskBytes; ++j)
                //    bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                mask.Bits = new BitArray(bits);

                Tags.Add(mask);
            }

            for(int i = 0; i < numFiles; i++)
            {
                DownloadData[i].Tags = GetTagsForEntry(i);
            }
        }

        private List<DownloadTag> GetTagsForEntry(int index)
        {
            return Tags.FindAll(tag => tag.Bits[index]);
        }
    }
}
