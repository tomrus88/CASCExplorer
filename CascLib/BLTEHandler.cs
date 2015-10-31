using System;
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

            bool extractOk = true;

            using (var fileStream = File.Open(fullPath, FileMode.Create))
            {
                try
                {
                    CopyTo(fileStream);
                }
                catch (Exception exc)
                {
                    extractOk = false;

                    Logger.WriteLine("Unable to extract file {0}: {1}", name, exc.Message);
                }
            }

            if (!extractOk)
                File.Delete(fullPath);
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
                throw new InvalidDataException("BLTEHandler: invalid magic");

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
                throw new InvalidDataException(string.Format("Possible error ({0}) at offset: 0x" + reader.BaseStream.Position.ToString("X"), chunkCount));

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

            for (int i = 0; i < chunks.Length; i++)
            {
                BLTEChunk chunk = chunks[i];

                chunk.Data = reader.ReadBytes(chunk.CompSize);

                if (chunk.Hash != null)
                {
                    byte[] hh = md5.ComputeHash(chunk.Data);

                    if (!hh.EqualsTo(chunk.Hash))
                        throw new InvalidDataException("MD5 missmatch!");
                }

                HandleChunk(chunk.Data, i, stream);
            }
        }

        private void HandleChunk(byte[] data, long index, Stream outStream)
        {
            // I really hope they don't put encrypted chunk into encrypted chunk
            switch (data[0])
            {
                case 0x45: // E (encrypted)
                    byte[] decrypted = Decrypt(data, index);
                    HandleChunk(decrypted, index, outStream);
                    break;
                case 0x46: // F (frame, recursive)
                    throw new Exception("DecoderFrame: implement me!");
                case 0x4E: // N (not compressed)
                    outStream.Write(data, 1, data.Length - 1);
                    break;
                case 0x5A: // Z (zlib compressed)
                    Decompress(data, outStream);
                    break;
                default:
                    throw new InvalidDataException(string.Format("Unknown BLTE chunk type {0} (0x{0:X2})!", (char)data[0], data[0]));
            }
        }

        private static byte[] Decrypt(byte[] data, long index)
        {
            byte keyNameSize = data[1];

            if (keyNameSize == 0 || keyNameSize != 8)
                throw new Exception("keyNameSize == 0 || keyNameSize != 8");

            byte[] keyNameBytes = data.Skip(2).Take(keyNameSize).ToArray();

            ulong keyName = BitConverter.ToUInt64(keyNameBytes, 0);

            byte IVSize = data[keyNameSize + 2];

            if (IVSize != 4 || IVSize > 0x10)
                throw new Exception("IVSize != 4 || IVSize > 0x10");

            byte[] IVpart = data.Skip(keyNameSize + 3).Take(IVSize).ToArray();

            if (data.Length < IVSize + keyNameSize + 4)
                throw new Exception("data.Length < IVSize + keyNameSize + 4");

            int dataOffset = keyNameSize + IVSize + 3;

            byte encType = data[dataOffset];

            if (encType != 0x53 && encType != 0x41) // 'S' or 'A'
                throw new Exception("encType != 0x53 && encType != 0x41");

            dataOffset++;

            // expand to 8 bytes
            byte[] IV = new byte[8];

            Array.Copy(IVpart, IV, IVpart.Length);

            // magic
            for (int bits = 0, i = 0; bits < 64; bits += 8, i++) // that loop is 32 on x86 and 64 on x64 - wtf Blizzard?
            {
                IV[i] ^= (byte)((index >> bits) & 0xFF);
            }

            byte[] key = KeyService.GetKey(keyName);

            if (key == null)
            {
                string msg = string.Format("Unknown keyname {0:X16}", keyName);
                //Logger.WriteLine(msg);
                throw new Exception(msg);
            }

            if (encType == 0x53)
            {
                Salsa20 salsa = new Salsa20();

                ICryptoTransform decryptor = salsa.CreateDecryptor(key, IV);

                byte[] dataOut = decryptor.TransformFinalBlock(data, dataOffset, data.Length - dataOffset);

                return dataOut;
            }
            else
            {
                // ARC4 ?
                throw new Exception("enc type A not yet done");
            }
        }

        private static void Decompress(byte[] data, Stream outStream)
        {
            // skip first 3 bytes (zlib)
            using (var ms = new MemoryStream(data, 3, data.Length - 3))
            using (var dStream = new DeflateStream(ms, CompressionMode.Decompress))
            {
                dStream.CopyTo(outStream);
            }
        }

        public void Dispose()
        {
            reader.Close();
        }
    }
}
