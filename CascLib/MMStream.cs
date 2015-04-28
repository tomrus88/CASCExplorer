using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace CASCExplorer
{
    public unsafe class MMStream : Stream
    {
        private MemoryMappedFile _file;
        private long _length;
        private MemoryMappedViewAccessor _accessor;
        private IntPtr BufferPointer;
        private byte* UnsafePointer;

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get; set;
        }

        public MMStream(string path)
        {
            _file = MemoryMappedFile.CreateFromFile(path);
            _length = new FileInfo(path).Length;

            _accessor = _file.CreateViewAccessor();

            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref UnsafePointer);
        }

        public MMStream(Stream other)
        {
            if (other is MemoryStream)
            {
                _length = other.Length;

                byte[] buffer = ((MemoryStream)other).GetBuffer();

                BufferPointer = Marshal.AllocHGlobal((int)_length);

                Marshal.Copy(buffer, 0, BufferPointer, (int)_length);

                UnsafePointer = (byte*)BufferPointer;
            }
            else
                throw new NotSupportedException("Can't create MMStream from " + other.GetType().Name);
        }

        public override void Close()
        {
            Dispose();
        }

        public new void Dispose()
        {
            if (_accessor != null)
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _accessor.Dispose();
            }

            if (_file != null)
                _file.Dispose();

            if (BufferPointer != IntPtr.Zero)
                Marshal.FreeHGlobal(BufferPointer);
        }

        public T Read<T>() where T : struct
        {
            T val = (T)Marshal.PtrToStructure((IntPtr)(UnsafePointer + Position), typeof(T));
            Position += Marshal.SizeOf(val);
            return val;
        }

        public T[] ReadArray<T>() where T : struct
        {
            long numBytes = ReadInt64();

            int itemCount = (int)numBytes / Marshal.SizeOf(typeof(T));

            T[] data = new T[itemCount];

            for (int i = 0; i < itemCount; ++i)
                data[i] = Read<T>();

            Position += (0 - (int)numBytes) & 0x07;

            return data;
        }

        public new byte ReadByte()
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

        public override void Flush()
        {

        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
                Position = offset; // offset should be positive
            else if (origin == SeekOrigin.Current)
                Position += offset; // offset can be both positive and negative
            else
                Position = _length + offset; // offset should be negative

            return Position;
        }

        public override void SetLength(long value)
        {
            _length = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int available = count;

            if (Position + available > Length)
                available = (int)(Length - Position);

            Marshal.Copy((IntPtr)(UnsafePointer + Position), buffer, offset, available);
            Position += available;
            return available;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {

        }
    }
}
