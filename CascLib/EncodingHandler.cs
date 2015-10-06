using System;
using System.Collections.Generic;
using System.Text;

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

        private const int CHUNK_SIZE = 4096;

        public int Count
        {
            get { return EncodingData.Count; }
        }

        public EncodingHandler(MMStream stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"encoding\"...");

            stream.Skip(2); // EN
            byte b1 = stream.ReadByte();
            byte checksumSizeA = stream.ReadByte();
            byte checksumSizeB = stream.ReadByte();
            ushort flagsA = stream.ReadUInt16();
            ushort flagsB = stream.ReadUInt16();
            int numEntriesA = stream.ReadInt32BE();
            int numEntriesB = stream.ReadInt32BE();
            byte b4 = stream.ReadByte();
            int stringBlockSize = stream.ReadInt32BE();

            string[] strings = Encoding.ASCII.GetString(stream.ReadBytes(stringBlockSize)).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < numEntriesA; ++i)
            {
                byte[] firstHash = stream.ReadBytes(16);
                byte[] blockHash = stream.ReadBytes(16);
            }

            long chunkStart = stream.Position;

            for (int i = 0; i < numEntriesA; ++i)
            {
                ushort keysCount;

                while ((keysCount = stream.ReadUInt16()) != 0)
                {
                    int fileSize = stream.ReadInt32BE();
                    byte[] md5 = stream.ReadBytes(16);

                    EncodingEntry entry = new EncodingEntry();
                    entry.Size = fileSize;

                    // how do we handle multiple keys?
                    for (int ki = 0; ki < keysCount; ++ki)
                    {
                        byte[] key = stream.ReadBytes(16);

                        // use first key for now
                        if (ki == 0)
                            entry.Key = key;
                        else
                            Logger.WriteLine("Multiple encoding keys for MD5 {0}: {1}", md5.ToHexString(), key.ToHexString());
                    }

                    //Encodings[md5] = entry;
                    EncodingData.Add(md5, entry);
                }

                // each chunk is 4096 bytes, and zero padding at the end
                long remaining = CHUNK_SIZE - ((stream.Position - chunkStart) % CHUNK_SIZE);

                if (remaining > 0)
                    stream.Position += remaining;

                worker?.ReportProgress((int)(i / (float)numEntriesA * 100));
            }

            for (int i = 0; i < numEntriesB; ++i)
            {
                byte[] firstKey = stream.ReadBytes(16);
                byte[] blockHash = stream.ReadBytes(16);
            }

            long chunkStart2 = stream.Position;

            for (int i = 0; i < numEntriesB; ++i)
            {
                byte[] key = stream.ReadBytes(16);
                int stringIndex = stream.ReadInt32BE();
                byte unk1 = stream.ReadByte();
                int fileSize = stream.ReadInt32BE();

                // each chunk is 4096 bytes, and zero padding at the end
                long remaining = CHUNK_SIZE - ((stream.Position - chunkStart2) % CHUNK_SIZE);

                if (remaining > 0)
                    stream.Position += remaining;
            }

            // string block till the end of file
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
