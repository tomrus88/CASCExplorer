using System.Collections.Generic;

namespace CASCExplorer
{
    public abstract class RootHandlerBase
    {
        protected readonly Jenkins96 Hasher = new Jenkins96();
        protected CASCFolder Root;

        public virtual int Count { get; protected set; }
        public virtual int CountTotal { get; protected set; }
        public virtual int CountSelect { get; protected set; }
        public virtual int CountUnknown { get; protected set; }
        public virtual LocaleFlags Locale { get; protected set; }
        public virtual ContentFlags Content { get; protected set; }

        public abstract IEnumerable<RootEntry> GetAllEntries(ulong hash);

        public abstract IEnumerable<RootEntry> GetEntries(ulong hash);

        public abstract void LoadListFile(string path, BackgroundWorkerEx worker = null);

        public abstract void Clear();

        public abstract void Dump();

        protected abstract CASCFolder CreateStorageTree();

        protected void CreateSubTree(CASCFolder root, ulong filehash, string file, char separator)
        {
            string[] parts = file.Split(separator);

            CASCFolder folder = root;

            for (int i = 0; i < parts.Length; ++i)
            {
                bool isFile = (i == parts.Length - 1);

                string entryName = parts[i];

                ICASCEntry entry = folder.GetEntry(entryName);

                if (entry == null)
                {
                    if (isFile)
                    {
                        entry = new CASCFile(filehash);
                        CASCFile.FileNames[filehash] = file;
                    }
                    else
                    {
                        entry = new CASCFolder(entryName);
                    }

                    folder.Entries[entryName] = entry;
                }

                folder = entry as CASCFolder;
            }
        }

        public CASCFolder SetFlags(LocaleFlags locale, ContentFlags content, bool createTree = true)
        {
            using (var _ = new PerfCounter(GetType().Name + "::SetFlags()"))
            {
                if (Locale != locale || Content != content)
                {
                    Locale = locale;
                    Content = content;

                    if (createTree)
                        Root = CreateStorageTree();
                }

                return Root;
            }
        }
    }
}
