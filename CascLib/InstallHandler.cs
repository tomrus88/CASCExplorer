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

        public List<InstallTag> Tags = new List<InstallTag>();
    }

    public class InstallTag
    {
        public string Name;
        public short Type;
        public BitArray Bits;
    }

    public class InstallHandler
    {
        private readonly List<InstallEntry> InstallData = new List<InstallEntry>();
        private static readonly Jenkins96 Hasher = new Jenkins96();

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

                int numMaskBytes = numFiles / 8 + (numFiles % 8 > 0 ? 1 : 0);

                List<InstallTag> Tags = new List<InstallTag>();

                for (int i = 0; i < numMasks; ++i)
                {
                    InstallTag mask = new InstallTag();
                    mask.Name = br.ReadCString();
                    mask.Type = br.ReadInt16BE();

                    byte[] bits = br.ReadBytes(numMaskBytes);

                    for (int j = 0; j < numMaskBytes;++j)
                        bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                    mask.Bits = new BitArray(bits);

                    Tags.Add(mask);
                }

                for (int i = 0; i < numFiles; ++i)
                {
                    InstallEntry entry = new InstallEntry();
                    entry.Name = br.ReadCString();
                    entry.MD5 = br.ReadBytes(16);
                    entry.Size = br.ReadInt32BE();

                    InstallData.Add(entry);

                    foreach (InstallTag tag in Tags)
                        if (tag.Bits[i])
                            entry.Tags.Add(tag);

                    if (worker != null)
                    {
                        worker.ThrowOnCancel();
                        worker.ReportProgress((int)((float)i / (float)(numFiles - 1) * 100));
                    }
                }
            }

            //Print();
        }

        public InstallEntry GetEntry(string name)
        {
            return InstallData.Where(i => i.Name == name).FirstOrDefault();
        }

        public IEnumerable<InstallEntry> GetEntries(string tag)
        {
            foreach (var entry in InstallData)
                if (entry.Tags.Any(t => t.Name == tag))
                    yield return entry;
        }

        public IEnumerable<InstallEntry> GetEntries(ulong hash)
        {
            foreach (var entry in InstallData)
                if (Hasher.ComputeHash(entry.Name) == hash)
                    yield return entry;
        }

        public IEnumerable<InstallEntry> GetEntries()
        {
            foreach (var entry in InstallData)
                yield return entry;
        }

        public void Print()
        {
            for (int i = 0; i < InstallData.Count; ++i)
            {
                var data = InstallData[i];

                Logger.WriteLine("{0:D4}: {1} {2}", i, data.MD5.ToHexString(), data.Name);

                Logger.WriteLine("    {0}", string.Join(",", data.Tags.Select(t => t.Name)));
            }
        }

        public void MergeData(CASCFolder folder)
        {
            foreach (var entry in InstallData)
            {
                CreateSubTree(folder, Hasher.ComputeHash(entry.Name), entry.Name);
            }
        }

        private static void CreateSubTree(CASCFolder root, ulong filehash, string file)
        {
            string[] parts = file.Split('\\');

            CASCFolder folder = root;

            for (int i = 0; i < parts.Length; ++i)
            {
                bool isFile = (i == parts.Length - 1);

                ulong hash = isFile ? filehash : Hasher.ComputeHash(parts[i]);

                ICASCEntry entry = folder.GetEntry(hash);

                if (entry == null)
                {
                    if (isFile)
                    {
                        entry = new CASCFile(hash);
                        CASCFile.FileNames[hash] = file;
                    }
                    else
                    {
                        entry = new CASCFolder(hash);
                        CASCFolder.FolderNames[hash] = parts[i];
                    }

                    folder.SubEntries[hash] = entry;
                }

                folder = entry as CASCFolder;
            }
        }

        public void Clear()
        {
            InstallData.Clear();
        }
    }
}
