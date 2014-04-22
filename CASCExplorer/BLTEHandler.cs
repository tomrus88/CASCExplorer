using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CASCExplorer
{
    class FileExistsException : Exception
    {
        public FileExistsException(string message) : base(message) { }
    }

    class BLTEChunk
    {
        public int compSize;
        public int decompSize;
        public byte[] hash;
        public byte[] data;
    }

    class BLTEHandler
    {
        BinaryReader reader;
        MD5 md5 = MD5.Create();
        int size;

        public BLTEHandler(Stream stream, int _size, bool raw)
        {
            this.reader = new BinaryReader(stream, Encoding.ASCII, true);

            if (!raw)
            {
                byte[] unkHash = reader.ReadBytes(16);
                int __size = reader.ReadInt32();
                byte[] unkData1 = reader.ReadBytes(2);
                byte[] unkData2 = reader.ReadBytes(8);
            }

            this.size = _size;
        }

        ~BLTEHandler()
        {
            reader.Close();
        }

        public void ExtractToFile(string path, string name)
        {
            var fullPath = Path.Combine(path, name);
            var dir = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fileStream = File.Open(fullPath, FileMode.Create))
            {
                long oldPos = reader.BaseStream.Position;
                ExtractData(fileStream);
                reader.BaseStream.Position = oldPos;
            }
        }

        public MemoryStream OpenFile()
        {
            MemoryStream memStream = new MemoryStream();
            long oldPos = reader.BaseStream.Position;
            ExtractData(memStream);
            reader.BaseStream.Position = oldPos;
            memStream.Position = 0;
            return memStream;
        }

        public void ExtractData(Stream stream)
        {
            int magic = reader.ReadInt32(); // BLTE (raw)

            if (magic != 0x45544c42)
                throw new InvalidDataException("BLTEHandler: magic");

            int frameHeaderSize = reader.ReadInt32BE();
            int chunkCount = 0;
            int totalSize = 0;

            if (frameHeaderSize == 0)
            {
                totalSize = size - 38;
                chunkCount = 1;
            }
            else
            {
                byte unk1 = reader.ReadByte(); // byte

                if (unk1 != 0x0F)
                    throw new InvalidDataException("unk1 != 0x0F");

                byte v1 = reader.ReadByte();
                byte v2 = reader.ReadByte();
                byte v3 = reader.ReadByte();
                chunkCount = v1 << 16 | v2 << 8 | v3 << 0; // 3-byte
            }

            if (chunkCount < 0)
                throw new InvalidDataException(String.Format("Possible error ({0}) at offset: 0x" + reader.BaseStream.Position.ToString("X"), chunkCount));

            BLTEChunk[] chunks = new BLTEChunk[chunkCount];

            for (int i = 0; i < chunkCount; ++i)
            {
                chunks[i] = new BLTEChunk();

                if (frameHeaderSize != 0)
                {
                    chunks[i].compSize = reader.ReadInt32BE();
                    chunks[i].decompSize = reader.ReadInt32BE();
                    chunks[i].hash = reader.ReadBytes(16);
                }
                else
                {
                    chunks[i].compSize = totalSize;
                    chunks[i].decompSize = totalSize; // -1?
                    chunks[i].hash = null;
                }
            }

            for (int i = 0; i < chunkCount; ++i)
            {
                chunks[i].data = reader.ReadBytes(chunks[i].compSize);

                if (frameHeaderSize != 0)
                {
                    byte[] hh = md5.ComputeHash(chunks[i].data);

                    if (!hh.EqualsTo(chunks[i].hash))
                        throw new InvalidDataException("MD5 missmatch!");
                }

                switch (chunks[i].data[0])
                {
                    //case 0x45: // E
                    //    break;
                    //case 0x46: // F
                    //    break;
                    case 0x4E: // N
                        {
                            if (chunks[i].data.Length - 1 != chunks[i].decompSize)
                                throw new InvalidDataException("Possible error (1) !");

                            stream.Write(chunks[i].data, 1, chunks[i].decompSize);
                        }
                        break;
                    case 0x5A: // Z
                        {
                            Decompress(stream, chunks[i].data);
                        }
                        break;
                    default:
                        throw new InvalidDataException("Unknown byte at switch case!");
                }
            }
        }

        private void Decompress(Stream outS, byte[] data)
        {
            byte[] buf = new byte[0x80000];

            using (var dStream = new DeflateStream(new MemoryStream(data, 3, data.Length - 3), CompressionMode.Decompress))
            {
                int len;
                while ((len = dStream.Read(buf, 0, buf.Length)) > 0)
                    outS.Write(buf, 0, len);
            }
        }
    }
}
