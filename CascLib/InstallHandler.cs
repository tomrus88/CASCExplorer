using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class InstallEntry
    {
        public string Name;
        public byte[] MD5;
        public int Size;
    }

    public class InstallMask
    {
        public string Name;
        public short Type;
        public BitArray Array;
    }

    public class InstallHandler
    {
        private readonly List<InstallEntry> InstallData = new List<InstallEntry>();
        private readonly Dictionary<string, InstallMask> Mask = new Dictionary<string, InstallMask>();

        public int Count
        {
            get { return InstallData.Count; }
        }

        public InstallHandler(Stream stream, AsyncAction worker)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"install\"...");
            }

            using (var br = new BinaryReader(stream))
            {
                br.ReadBytes(2); // IN

                byte b1 = br.ReadByte();
                byte b2 = br.ReadByte();
                short numMasks = br.ReadInt16BE();
                int numFiles = br.ReadInt32BE();

                for (int i = 0; i < numMasks; ++i)
                {
                    InstallMask mask = new InstallMask();
                    mask.Name = br.ReadCString();
                    mask.Type = br.ReadInt16BE();

                    //int[] bits = new int[7];
                    //for (int j = 0; j < bits.Length; ++j)
                    //{
                    //    bits[j] = br.ReadInt32BE();
                    //}
                    //mask.Array = new BitArray(bits.Reverse().ToArray());
                    mask.Array = new BitArray(br.ReadBytes(28).Reverse().ToArray());

                    Mask.Add(mask.Name, mask);
                }

                for (int i = 0; i < numFiles; ++i)
                {
                    InstallEntry entry = new InstallEntry();
                    entry.Name = br.ReadCString();
                    entry.MD5 = br.ReadBytes(16);
                    entry.Size = br.ReadInt32BE();

                    InstallData.Add(entry);

                    if (worker != null)
                    {
                        worker.ThrowOnCancel();
                        worker.ReportProgress((int)((float)i / (float)(numFiles - 1) * 100));
                    }
                }
            }
        }

        public InstallEntry GetEntry(string name)
        {
            return InstallData.Where(i => i.Name == name).FirstOrDefault();
        }

        public void Print()
        {
            foreach (var type in Mask)
            {
                Logger.WriteLine("Install Files for {0}:", type.Key);

                var bits = Mask[type.Key].Array;

                Logger.WriteLine("Bits: {0}", bits.ToBinaryString());

                for (int i = 0; i < bits.Count; i++)
                {
                    if (bits[i] && i < InstallData.Count)
                    {
                        Logger.WriteLine(InstallData[i].Name);
                    }
                }
            }
        }

        public void Clear()
        {
            InstallData.Clear();
            Mask.Clear();
        }
    }
}
