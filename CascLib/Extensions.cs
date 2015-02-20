using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CASCExplorer
{
    public static class Extensions
    {
        public static int ReadInt32BE(this BinaryReader reader)
        {
            return BitConverter.ToInt32(reader.ReadBytes(4).Reverse().ToArray(), 0);
        }

        public static uint ReadUInt32BE(this BinaryReader reader)
        {
            return BitConverter.ToUInt32(reader.ReadBytes(4).Reverse().ToArray(), 0);
        }

        public static T Read<T>(this BinaryReader reader) where T : struct
        {
            byte[] result = reader.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle handle = GCHandle.Alloc(result, GCHandleType.Pinned);
            T returnObject = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return returnObject;
        }

        public static List<T> Read<T>(this BinaryReader reader, bool fake) where T : struct
        {
            long numBytes = reader.ReadInt64();

            int itemCount = (int)numBytes / Marshal.SizeOf(typeof(T));

            List<T> data = new List<T>(itemCount);

            for (int i = 0; i < itemCount; ++i)
                data.Add(reader.Read<T>());

            reader.BaseStream.Position += (0 - (int)numBytes) & 0x07;

            return data;
        }

        public static short ReadInt16BE(this BinaryReader reader)
        {
            return BitConverter.ToInt16(reader.ReadBytes(2).Reverse().ToArray(), 0);
        }

        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", string.Empty);
        }

        public static bool EqualsTo(this byte[] hash, byte[] other)
        {
            if (hash.Length != other.Length)
                return false;
            for (var i = 0; i < hash.Length; ++i)
                if (hash[i] != other[i])
                    return false;
            return true;
        }

        public static bool EqualsToIgnoreLength(this byte[] array, byte[] other)
        {
            for (var i = 0; i < array.Length; ++i)
                if (array[i] != other[i])
                    return false;
            return true;
        }

        public static bool IsZeroed(this byte[] array)
        {
            for (var i = 0; i < array.Length; ++i)
                if (array[i] != 0)
                    return false;
            return true;
        }

        public static byte[] Copy(this byte[] array, int len)
        {
            byte[] ret = new byte[len];
            for (int i = 0; i < len; ++i)
                ret[i] = array[i];
            return ret;
        }

        public static string ToBinaryString(this BitArray bits)
        {
            if (bits.Count == 0)
                return String.Empty;

            string result = String.Empty;

            for (int i = 0; i < bits.Count; ++i)
            {
                result += bits[i] ? "1" : "0";
            }

            return result;
        }
    }

    public static class CStringExtensions
    {
        /// <summary> Reads the NULL terminated string from 
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        public static string ReadCString(this BinaryReader reader)
        {
            return reader.ReadCString(Encoding.UTF8);
        }

        /// <summary> Reads the NULL terminated string from 
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        public static string ReadCString(this BinaryReader reader, Encoding encoding)
        {
            try
            {
                var bytes = new List<byte>();
                byte b;
                while ((b = reader.ReadByte()) != 0)
                    bytes.Add(b);
                return encoding.GetString(bytes.ToArray());
            }
            catch (EndOfStreamException)
            {
                return String.Empty;
            }
        }

        public static void WriteCString(this BinaryWriter writer, string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            writer.Write(bytes);
            writer.Write((byte)0);
        }

        public static byte[] ToByteArray(this string str)
        {
            str = str.Replace(" ", String.Empty);

            var res = new byte[str.Length / 2];
            for (int i = 0; i < res.Length; ++i)
            {
                string temp = String.Concat(str[i * 2], str[i * 2 + 1]);
                res[i] = Convert.ToByte(temp, 16);
            }
            return res;
        }
    }
}
