using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public interface ICASCEntry
    {
        string Name { get; }
        ulong Hash { get; }
    }

    public class CASCFolder : ICASCEntry
    {
        public Dictionary<ulong, ICASCEntry> SubEntries;
        ulong hash;

        public CASCFolder(ulong hash)
        {
            SubEntries = new Dictionary<ulong, ICASCEntry>();
            this.hash = hash;
        }

        public string Name
        {
            get { return FolderNames[hash]; }
        }

        public ulong Hash
        {
            get { return hash; }
        }

        public ICASCEntry GetEntry(ulong hash)
        {
            ICASCEntry entry;
            SubEntries.TryGetValue(hash, out entry);
            return entry;
        }

        public IEnumerable<CASCFile> GetFiles(IEnumerable<int> selection = null, bool recursive = true)
        {
            if (selection != null)
            {
                foreach (int index in selection)
                {
                    var entry = SubEntries.ElementAt(index);

                    if (entry.Value is CASCFile)
                    {
                        yield return entry.Value as CASCFile;
                    }
                    else
                    {
                        if (recursive)
                        {
                            foreach (var file in (entry.Value as CASCFolder).GetFiles())
                            {
                                yield return file;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var entry in SubEntries)
                {
                    if (entry.Value is CASCFile)
                    {
                        yield return entry.Value as CASCFile;
                    }
                    else
                    {
                        if (recursive)
                        {
                            foreach (var file in (entry.Value as CASCFolder).GetFiles())
                            {
                                yield return file;
                            }
                        }
                    }
                }
            }
        }

        public static readonly Dictionary<ulong, string> FolderNames = new Dictionary<ulong, string>();
    }

    public class CASCFile : ICASCEntry
    {
        ulong hash;

        public CASCFile(ulong hash)
        {
            this.hash = hash;
        }

        public string Name
        {
            get { return Path.GetFileName(FullName); }
        }

        public string FullName
        {
            get { return FileNames[hash]; }
            set { FileNames[hash] = value; }
        }

        public ulong Hash
        {
            get { return hash; }
        }

        public int GetSize(CASCHandler casc)
        {
            var encoding = casc.GetEncodingEntry(hash);

            if (encoding != null)
                return encoding.Size;

            return 0;
        }

        public static readonly Dictionary<ulong, string> FileNames = new Dictionary<ulong, string>();
    }
}
