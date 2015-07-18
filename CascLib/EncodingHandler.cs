using System.Collections.Generic;
using System.IO;

namespace CASCExplorer
{
    public class EncodingEntry
    {
        public int Size;
        public byte[] Key;
    }

    public class EncodingHandler
    {
        private static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        private readonly Dictionary<byte[], EncodingEntry> EncodingData = new Dictionary<byte[], EncodingEntry>(comparer);

        public int Count
        {
            get { return EncodingData.Count; }
        }

        public EncodingHandler(MMStream stream, AsyncAction worker)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"encoding\"...");
            }

            stream.Skip(2); // EN
            byte b1 = stream.ReadByte();
            byte b2 = stream.ReadByte();
            byte b3 = stream.ReadByte();
            ushort s1 = stream.ReadUInt16();
            ushort s2 = stream.ReadUInt16();
            int numEntries = stream.ReadInt32BE();
            int i1 = stream.ReadInt32BE();
            byte b4 = stream.ReadByte();
            int entriesOfs = stream.ReadInt32BE();

            stream.Position += entriesOfs; // skip strings

            stream.Position += numEntries * 32;
            //for (int i = 0; i < numEntries; ++i)
            //{
            //    br.ReadBytes(16);
            //    br.ReadBytes(16);
            //}

            for (int i = 0; i < numEntries; ++i)
            {
                ushort keysCount;

                while ((keysCount = stream.ReadUInt16()) != 0)
                {
                    int fileSize = stream.ReadInt32BE();
                    byte[] md5 = stream.ReadBytes(16);

                    var entry = new EncodingEntry();
                    entry.Size = fileSize;

                    // how do we handle multiple keys?
                    for (int ki = 0; ki < keysCount; ++ki)
                    {
                        byte[] key = stream.ReadBytes(16);

                        // use first key for now
                        if (ki == 0)
                            entry.Key = key;
                        //else
                        //    Logger.WriteLine("Multiple encoding keys for MD5 {0}: {1}", md5.ToHexString(), key.ToHexString());
                    }

                    //Encodings[md5] = entry;
                    EncodingData.Add(md5, entry);
                }

                //br.ReadBytes(28);
                while (stream.PeekByte() == 0)
                    stream.Skip(1);

                if (worker != null)
                {
                    worker.ThrowOnCancel();
                    worker.ReportProgress((int)((float)i / (float)numEntries * 100));
                }
            }
            //var pos = br.BaseStream.Position;
            //for (int i = 0; i < i1; ++i)
            //{
            //    br.ReadBytes(16);
            //    br.ReadBytes(16);
            //}
        }

        public EncodingEntry GetEntry(byte[] md5)
        {
            EncodingEntry result;
            EncodingData.TryGetValue(md5, out result);
            return result;
        }

        public void Clear()
        {
            EncodingData.Clear();
        }
    }
}
