using System;
using System.Collections.Generic;
using System.IO;

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
            get { return CASCHandler.FolderNames[hash]; }
        }

        public ulong Hash
        {
            get { return hash; }
        }

        public ICASCEntry GetEntry(ulong hash)
        {
            if (!SubEntries.ContainsKey(hash))
                return null;
            return SubEntries[hash];
        }

        public int CompareTo(ICASCEntry other)
        {
            if (other is CASCFolder)
                return Name.CompareTo(other.Name);
            else
                return this is CASCFolder ? -1 : 1;
        }
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
            get { return CASCHandler.FileNames[hash]; }
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
    }
}
