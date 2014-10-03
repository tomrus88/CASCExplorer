using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    [Flags]
    public enum LocaleFlags
    {
        All = -1,
        None = 0,
        //Unk_1 = 0x1,
        enUS = 0x2,
        koKR = 0x4,
        //Unk_8 = 0x8,
        frFR = 0x10,
        deDE = 0x20,
        zhCN = 0x40,
        esES = 0x80,
        zhTW = 0x100,
        enGB = 0x200,
        enCN = 0x400,
        enTW = 0x800,
        esMX = 0x1000,
        ruRU = 0x2000,
        ptBR = 0x4000,
        itIT = 0x8000,
        ptPT = 0x10000,
        All_WoW = enUS | koKR | frFR | deDE | zhCN | esES | zhTW | enGB | esMX | ruRU | ptBR | itIT | ptPT
    }

    [Flags]
    public enum ContentFlags : uint
    {
        None = 0,
        LowViolence = 0x80, // many models have this flag
        NoCompression = 0x80000000 // sounds have this flag
    }

    public class RootBlock
    {
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
    }

    public class RootEntry
    {
        public RootBlock Block;
        public int Unk1;
        public byte[] MD5;
        public ulong Hash;

        public override string ToString()
        {
            return String.Format("RootBlock: {0:X8} {1:X8}, File: {2:X8} {3}", Block.ContentFlags, Block.LocaleFlags, Unk1, MD5.ToHexString());
        }
    }

    public class WowRootHandler
    {
        public readonly MultiDictionary<ulong, RootEntry> RootData = new MultiDictionary<ulong, RootEntry>();
        public readonly HashSet<ulong> UnknownFiles = new HashSet<ulong>();
        private static readonly Jenkins96 Hasher = new Jenkins96();

        public int Count { get { return RootData.Count; } }
        public int CountTotal { get { return RootData.Sum(re => re.Value.Count); } }
        public int CountSelect { get; private set; }
        public int CountUnknown { get; private set; }

        public WowRootHandler(Stream stream, AsyncAction worker)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"root\"...");
            }

            using (var br = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    int count = br.ReadInt32();

                    RootBlock block = new RootBlock();
                    block.ContentFlags = (ContentFlags)br.ReadUInt32();
                    block.LocaleFlags = (LocaleFlags)br.ReadUInt32();

                    if (block.LocaleFlags == LocaleFlags.None)
                        throw new Exception("block.LocaleFlags == LocaleFlags.None");

                    if (block.ContentFlags != ContentFlags.None && (block.ContentFlags & (ContentFlags.LowViolence | ContentFlags.NoCompression)) == 0)
                        throw new Exception("block.ContentFlags != ContentFlags.None");

                    RootEntry[] entries = new RootEntry[count];

                    for (var i = 0; i < count; ++i)
                    {
                        entries[i] = new RootEntry();
                        entries[i].Block = block;
                        entries[i].Unk1 = br.ReadInt32();
                    }

                    for (var i = 0; i < count; ++i)
                    {
                        entries[i].MD5 = br.ReadBytes(16);

                        ulong hash = br.ReadUInt64();
                        entries[i].Hash = hash;

                        RootData.Add(hash, entries[i]);
                    }

                    if (worker != null)
                    {
                        worker.ThrowOnCancel();
                        worker.ReportProgress((int)((float)stream.Position / (float)stream.Length * 100));
                    }
                }
            }
        }

        public HashSet<RootEntry> GetRootInfo(ulong hash)
        {
            HashSet<RootEntry> result;
            RootData.TryGetValue(hash, out result);
            return result;
        }

        public void LoadListFile(string path, AsyncAction worker = null)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"listfile\"...");
            }

            if (!File.Exists(path))
                throw new FileNotFoundException("list file missing!");

            using (var sr = new StreamReader(path))
            {
                string file;

                while ((file = sr.ReadLine()) != null)
                {
                    ulong fileHash = Hasher.ComputeHash(file);

                    // skip invalid names
                    if (!RootData.ContainsKey(fileHash))
                    {
                        Logger.WriteLine("Invalid file name: {0}", file);
                        continue;
                    }

                    CASCFile.FileNames[fileHash] = file;

                    if (worker != null)
                    {
                        worker.ThrowOnCancel();
                        worker.ReportProgress((int)((float)sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }
                }

                Logger.WriteLine("CASCHandler: loaded {0} valid file names", CASCFile.FileNames.Count);
            }
        }

        public CASCFolder CreateStorageTree(LocaleFlags locale)
        {
            var rootHash = Hasher.ComputeHash("root");

            var root = new CASCFolder(rootHash);

            CASCFolder.FolderNames[rootHash] = "root";

            CountSelect = 0;

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            foreach (var unkFile in UnknownFiles)
                CASCFile.FileNames.Remove(unkFile);

            //Stream sw = new FileStream("unknownHashes.dat", FileMode.Create);
            //BinaryWriter bw = new BinaryWriter(sw);

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                if (!rootEntry.Value.Any(re => (re.Block.LocaleFlags & locale) != 0))
                    continue;

                string file;

                if (!CASCFile.FileNames.TryGetValue(rootEntry.Key, out file))
                {
                    file = "unknown\\" + rootEntry.Key.ToString("X16");
                    CountUnknown++;
                    UnknownFiles.Add(rootEntry.Key);
                    //Console.WriteLine("{0:X16}", BitConverter.ToUInt64(BitConverter.GetBytes(rootEntry.Key).Reverse().ToArray(), 0));
                    //bw.Write(rootEntry.Key);
                }

                CreateSubTree(root, rootEntry.Key, file);
                CountSelect++;
            }

            //bw.Flush();
            //bw.Close();

            Logger.WriteLine("CASCHandler: {0} file names missing", CountUnknown);

            return root;
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
    }
}
