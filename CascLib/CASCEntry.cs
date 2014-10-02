using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public interface ICASCEntry : IComparable<ICASCEntry>
    {
        string Name { get; }
        ulong Hash { get; }
    }

    public class CASCFolder : ICASCEntry, IComparable<ICASCEntry>
    {
        public Dictionary<ulong, ICASCEntry> SubEntries;
        ulong hash;

        public CASCFolder(ulong _hash)
        {
            SubEntries = new Dictionary<ulong, ICASCEntry>();
            hash = _hash;
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

        public int CompareTo(ICASCEntry other)
        {
            if (other is CASCFolder)
                return Name.CompareTo(other.Name);
            else
                return this is CASCFolder ? -1 : 1;
        }

        public static readonly Dictionary<ulong, string> FolderNames = new Dictionary<ulong, string>();
    }

    public class CASCFile : ICASCEntry, IComparable<ICASCEntry>
    {
        ulong hash;

        public CASCFile(ulong _hash)
        {
            hash = _hash;
        }

        public string Name
        {
            get { return Path.GetFileName(FullName); }
        }

        public string FullName
        {
            get { return FileNames[hash]; }
        }

        public ulong Hash
        {
            get { return hash; }
        }

        public int CompareTo(ICASCEntry other)
        {
            if (other is CASCFile)
                return Name.CompareTo(other.Name);
            else
                return this is CASCFile ? 1 : -1;
        }

        public static readonly Dictionary<ulong, string> FileNames = new Dictionary<ulong, string>();
    }
}
