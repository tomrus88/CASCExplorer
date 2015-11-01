using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CASCExplorer
{
    public class OWRootHandler : RootHandlerBase
    {
        private readonly Dictionary<ulong, RootEntry> RootData = new Dictionary<ulong, RootEntry>();

        public OWRootHandler(BinaryReader stream, BackgroundWorkerEx worker, CASCHandler casc)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            string str = Encoding.ASCII.GetString(stream.ReadBytes((int)stream.BaseStream.Length));

            string[] array = str.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // need to figure out what to do with those apm files

            for (int i = 1; i < array.Length; i++)
            {
                string[] filedata = array[i].Split('|');

                if (Path.GetExtension(filedata[2]) == ".apm")
                {
                    // add apm file for dev purposes
                    ulong fileHash1 = Hasher.ComputeHash(filedata[2]);
                    RootData[fileHash1] = new RootEntry() { MD5 = filedata[0].ToByteArray(), Block = RootBlock.Empty };

                    CASCFile.FileNames[fileHash1] = filedata[2];

                    // add files listed in apm file
                    byte[] md5 = filedata[0].ToByteArray();

                    EncodingEntry enc = casc.Encoding.GetEntry(md5);

                    using (BinaryReader s = new BinaryReader(casc.OpenFile(enc.Key)))
                    {
                        if (s != null)
                        {
                            // still need to figure out complete apm structure
                            // at start of file there's a lot of data that is same in all apm files
                            s.BaseStream.Position = 0xC;

                            uint count = s.ReadUInt32();

                            s.BaseStream.Position = 0x894;

                            // size of each entry seems to be 0x48 bytes (0x2C bytes unk data; int size; ulong unk; byte[16] md5)
                            for (int j = 0; j < count; j++)
                            {
                                s.BaseStream.Position += 0x2C; // skip unknown
                                int size = s.ReadInt32(); // size (matches size in encoding file)
                                s.BaseStream.Position += 8; // skip unknown
                                byte[] md5_2 = s.ReadBytes(16);

                                EncodingEntry enc2 = casc.Encoding.GetEntry(md5_2);

                                if (enc2 == null)
                                {
                                    throw new Exception("enc2 == null");
                                }

                                string fakeName = Path.GetFileNameWithoutExtension(filedata[2]) + "/" + md5_2.ToHexString();

                                ulong fileHash = Hasher.ComputeHash(fakeName);
                                RootData[fileHash] = new RootEntry() { MD5 = md5_2, Block = RootBlock.Empty };

                                CASCFile.FileNames[fileHash] = fakeName;
                            }
                        }
                    }
                }
            }

            int current = 0;

            foreach (var entry in casc.Encoding.Entries)
            {
                DownloadEntry dl = casc.Download.GetEntry(entry.Value.Key);

                if (dl != null)
                {
                    string fakeName = "unknown" + "/" + entry.Key[0].ToString("X2") + "/" + entry.Key.ToHexString();

                    var locales = dl.Tags.Where(tag => tag.Value.Type == 4).Select(tag => (LocaleFlags)Enum.Parse(typeof(LocaleFlags), tag.Key));

                    LocaleFlags locale = LocaleFlags.None;

                    foreach (var loc in locales)
                        locale |= loc;

                    ulong fileHash = Hasher.ComputeHash(fakeName);
                    RootData[fileHash] = new RootEntry() { MD5 = entry.Key, Block = new RootBlock() { LocaleFlags = locale } };

                    CASCFile.FileNames[fileHash] = fakeName;
                }

                worker?.ReportProgress((int)(++current / (float)casc.Encoding.Count * 100));
            }
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            RootEntry entry;

            if (RootData.TryGetValue(hash, out entry))
                yield return entry;
            else
                yield break;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            RootEntry entry;

            if (RootData.TryGetValue(hash, out entry))
                yield return entry;
            else
                yield break;
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {

        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            CountSelect = 0;

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            foreach (var rootEntry in RootData)
            {
                if ((rootEntry.Value.Block.LocaleFlags & Locale) == 0)
                    continue;

                CreateSubTree(root, rootEntry.Key, CASCFile.FileNames[rootEntry.Key], '/');
                CountSelect++;
            }

            Logger.WriteLine("OWRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public override void Clear()
        {
            RootData.Clear();
            Root.Entries.Clear();
            CASCFile.FileNames.Clear();
        }

        public override void Dump()
        {

        }
    }
}
