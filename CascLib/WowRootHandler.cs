using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    [Flags]
    public enum LocaleFlags : uint
    {
        All = 0xFFFFFFFF,
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
        enSG = 0x20000000, // custom
        plPL = 0x40000000, // custom
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
        public static readonly RootBlock Empty = new RootBlock() { ContentFlags = ContentFlags.None, LocaleFlags = LocaleFlags.All };
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
    }

    public class RootEntry
    {
        public RootBlock Block;
        public int FileDataId;
        public byte[] MD5;

        public override string ToString()
        {
            return string.Format("RootBlock: {0:X8} {1:X8}, File: {2:X8} {3}", Block.ContentFlags, Block.LocaleFlags, FileDataId, MD5.ToHexString());
        }
    }

    public class WowRootHandler : RootHandlerBase
    {
        private readonly MultiDictionary<ulong, RootEntry> RootData = new MultiDictionary<ulong, RootEntry>();
        private readonly Dictionary<int, ulong> FileDataStore = new Dictionary<int, ulong>();
        private readonly Dictionary<ulong, int> FileDataStoreReverse = new Dictionary<ulong, int>();
        private readonly HashSet<ulong> UnknownFiles = new HashSet<ulong>();

        public override int Count { get { return RootData.Count; } }
        public override int CountTotal { get { return RootData.Sum(re => re.Value.Count); } }

        public WowRootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            while (stream.BaseStream.Position < stream.BaseStream.Length)
            {
                int count = stream.ReadInt32();

                RootBlock block = new RootBlock();
                block.ContentFlags = (ContentFlags)stream.ReadUInt32();
                block.LocaleFlags = (LocaleFlags)stream.ReadUInt32();

                if (block.LocaleFlags == LocaleFlags.None)
                    throw new Exception("block.LocaleFlags == LocaleFlags.None");

                if (block.ContentFlags != ContentFlags.None && (block.ContentFlags & (ContentFlags.LowViolence | ContentFlags.NoCompression)) == 0)
                    throw new Exception("block.ContentFlags != ContentFlags.None");

                RootEntry[] entries = new RootEntry[count];

                int fileDataIndex = 0;

                for (var i = 0; i < count; ++i)
                {
                    entries[i] = new RootEntry();
                    entries[i].Block = block;
                    entries[i].FileDataId = fileDataIndex + stream.ReadInt32();

                    fileDataIndex = entries[i].FileDataId + 1;
                }

                //Console.WriteLine("Block: {0} {1} (size {2})", block.ContentFlags, block.LocaleFlags, count);

                for (var i = 0; i < count; ++i)
                {
                    entries[i].MD5 = stream.ReadBytes(16);

                    ulong hash = stream.ReadUInt64();

                    RootData.Add(hash, entries[i]);

                    //Console.WriteLine("File: {0:X8} {1:X16} {2}", entries[i].FileDataId, hash, entries[i].MD5.ToHexString());

                    ulong hash2;

                    int fileDataId = entries[i].FileDataId;

                    if (FileDataStore.TryGetValue(fileDataId, out hash2))
                    {
                        if (hash2 == hash)
                        {
                            // duplicate, skipping
                            continue;
                        }
                        else
                        {
                            Logger.WriteLine("ERROR: got miltiple hashes for filedataid {0}", fileDataId);
                            continue;
                        }
                    }

                    FileDataStore.Add(fileDataId, hash);
                    FileDataStoreReverse.Add(hash, fileDataId);
                }

                worker?.ReportProgress((int)(stream.BaseStream.Position / (float)stream.BaseStream.Length * 100));
            }
        }

        public IEnumerable<RootEntry> GetAllEntriesByFileDataId(int fileDataId)
        {
            return GetAllEntries(GetHashByFileDataId(fileDataId));
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            foreach (var set in RootData)
                foreach (var entry in set.Value)
                    yield return new KeyValuePair<ulong, RootEntry>(set.Key, entry);
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            HashSet<RootEntry> result;
            RootData.TryGetValue(hash, out result);

            if (result == null)
                yield break;

            foreach (var entry in result)
                yield return entry;
        }

        public IEnumerable<RootEntry> GetEntriesByFileDataId(int fileDataId)
        {
            return GetEntries(GetHashByFileDataId(fileDataId));
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            var rootInfos = GetAllEntries(hash);

            if (!rootInfos.Any())
                yield break;

            var rootInfosLocale = rootInfos.Where(re => (re.Block.LocaleFlags & Locale) != 0);

            if (rootInfosLocale.Count() > 1)
            {
                var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags == Content));

                if (rootInfosLocaleAndContent.Any())
                    rootInfosLocale = rootInfosLocaleAndContent;
            }

            foreach (var entry in rootInfosLocale)
                yield return entry;
        }

        public ulong GetHashByFileDataId(int fileDataId)
        {
            ulong hash;
            FileDataStore.TryGetValue(fileDataId, out hash);
            return hash;
        }

        public int GetFileDataIdByHash(ulong hash)
        {
            int fid;
            FileDataStoreReverse.TryGetValue(hash, out fid);
            return fid;
        }

        public int GetFileDataIdByName(string name)
        {
            return GetFileDataIdByHash(Hasher.ComputeHash(name));
        }

        private bool LoadPreHashedListFile(string pathbin, string pathtext, BackgroundWorkerEx worker = null)
        {
            using (var _ = new PerfCounter("WowRootHandler::LoadPreHashedListFile()"))
            {
                worker?.ReportProgress(0, "Loading \"listfile\"...");

                if (!File.Exists(pathbin))
                    return false;

                var timebin = File.GetLastWriteTime(pathbin);
                var timetext = File.GetLastWriteTime(pathtext);

                if (timebin != timetext) // text has been modified, recreate crehashed file
                    return false;

                Logger.WriteLine("WowRootHandler: loading file names...");

                using (var fs = new FileStream(pathbin, FileMode.Open))
                using (var br = new BinaryReader(fs))
                {
                    int numFolders = br.ReadInt32();

                    for (int i = 0; i < numFolders; i++)
                    {
                        string dirName = br.ReadString();

                        //Logger.WriteLine(dirName);

                        int numFiles = br.ReadInt32();

                        for (int j = 0; j < numFiles; j++)
                        {
                            ulong fileHash = br.ReadUInt64();
                            string fileName = br.ReadString();

                            string fileNameFull = dirName != String.Empty ? dirName + "\\" + fileName : fileName;

                            // skip invalid names
                            if (!RootData.ContainsKey(fileHash))
                            {
                                Logger.WriteLine("Invalid file name: {0}", fileNameFull);
                                continue;
                            }

                            CASCFile.FileNames[fileHash] = fileNameFull;
                        }

                        worker?.ReportProgress((int)(fs.Position / (float)fs.Length * 100));
                    }

                    Logger.WriteLine("WowRootHandler: loaded {0} valid file names", CASCFile.FileNames.Count);
                }
            }

            return true;
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            if (LoadPreHashedListFile("listfile.bin", path, worker))
                return;

            using (var _ = new PerfCounter("WowRootHandler::LoadListFile()"))
            {
                worker?.ReportProgress(0, "Loading \"listfile\"...");

                if (!File.Exists(path))
                    throw new FileNotFoundException("list file missing!");

                Logger.WriteLine("WowRootHandler: loading file names...");

                Dictionary<string, Dictionary<ulong, string>> dirData = new Dictionary<string, Dictionary<ulong, string>>(StringComparer.OrdinalIgnoreCase);
                dirData[""] = new Dictionary<ulong, string>();

                using (var fs = new FileStream("listfile.bin", FileMode.Create))
                using (var bw = new BinaryWriter(fs))
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

                        int dirSepIndex = file.LastIndexOf('\\');

                        if (dirSepIndex >= 0)
                        {
                            string key = file.Substring(0, dirSepIndex);

                            if (!dirData.ContainsKey(key))
                            {
                                dirData[key] = new Dictionary<ulong, string>();
                            }

                            dirData[key][fileHash] = file.Substring(dirSepIndex + 1);
                        }
                        else
                            dirData[""][fileHash] = file;

                        worker?.ReportProgress((int)(sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }

                    bw.Write(dirData.Count); // count of dirs

                    foreach (var dir in dirData)
                    {
                        bw.Write(dir.Key); // dir name

                        //Logger.WriteLine(dir.Key);

                        bw.Write(dirData[dir.Key].Count); // count of files in dir

                        foreach (var fh in dirData[dir.Key])
                        {
                            bw.Write(fh.Key); // file name hash
                            bw.Write(fh.Value); // file name (without dir name)
                        }
                    }

                    Logger.WriteLine("WowRootHandler: loaded {0} valid file names", CASCFile.FileNames.Count);
                }

                File.SetLastWriteTime("listfile.bin", File.GetLastWriteTime(path));
            }
        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            CountSelect = 0;

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            foreach (var unkFile in UnknownFiles)
                CASCFile.FileNames.Remove(unkFile);

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.Block.LocaleFlags & Locale) != 0);

                if (rootInfosLocale.Count() > 1)
                {
                    var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags == Content));

                    if (rootInfosLocaleAndContent.Any())
                        rootInfosLocale = rootInfosLocaleAndContent;
                }

                if (!rootInfosLocale.Any())
                    continue;

                string file;

                if (!CASCFile.FileNames.TryGetValue(rootEntry.Key, out file))
                {
                    file = "unknown\\" + rootEntry.Key.ToString("X16") + "_" + rootEntry.Value.First().FileDataId;

                    CountUnknown++;
                    UnknownFiles.Add(rootEntry.Key);
                }

                CreateSubTree(root, rootEntry.Key, file);
                CountSelect++;
            }

            Logger.WriteLine("WowRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public bool IsUnknownFile(ulong hash)
        {
            return UnknownFiles.Contains(hash);
        }

        public override void Clear()
        {
            RootData.Clear();
            FileDataStore.Clear();
            FileDataStoreReverse.Clear();
            UnknownFiles.Clear();
            if (Root != null)
                Root.Entries.Clear();
            CASCFile.FileNames.Clear();
        }

        public override void Dump()
        {
            foreach (var fd in RootData.OrderBy(r => r.Value.First().FileDataId))
            {
                string name;

                if (!CASCFile.FileNames.TryGetValue(fd.Key, out name))
                    name = fd.Key.ToString("X16");

                Logger.WriteLine("{0:D7} {1:X16} {2} {3}", fd.Value.First().FileDataId, fd.Key, string.Join(",", fd.Value.Select(r => r.Block.LocaleFlags.ToString())), name);
            }
        }
    }
}
