using System;
using System.Collections.Generic;
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
        public int CompSize;
        public int DecompSize;
        public byte[] Hash;
        public byte[] Data;
    }

    class BLTEHandler : IDisposable
    {
        BinaryReader reader;
        MD5 md5 = MD5.Create();
        int size;

        public BLTEHandler(Stream stream, int _size)
        {
            this.reader = new BinaryReader(stream, Encoding.ASCII);
            this.size = _size;
        }

        public void ExtractToFile(string path, string name)
        {
            var fullPath = Path.Combine(path, name);
            var dir = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fileStream = File.Open(fullPath, FileMode.Create))
            {
                ExtractData(fileStream);
            }
        }

        public MemoryStream OpenFile()
        {
            MemoryStream memStream = new MemoryStream();
            ExtractData(memStream);
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
                totalSize = size - 8;// - 38;

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

            var chunks = new List<BLTEChunk>(chunkCount);

            for (int i = 0; i < chunkCount; ++i)
            {
                var chunk = new BLTEChunk();
                if (frameHeaderSize != 0)
                {
                    chunk.CompSize = reader.ReadInt32BE();
                    chunk.DecompSize = reader.ReadInt32BE();
                    chunk.Hash = reader.ReadBytes(16);
                }
                else
                {
                    chunk.CompSize = totalSize;
                    chunk.DecompSize = totalSize - 1;
                    chunk.Hash = null;
                }
                chunks.Add(chunk);
            }

            foreach (var chunk in chunks)
            {
                chunk.Data = reader.ReadBytes(chunk.CompSize);

                if (chunk.Data.Length != chunk.CompSize)
                    throw new Exception("chunks[i].data.Length != chunks[i].compSize");

                if (frameHeaderSize != 0)
                {
                    byte[] hh = md5.ComputeHash(chunk.Data);

                    if (!hh.EqualsTo(chunk.Hash))
                        throw new InvalidDataException("MD5 missmatch!");
                }

                switch (chunk.Data[0])
                {
                    //case 0x45: // E
                    //    break;
                    //case 0x46: // F
                    //    break;
                    case 0x4E: // N
                        if (chunk.Data.Length - 1 != chunk.DecompSize)
                            throw new InvalidDataException("Possible error (1) !");
                        stream.Write(chunk.Data, 1, chunk.DecompSize);
                        break;
                    case 0x5A: // Z
                        Decompress(stream, chunk.Data);
                        break;
                    default:
                        throw new InvalidDataException("Unknown byte at switch case!");
                }
            }
        }

        private static void Decompress(Stream outS, byte[] data)
        {
            using (var ms = new MemoryStream(data, 3, data.Length - 3))
            using (var dStream = new DeflateStream(ms, CompressionMode.Decompress))
            {
                dStream.CopyTo(outS, 0x80000);
            }
        }

        public void Dispose()
        {
            reader.Close();
        }
    }
}
