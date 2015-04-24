using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace CASCExplorer
{
    public unsafe class MMStream : IDisposable
    {
        private MemoryMappedFile file;

        public MemoryMappedViewAccessor Accessor { get; private set; }
        public long Position { get; set; }
        public long Length { get; private set; }

        private byte* UnsafePointer;

        public MMStream(string path)
        {
            file = MemoryMappedFile.CreateFromFile(path);

            Length = new FileInfo(path).Length;

            Accessor = file.CreateViewAccessor();

            Accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref UnsafePointer);
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (Accessor != null)
            {
                Accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                Accessor.Dispose();
            }

            if (file != null)
                file.Dispose();
        }

        public T Read<T>() where T : struct
        {
            T val = (T)Marshal.PtrToStructure((IntPtr)(UnsafePointer + Position), typeof(T));
            Position += Marshal.SizeOf(val);
            return val;
        }

        public List<T> Read<T>(bool fake) where T : struct
        {
            long numBytes = ReadInt64();

            int itemCount = (int)numBytes / Marshal.SizeOf(typeof(T));

            List<T> data = new List<T>(itemCount);

            for (int i = 0; i < itemCount; ++i)
                data.Add(Read<T>());

            Position += (0 - (int)numBytes) & 0x07;

            return data;
        }

        public byte ReadByte()
        {
            byte val = *(UnsafePointer + Position);
            Position += 1;
            return val;
        }

        public short ReadInt16BE()
        {
            short val = 0;
            val |= (short)(*(UnsafePointer + Position + 1) << 0);
            val |= (short)(*(UnsafePointer + Position + 0) << 8);
            Position += 2;
            return val;
        }

        public short ReadInt16()
        {
            short val = *(short*)(UnsafePointer + Position);
            Position += 2;
            return val;
        }

        public ushort ReadUInt16()
        {
            ushort val = *(ushort*)(UnsafePointer + Position);
            Position += 2;
            return val;
        }

        public int ReadInt32BE()
        {
            int val = 0;
            val |= (*(UnsafePointer + Position + 3) << 0);
            val |= (*(UnsafePointer + Position + 2) << 8);
            val |= (*(UnsafePointer + Position + 1) << 16);
            val |= (*(UnsafePointer + Position + 0) << 24);
            Position += 4;
            return val;
        }

        public int ReadInt32()
        {
            int val = *(int*)(UnsafePointer + Position);
            Position += 4;
            return val;
        }

        public uint ReadUInt32()
        {
            uint val = *(uint*)(UnsafePointer + Position);
            Position += 4;
            return val;
        }

        public long ReadInt64()
        {
            long val = *(long*)(UnsafePointer + Position);
            Position += 8;
            return val;
        }

        public ulong ReadUInt64()
        {
            ulong val = *(ulong*)(UnsafePointer + Position);
            Position += 8;
            return val;
        }

        public byte[] ReadBytes(int count)
        {
            byte[] array = new byte[count];
            Marshal.Copy((IntPtr)(UnsafePointer + Position), array, 0, array.Length);
            Position += count;
            return array;
        }

        public string ReadCString()
        {
            int len = 0;

            while (*(UnsafePointer + Position + len) != 0)
                len++;

            string val = new string((sbyte*)(UnsafePointer + Position), 0, len, Encoding.UTF8);
            Position += len + 1;
            return val;
        }

        public byte PeekByte()
        {
            return *(UnsafePointer + Position);
        }

        public void Skip(int count)
        {
            Position += count;
        }
    }
}
