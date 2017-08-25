using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace CASCExplorer
{
    public static class FastStruct<T> where T : struct
    {
        private delegate T PtrToStructureDelegateByteRef(ref byte source);
        private delegate void CopyMemoryDelegate(ref T dest, ref byte src, int count);

        private readonly static PtrToStructureDelegateByteRef PtrToStructureByteRef = BuildLoadFromByteRefMethod();
        private readonly static CopyMemoryDelegate CopyMemory = BuildCopyMemoryMethod();

        private static DynamicMethod methodLoadByteRef;
        private static DynamicMethod methodCopyMemory;

        public static readonly int Size = Marshal.SizeOf<T>();

        private static PtrToStructureDelegateByteRef BuildLoadFromByteRefMethod()
        {
            methodLoadByteRef = new DynamicMethod("PtrToStructureByteRef<" + typeof(T).FullName + ">",
                typeof(T), new[] { typeof(byte).MakeByRefType() }, typeof(FastStruct<T>));

            ILGenerator generator = methodLoadByteRef.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (PtrToStructureDelegateByteRef)methodLoadByteRef.CreateDelegate(typeof(PtrToStructureDelegateByteRef));
        }

        private static CopyMemoryDelegate BuildCopyMemoryMethod()
        {
            methodCopyMemory = new DynamicMethod("CopyMemory<" + typeof(T).FullName + ">",
                typeof(void), new[] { typeof(T).MakeByRefType(), typeof(byte).MakeByRefType(), typeof(int) }, typeof(FastStruct<T>));

            ILGenerator generator = methodCopyMemory.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Cpblk);
            generator.Emit(OpCodes.Ret);

            return (CopyMemoryDelegate)methodCopyMemory.CreateDelegate(typeof(CopyMemoryDelegate));
        }

        public static T ArrayToStructure(byte[] src)
        {
            return PtrToStructureByteRef(ref src[0]);
        }

        public static T[] ReadArray(byte[] source)
        {
            uint elementSize = (uint)Size;

            T[] buffer = new T[source.Length / elementSize];

            if (source.Length > 0)
                CopyMemory(ref buffer[0], ref source[0], source.Length);

            return buffer;
        }
    }
}
