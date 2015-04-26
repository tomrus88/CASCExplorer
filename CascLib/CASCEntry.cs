using System;
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
        public Dictionary<string, ICASCEntry> Entries;

        private string name;

        public CASCFolder(string name)
        {
            Entries = new Dictionary<string, ICASCEntry>(StringComparer.InvariantCultureIgnoreCase);
            this.name = name;
        }

        public string Name
        {
            get { return name; }
        }

        public ulong Hash
        {
            get { return 0; }
        }

        public ICASCEntry GetEntry(string name)
        {
            ICASCEntry entry;
            Entries.TryGetValue(name, out entry);
            return entry;
        }

        public IEnumerable<CASCFile> GetFiles(IEnumerable<int> selection = null, bool recursive = true)
        {
            if (selection != null)
            {
                foreach (int index in selection)
                {
                    var entry = Entries.ElementAt(index);

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
                foreach (var entry in Entries)
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
