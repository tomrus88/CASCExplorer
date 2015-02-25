using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CASCExplorer
{
    public class D3RootHandler : IRootHandler
    {
        private Dictionary<string, byte[]> data = new Dictionary<string, byte[]>();
        private static readonly Jenkins96 Hasher = new Jenkins96();
        private CASCFolder Root;

        public D3RootHandler(Stream stream, AsyncAction worker)
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
            }
        }

        public ContentFlags Content
        {
            get
            {
                return ContentFlags.None;
            }
        }

        public int Count
        {
            get
            {
                return 0;
            }
        }

        public int CountSelect
        {
            get
            {
                return 0;
            }
        }

        public int CountTotal
        {
            get
            {
                return 0;
            }
        }

        public int CountUnknown
        {
            get
            {
                return 0;
            }
        }

        public LocaleFlags Locale
        {
            get
            {
                return LocaleFlags.None;
            }
        }

        public void Clear()
        {
            
        }

        public IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            foreach (var kv in data)
            {
                RootEntry entry = new RootEntry();
                entry.MD5 = kv.Value;

                yield return entry;
            }
        }

        public IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            foreach (var kv in data)
            {
                RootEntry entry = new RootEntry();
                entry.MD5 = kv.Value;

                yield return entry;
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

            return root;
        }

        public CASCFolder SetFlags(LocaleFlags locale, ContentFlags content, bool createTree = true)
        {
            if (createTree)
                Root = CreateStorageTree();

            return Root;
        }
    }
}
