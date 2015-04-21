using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CASCExplorer
{
    class CASCEntrySorter : IComparer<ICASCEntry>
    {
        public int SortColumn { get; set; }
        public SortOrder Order { get; set; }
        public CASCHandler CASC { get; set; }

        public CASCEntrySorter()
        {
            SortColumn = 0;
            Order = SortOrder.Ascending;
        }

        public int Compare(ICASCEntry x, ICASCEntry y)
        {
            int result = 0;

            switch (SortColumn)
            {
                case 0: // Name
                    if (x is CASCFile && y is CASCFile)
                    {
                        result = x.Name.CompareTo(y.Name);
                    }
                    else if (x is CASCFolder && y is CASCFolder)
                    {
                        result = x.Name.CompareTo(y.Name);
                    }
                    else if (x is CASCFile) // x is CASCFile && y is CASCFolder
                    {
                        result = 1;
                    }
                    else // x is CASCFolder && y is CASCFile
                    {
                        result = -1;
                    }
                    break;
                case 1: // Type
                    if (x is CASCFile && y is CASCFile)
                    {
                        result = Path.GetExtension(x.Name).CompareTo(Path.GetExtension(y.Name));
                    }
                    else if (x is CASCFolder && y is CASCFolder)
                    {
                        result = x.Name.CompareTo(y.Name);
                    }
                    else if (x is CASCFile) // x is CASCFile && y is CASCFolder
                    {
                        result = 1;
                    }
                    else // x is CASCFolder && y is CASCFile
                    {
                        result = -1;
                    }
                    break;
                case 2: // Locale Flags
                    if (x is CASCFile && y is CASCFile)
                    {
                        var flags1 = CASC.Root.GetEntries(x.Hash).First().Block.LocaleFlags;
                        var flags2 = CASC.Root.GetEntries(y.Hash).First().Block.LocaleFlags;
                        result = flags1.CompareTo(flags2);
                    }
                    else if (x is CASCFolder && y is CASCFolder)
                    {
                        result = x.Name.CompareTo(y.Name);
                    }
                    else if (x is CASCFile) // x is CASCFile && y is CASCFolder
                    {
                        result = 1;
                    }
                    else // x is CASCFolder && y is CASCFile
                    {
                        result = -1;
                    }
                    break;
                case 3: // Content Flags
                    if (x is CASCFile && y is CASCFile)
                    {
                        var flags1 = CASC.Root.GetEntries(x.Hash).First().Block.ContentFlags;
                        var flags2 = CASC.Root.GetEntries(y.Hash).First().Block.ContentFlags;
                        result = flags1.CompareTo(flags2);
                    }
                    else if (x is CASCFolder && y is CASCFolder)
                    {
                        result = x.Name.CompareTo(y.Name);
                    }
                    else if (x is CASCFile) // x is CASCFile && y is CASCFolder
                    {
                        result = 1;
                    }
                    else // x is CASCFolder && y is CASCFile
                    {
                        result = -1;
                    }
                    break;
                case 4: // Size
                    if (x is CASCFile && y is CASCFile)
                    {
                        var size1 = (x as CASCFile).GetSize(CASC);
                        var size2 = (y as CASCFile).GetSize(CASC);

                        if (size1 == size2)
                            result = 0;
                        else
                            result = size1 < size2 ? -1 : 1;
                    }
                    else if (x is CASCFolder && y is CASCFolder)
                    {
                        result = 0;
                    }
                    else if (x is CASCFile) // x is CASCFile && y is CASCFolder
                    {
                        result = 1;
                    }
                    else // x is CASCFolder && y is CASCFile
                    {
                        result = -1;
                    }
                    break;
            }

            if (Order == SortOrder.Ascending)
                return result;
            else
                return -result;
        }
    }
}
