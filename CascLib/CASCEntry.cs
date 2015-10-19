using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public interface ICASCEntry
    {
        string Name { get; }
        ulong Hash { get; }
    }

    public class CASCFolder : ICASCEntry, INotifyPropertyChanged
    {
        private CASCFolder _parent;
        private bool _isSelected;
        private bool _isExpanded;
        private string _name;
        private ulong _hash;

        public event PropertyChangedEventHandler PropertyChanged;

        public Dictionary<string, ICASCEntry> Entries { get; private set; }

        public CASCFolder Parent
        {
            get { return _parent; }
            set { _parent = value; }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if(_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                }

                if (_isExpanded && _parent != null)
                    _parent.IsExpanded = true;
            }
        }

        public CASCFolder(string name, CASCFolder parent)
        {
            Entries = new Dictionary<string, ICASCEntry>(StringComparer.OrdinalIgnoreCase);
            _name = name;
            _parent = parent;
            _hash = 0;
        }

        public string Name
        {
            get { return _name; }
        }

        public ulong Hash
        {
            get { return _hash; }
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
        private ulong hash;

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
