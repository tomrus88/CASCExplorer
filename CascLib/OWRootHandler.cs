using System;
using System.Collections.Generic;
using System.IO;

namespace CASCExplorer
{
    public class OWRootHandler : RootHandlerBase
    {
        private readonly Dictionary<ulong, RootEntry> RootData = new Dictionary<ulong, RootEntry>();

        public OWRootHandler(MMStream stream, BackgroundWorkerEx worker, CASCHandler casc)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            var str = stream.ReadCString();

            var array = str.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

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

                    using (MMStream s = new MMStream(casc.OpenFile(enc.Key)))
                    {
                        if (s != null)
                        {
                            // still need to figure out complete apm structure
                            // at start of file there's a lot of data that are same in all apm files
                            s.Position = 0xC;

                            uint count = s.ReadUInt32();

                            s.Position = 0x8CC;

                            for (int j = 0; j < count; j++)
                            {
                                // size of each entry seems to be 0x48 bytes (int size; byte[16] md5; ???)
                                //int size = s.ReadInt32();
                                byte[] md5_2 = s.ReadBytes(16);

                                EncodingEntry enc2 = casc.Encoding.GetEntry(md5_2);

                                if (enc2 == null)
                                {
                                    throw new Exception("boom!");
                                }

                                string fakeName = Path.GetFileNameWithoutExtension(filedata[2]) + "/" + md5_2.ToHexString();

                                ulong fileHash = Hasher.ComputeHash(fakeName);
                                RootData[fileHash] = new RootEntry() { MD5 = md5_2, Block = RootBlock.Empty };

                                CASCFile.FileNames[fileHash] = fakeName;

                                s.Position += (0x48 - 0x10);
                            }
                        }
                    }
                }
            }

            worker?.ReportProgress(100);
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
                CreateSubTree(root, rootEntry.Key, CASCFile.FileNames[rootEntry.Key], '/');
                CountSelect++;
            }

            Logger.WriteLine("HSRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public override void Clear()
        {
            Root.Entries.Clear();
            CASCFile.FileNames.Clear();
        }

        public override void Dump()
        {

        }
    }
}
