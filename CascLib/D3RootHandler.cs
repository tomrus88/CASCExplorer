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
        Dictionary<string, byte[]> data = new Dictionary<string, byte[]>();
        private CASCFolder Root;

        public D3RootHandler(Stream stream, AsyncAction worker)
        {
            Root = new CASCFolder(0);

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

        public HashSet<RootEntry> GetAllEntries(ulong hash)
        {
            HashSet<RootEntry> hs = new HashSet<RootEntry>();

            foreach (var kv in data)
            {
                RootEntry entry = new RootEntry();
                entry.MD5 = kv.Value;

                hs.Add(entry);
            }

            return hs;
        }

        public IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            HashSet<RootEntry> hs = new HashSet<RootEntry>();

            foreach (var kv in data)
            {
                RootEntry entry = new RootEntry();
                entry.MD5 = kv.Value;

                hs.Add(entry);
            }

            return hs;
        }

        public void LoadListFile(string path, AsyncAction worker = null)
        {
            
        }

        public CASCFolder SetFlags(LocaleFlags locale, ContentFlags content, bool createTree = true)
        {
            return Root;
        }
    }
}
