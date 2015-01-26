using System.Collections.Generic;

namespace CASCExplorer
{
    public interface IRootHandler
    {
        int Count { get; }
        int CountTotal { get; }
        int CountSelect { get; }
        int CountUnknown { get; }
        LocaleFlags Locale { get; }
        ContentFlags Content { get; }

        HashSet<RootEntry> GetAllEntries(ulong hash);

        IEnumerable<RootEntry> GetEntries(ulong hash);

        void LoadListFile(string path, AsyncAction worker = null);

        CASCFolder SetFlags(LocaleFlags locale, ContentFlags content, bool createTree = true);
    }
}
