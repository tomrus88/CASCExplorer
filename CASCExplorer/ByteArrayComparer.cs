using System.Collections.Generic;

namespace CASCExplorer
{
    class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        const uint FnvPrime32 = 16777619;
        const uint FnvOffset32 = 2166136261;

        public bool Equals(byte[] x, byte[] y)
        {
            if (x.Length != y.Length)
                return false;

            for (int i = 0; i < x.Length; ++i)
                if (x[i] != y[i])
                    return false;

            return true;
        }

        public int GetHashCode(byte[] obj)
        {
            return To32BitFnv1aHash(obj);
        }

        private int To32BitFnv1aHash(byte[] toHash)
        {
            uint hash = FnvOffset32;

            foreach (var chunk in toHash)
            {
                hash ^= chunk;
                hash *= FnvPrime32;
            }

            return unchecked((int)hash);
        }
    }
}
