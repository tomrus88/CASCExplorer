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
                    case 0x46: // F (frame, recursive)
                        throw new Exception("DecoderFrame: implement me!");
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
            // hardcoded keys
            [0x402CD9D8D6BFED98] = "AEB0EADEA47612FE6C041A03958DF241".ToByteArray(),
            [0xFB680CB6A8BF81F3] = "62D90EFA7F36D71C398AE2F1FE37BDB9".ToByteArray(),
            // streamed keys
            [0xDBD3371554F60306] = "34E397ACE6DD30EEFDC98A2AB093CD3C".ToByteArray(),
            [0x11A9203C9881710A] = "2E2CB8C397C2F24ED0B5E452F18DC267".ToByteArray(),
            [0xA19C4F859F6EFA54] = "0196CB6F5ECBAD7CB5283891B9712B4B".ToByteArray(),
            [0x87AEBBC9C4E6B601] = "685E86C6063DFDA6C9E85298076B3D42".ToByteArray(),
            [0xDEE3A0521EFF6F03] = "AD740CE3FFFF9231468126985708E1B9".ToByteArray(),
            [0x8C9106108AA84F07] = "53D859DDA2635A38DC32E72B11B32F29".ToByteArray(),
            //[0x49166D358A34D815] = "00000000000000000000000000000000".ToByteArray(),
        };

        private static void Decrypt(byte[] data, Stream outS)
        {
            byte keyNameSize = data[1];

            if (keyNameSize == 0 || keyNameSize != 8)
                throw new Exception("keyNameSize == 0 || keyNameSize != 8");

            byte[] keyNameBytes = data.Skip(2).Take(keyNameSize).ToArray();

            ulong keyName = BitConverter.ToUInt64(keyNameBytes, 0);

            byte IVSize = data[keyNameSize + 2];

            if (IVSize > 0x10)
                throw new Exception("IVSize > 0x10");

            byte[] IVpart = data.Skip(keyNameSize + 3).Take(IVSize).ToArray();

            if (data.Length < IVSize + keyNameSize + 4)
                throw new Exception("data.Length < IVSize + keyNameSize + 4");

            int dataOffset = keyNameSize + IVSize + 3;

            byte encType = data[dataOffset];

            if (encType != 0x53 && encType != 0x41) // 'S' or 'A'
                throw new Exception("encType != 0x53 && encType != 0x41");

            // expand to 8 bytes
            byte[] IV = new byte[8];

            for (int i = 0; i < IVpart.Length; i++)
                IV[i] = IVpart[i];

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
            }

            if (encType == 0x53)
            {
                Salsa20 salsa = new Salsa20();
                var decryptor = salsa.CreateDecryptor(key, IV);

                var dataOut = decryptor.TransformFinalBlock(data, dataOffset + 1, data.Length - (dataOffset + 1));
                outS.Write(dataOut, 0, dataOut.Length);
            }
            else
            {
                // ARC4 ?
                throw new Exception("enc type A not yet done");
            }
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
