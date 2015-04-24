using System;
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

        public virtual void LoadListFile(string path, AsyncAction worker = null)
        {

        }

        public abstract void Clear();

        protected abstract CASCFolder CreateStorageTree();

        Dictionary<string, ulong> dirHashes = new Dictionary<string, ulong>(StringComparer.InvariantCultureIgnoreCase);

        private ulong GetOrComputeDirHash(string dir)
        {
            ulong hash;

            if (dirHashes.TryGetValue(dir, out hash))
                return hash;

            hash = Hasher.ComputeHash(dir);
            dirHashes[dir] = hash;

            return hash;
        }

        protected void CreateSubTree(CASCFolder root, ulong filehash, string file, char separator)
        {
            string[] parts = file.Split(separator);

            CASCFolder folder = root;

            for (int i = 0; i < parts.Length; ++i)
            {
                bool isFile = (i == parts.Length - 1);

                ulong hash = isFile ? filehash : GetOrComputeDirHash(parts[i]);

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
