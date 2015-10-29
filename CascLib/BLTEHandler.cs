using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CASCExplorer
{
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
            reader = new BinaryReader(stream, Encoding.ASCII, true);
            size = _size;
        }

        public void ExtractToFile(string path, string name)
        {
            string fullPath = Path.Combine(path, name);
            string dir = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fileStream = File.Open(fullPath, FileMode.Create))
            {
                CopyTo(fileStream);
            }
        }

        public MemoryStream OpenFile()
        {
            MemoryStream memStream = new MemoryStream();
            CopyTo(memStream);
            memStream.Position = 0;
            return memStream;
        }

        public void CopyTo(Stream stream)
        {
            int magic = reader.ReadInt32(); // BLTE (raw)

            if (magic != 0x45544c42)
                throw new InvalidDataException("BLTEHandler: magic");

            int frameHeaderSize = reader.ReadInt32BE();
            int chunkCount = 0;

            if (frameHeaderSize == 0)
            {
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
                BLTEChunk chunk = new BLTEChunk();

                if (frameHeaderSize != 0)
                {
                    chunk.CompSize = reader.ReadInt32BE();
                    chunk.DecompSize = reader.ReadInt32BE();
                    chunk.Hash = reader.ReadBytes(16);
                }
                else
                {
                    chunk.CompSize = size - 8;
                    chunk.DecompSize = size - 8 - 1;
                    chunk.Hash = null;
                }

                chunks[i] = chunk;
            }

            foreach (var chunk in chunks)
            {
                chunk.Data = reader.ReadBytes(chunk.CompSize);

                if (chunk.Data.Length != chunk.CompSize)
                    throw new Exception("chunks[i].data.Length != chunks[i].compSize");

                if (chunk.Hash != null)
                {
                    byte[] hh = md5.ComputeHash(chunk.Data);

                    if (!hh.EqualsTo(chunk.Hash))
                        throw new InvalidDataException("MD5 missmatch!");
                }

                switch (chunk.Data[0])
                {
                    case 0x45: // E (encrypted)
                        Decrypt(chunk.Data, stream);
                        break;
                    //case 0x46: // F (frame, recursive)
                    //    break;
                    case 0x4E: // N (not compressed)
                        if (chunk.Data.Length - 1 != chunk.DecompSize)
                            throw new InvalidDataException("Possible error (1) !");
                        stream.Write(chunk.Data, 1, chunk.DecompSize);
                        break;
                    case 0x5A: // Z (zlib compressed)
                        Decompress(chunk.Data, stream);
                        break;
                    default:
                        throw new InvalidDataException("Unknown BLTE chunk type!");
                }
            }
        }

        static Dictionary<ulong, byte[]> keys = new Dictionary<ulong, byte[]>()
        {
            [0X402CD9D8D6BFED98] = new byte[] { 0xAE, 0xB0, 0xEA, 0xDE, 0xA4, 0x76, 0x12, 0xFE, 0x6C, 0x04, 0x1A, 0x03, 0x95, 0x8D, 0xF2, 0x41 },
            [0xFB680CB6A8BF81F3] = new byte[] { 0x62, 0xD9, 0x0E, 0xFA, 0x7F, 0x36, 0xD7, 0x1C, 0x39, 0x8A, 0xE2, 0xF1, 0xFE, 0x37, 0xBD, 0xB9 },
        };

        private static void Decrypt(byte[] data, Stream outS)
        {
            byte keySeedSize = data[1];

            if (keySeedSize == 0)
                return;

            byte[] keySeed = data.Skip(2).Take(keySeedSize).ToArray();

            ulong keyName = BitConverter.ToUInt64(keySeed, 0);

            byte IVSeedSize = data[keySeedSize + 2];

            if (IVSeedSize > 0x10)
                return;

            byte[] IVSeed = data.Skip(keySeedSize + 3).Take(IVSeedSize).ToArray();

            if (data.Length < IVSeedSize + keySeedSize + 4)
                return;

            int dataOffset = keySeedSize + IVSeedSize + 3;

            byte encType = data[dataOffset];

            if (encType != 0x53 && encType != 0x41) // 'S' or 'A'
                return;

            if (encType == 'A')
                throw new Exception("enc type A not yet done");

            byte[] IV = new byte[8];

            for (int i = 0; i < IVSeed.Length; i++)
                IV[i] = IVSeed[i];

            //int someValue = 0; // unknown value (ulong on x64)

            //for (int i = 0, j = 0; i < 32; i += 8, j++) // that loop is 32 bit on x86 and 64 bit on x64 - wtf Blizzard?
            //{
            //    IV[j] ^= (byte)(someValue >> i);
            //}

            byte[] key;

            if (!keys.TryGetValue(keyName, out key))
            {
                string msg = string.Format("Unknown keyname {0:X16}", keyName);
                //Logger.WriteLine(msg);
                throw new Exception(msg);
                //key = new byte[16];
            }

            Salsa20 salsa = new Salsa20();
            var decryptor = salsa.CreateDecryptor(key, IV);

            var data2 = decryptor.TransformFinalBlock(data, dataOffset + 1, data.Length - (dataOffset + 1));

            outS.Write(data2, 0, data2.Length);

            // for now just store them as is
            //outS.Write(data, 0, data.Length);
        }

        private static void Decompress(byte[] data, Stream outS)
        {
            // skip first 3 bytes (zlib)
            using (var ms = new MemoryStream(data, 3, data.Length - 3))
            using (var dStream = new DeflateStream(ms, CompressionMode.Decompress))
            {
                dStream.CopyTo(outS);
            }
        }

        public void Dispose()
        {
            reader.Close();
        }
    }
}
