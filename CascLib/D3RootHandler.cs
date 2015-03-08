using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class D3RootEntry
    {
        public byte[] MD5;
        public string Name;
        public LocaleFlags LocaleFlags;
    }

    public class D3RootHandler : IRootHandler
    {
        private Dictionary<string, byte[]> data = new Dictionary<string, byte[]>();
        private readonly MultiDictionary<ulong, D3RootEntry> RootData = new MultiDictionary<ulong, D3RootEntry>();
        private static readonly Jenkins96 Hasher = new Jenkins96();
        private LocaleFlags locale;
        private CASCFolder Root;

        public int Count { get { return RootData.Count; } }
        public int CountTotal { get { return RootData.Sum(re => re.Value.Count); } }
        public int CountSelect { get; private set; }
        public int CountUnknown { get; private set; }
        public LocaleFlags Locale { get { return locale; } }
        public ContentFlags Content { get { return ContentFlags.None; } }

        public D3RootHandler(Stream stream, AsyncAction worker, CASCHandler casc)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"root\"...");
            }

            using (var br = new BinaryReader(stream))
            {
                byte b1 = br.ReadByte();
                byte b2 = br.ReadByte();
                byte b3 = br.ReadByte();
                byte b4 = br.ReadByte();

                int count = br.ReadInt32();

                for (int i = 0; i < count; ++i)
                {
                    byte[] hash = br.ReadBytes(16);
                    string name = br.ReadCString();

                    data[name] = hash;

                    Logger.WriteLine("{0}: {1} {2}", i, hash.ToHexString(), name);
                }

                // we need to parse CoreTOC.dat for sno stuff...

                foreach (var kv in data)
                {
                    EncodingEntry enc = casc.Encoding.GetEntry(kv.Value);

                    Stream s = OpenD3SubRootFile(casc, enc.Key, kv.Value, "data\\" + casc.Config.BuildName + "\\subroot\\" + kv.Key);

                    if (s != null)
                    {
                        using (var br2 = new BinaryReader(s))
                        {
                            uint magic = br2.ReadUInt32();

                            int nEntries0 = br2.ReadInt32();

                            for (int i = 0; i < nEntries0; i++)
                            {
                                D3RootEntry entry = new D3RootEntry();
                                entry.MD5 = br2.ReadBytes(16);
                                int snoId = br2.ReadInt32();
                                //filename can be inferred with format str %s\%s%s, using SNOGroup, AssetName and file extension (from SNOGroup)
                                entry.Name = "entry0\\snoId_" + snoId;

                                LocaleFlags locale;

                                if (Enum.TryParse<LocaleFlags>(kv.Key, out locale))
                                    entry.LocaleFlags = locale;
                                else
                                    entry.LocaleFlags = LocaleFlags.All;

                                RootData.Add(Hasher.ComputeHash(entry.Name), entry);
                            }

                            int nEntries1 = br2.ReadInt32();

                            for (int i = 0; i < nEntries1; i++)
                            {
                                D3RootEntry entry = new D3RootEntry();
                                entry.MD5 = br2.ReadBytes(16);
                                int snoId = br2.ReadInt32();
                                int fileNumber = br2.ReadInt32();
                                //filename can be inferred as above but with format %s\%s\%04d%s, using SNOGroup, AssetName, fileNumber and an extension, which can be .fsb, .ogg, .svr...
                                entry.Name = "entry1\\snoId_" + snoId + "\\fileNumber_" + fileNumber;

                                LocaleFlags locale;

                                if (Enum.TryParse<LocaleFlags>(kv.Key, out locale))
                                    entry.LocaleFlags = locale;
                                else
                                    entry.LocaleFlags = LocaleFlags.All;

                                RootData.Add(Hasher.ComputeHash(entry.Name), entry);
                            }

                            int nNamedEntries = br2.ReadInt32();

                            for (int i = 0; i < nNamedEntries; i++)
                            {
                                D3RootEntry entry = new D3RootEntry();
                                entry.MD5 = br2.ReadBytes(16);
                                entry.Name = br2.ReadCString();

                                LocaleFlags locale;

                                if (Enum.TryParse<LocaleFlags>(kv.Key, out locale))
                                    entry.LocaleFlags = locale;
                                else
                                    entry.LocaleFlags = LocaleFlags.All;

                                RootData.Add(Hasher.ComputeHash(entry.Name), entry);
                            }
                        }
                    }
                }
            }
        }

        private Stream OpenD3SubRootFile(CASCHandler casc, byte[] key, byte[] md5, string name)
        {
            Stream s = casc.TryLocalCache(key, md5, name);

            if (s != null)
                return s;

            s = casc.TryLocalCache(key, md5, name);

            if (s != null)
                return s;

            return casc.OpenFile(key);
        }

        public void Clear()
        {
            data.Clear();
            RootData.Clear();
        }

        public IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            HashSet<D3RootEntry> result;
            RootData.TryGetValue(hash, out result);

            foreach (var e in result)
            {
                var re = new RootEntry();
                re.MD5 = e.MD5;
                yield return re;
            }
        }

        public IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            HashSet<D3RootEntry> result;
            RootData.TryGetValue(hash, out result);

            foreach (var e in result)
            {
                var re = new RootEntry();
                re.MD5 = e.MD5;
                yield return re;
            }

        }

        public void LoadListFile(string path, AsyncAction worker = null)
        {
            
        }

        private CASCFolder CreateStorageTree()
        {
            var rootHash = Hasher.ComputeHash("root");

            var root = new CASCFolder(rootHash);

            CASCFolder.FolderNames[rootHash] = "root";

            CountSelect = 0;

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.LocaleFlags & locale) != 0);

                //if (rootInfosLocale.Count() > 1)
                //{
                //    var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags == content));

                //    if (rootInfosLocaleAndContent.Any())
                //        rootInfosLocale = rootInfosLocaleAndContent;
                //}

                if (!rootInfosLocale.Any())
                    continue;

                string file = rootEntry.Value.First().Name;

                //if (!CASCFile.FileNames.TryGetValue(rootEntry.Key, out file))
                //{
                //    file = "unknown\\" + rootEntry.Key.ToString("X16");
                //    CountUnknown++;
                //    UnknownFiles.Add(rootEntry.Key);
                //}

                CreateSubTree(root, rootEntry.Key, file);
                CountSelect++;
            }

            //Logger.WriteLine("D3RootHandler: {0} file names missing for locale {1}", CountUnknown, locale);

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
            if (this.locale != locale)
            {
                this.locale = locale;

                if (createTree)
                    Root = CreateStorageTree();
            }

            return Root;
        }
    }
}
