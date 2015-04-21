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
        public static RootBlock Empty = new RootBlock();
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
            return String.Format("RootBlock: {0:X8} {1:X8}, File: {2:X8} {3}", Block.ContentFlags, Block.LocaleFlags, FileDataId, MD5.ToHexString());
        }
    }

    public class WowRootHandler : IRootHandler
    {
        private readonly MultiDictionary<ulong, RootEntry> RootData = new MultiDictionary<ulong, RootEntry>();
        private readonly Dictionary<int, ulong> FileDataStore = new Dictionary<int, ulong>();
        private readonly HashSet<ulong> UnknownFiles = new HashSet<ulong>();
        private static readonly Jenkins96 Hasher = new Jenkins96();
        private LocaleFlags locale;
        private ContentFlags content;
        private CASCFolder Root;

        public int Count { get { return RootData.Count; } }
        public int CountTotal { get { return RootData.Sum(re => re.Value.Count); } }
        public int CountSelect { get; private set; }
        public int CountUnknown { get; private set; }
        public LocaleFlags Locale { get { return locale; } }
        public ContentFlags Content { get { return content; } }

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

                    int fileDataIndex = 0;

                    for (var i = 0; i < count; ++i)
                    {
                        entries[i] = new RootEntry();
                        entries[i].Block = block;
                        entries[i].FileDataId = fileDataIndex + br.ReadInt32();

                        fileDataIndex = entries[i].FileDataId + 1;
                    }

                    //Console.WriteLine("Block: {0} {1} (size {2})", block.ContentFlags, block.LocaleFlags, count);

                    for (var i = 0; i < count; ++i)
                    {
                        entries[i].MD5 = br.ReadBytes(16);

                        ulong hash = br.ReadUInt64();

                        RootData.Add(hash, entries[i]);

                        //Console.WriteLine("File: {0:X8} {1:X16} {2}", entries[i].FileDataId, hash, entries[i].MD5.ToHexString());

                        if (FileDataStore.ContainsKey(entries[i].FileDataId) && FileDataStore[entries[i].FileDataId] == hash)
                        {
                            //Console.WriteLine("2 {0:X8} {1:X16}", entries[i].FileDataId, hash);
                            continue;
                        }

                        FileDataStore.Add(entries[i].FileDataId, hash);
                    }

                    if (worker != null)
                    {
                        worker.ThrowOnCancel();
                        worker.ReportProgress((int)((float)stream.Position / (float)stream.Length * 100));
                    }
                }
            }
        }

        public IEnumerable<RootEntry> GetAllEntriesByFileDataId(int fileDataId)
        {
            ulong hash;
            FileDataStore.TryGetValue(fileDataId, out hash);
            return GetAllEntries(hash);
        }

        public IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            HashSet<RootEntry> result;
            RootData.TryGetValue(hash, out result);
            return result;
        }

        public IEnumerable<RootEntry> GetEntriesByFileDataId(int fileDataId)
        {
            ulong hash;
            FileDataStore.TryGetValue(fileDataId, out hash);
            return GetEntries(hash);
        }

        // Returns only entries that match current locale and content flags
        public IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            var rootInfos = GetAllEntries(hash);

            if (rootInfos == null)
                yield break;

            var rootInfosLocale = rootInfos.Where(re => (re.Block.LocaleFlags & locale) != 0);

            if (rootInfosLocale.Count() > 1)
            {
                var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags == content));

                if (rootInfosLocaleAndContent.Any())
                    rootInfosLocale = rootInfosLocaleAndContent;
            }

            foreach (var entry in rootInfosLocale)
                yield return entry;
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

            Logger.WriteLine("WowRootHandler: loading file names...");

            //Dictionary<string, bool> dirs = new Dictionary<string, bool>();

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

                    //int dirIndex = file.LastIndexOf('\\');

                    //if (dirIndex >= 0)
                    //    dirs[file.ToLower().Substring(0, dirIndex)] = true;

                    if (worker != null)
                    {
                        worker.ThrowOnCancel();
                        worker.ReportProgress((int)((float)sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }
                }

                //foreach (var dir in dirs)
                //    Console.WriteLine(dir.Key);

                Logger.WriteLine("WowRootHandler: loaded {0} valid file names", CASCFile.FileNames.Count);
            }
        }

        private CASCFolder CreateStorageTree()
        {
            var rootHash = Hasher.ComputeHash("root");

            var root = new CASCFolder(rootHash);

            CASCFolder.FolderNames[rootHash] = "root";

            CountSelect = 0;

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            foreach (var unkFile in UnknownFiles)
                CASCFile.FileNames.Remove(unkFile);

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.Block.LocaleFlags & locale) != 0);

                if (rootInfosLocale.Count() > 1)
                {
                    var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags == content));

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

            Logger.WriteLine("WowRootHandler: {0} file names missing for locale {1}", CountUnknown, locale);

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

        public CASCFolder SetFlags(LocaleFlags locale, ContentFlags content, bool createTree = true)
        {
            if (this.locale != locale || this.content != content)
            {
                this.locale = locale;
                this.content = content;

                if (createTree)
                    Root = CreateStorageTree();
            }

            return Root;
        }

        public bool IsUnknownFile(ulong hash)
        {
            return UnknownFiles.Contains(hash);
        }

        public void Clear()
        {
            RootData.Clear();
            UnknownFiles.Clear();
            Root.SubEntries.Clear();
            CASCFolder.FolderNames.Clear();
            CASCFile.FileNames.Clear();
        }
    }
}
