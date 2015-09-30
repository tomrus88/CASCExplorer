using System.Collections.Generic;

namespace CASCExplorer
{
    public class HSRootHandler : RootHandlerBase
    {
        public HSRootHandler(MMStream stream, AsyncAction worker)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"root\"...");
            }

            // Hearthstone root file happened to be game executable! Just ignore it.

            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(100);
            }
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            yield break;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            yield break;
        }

        public override void LoadListFile(string path, AsyncAction worker = null)
        {

        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");

            CountSelect = 0;

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            Logger.WriteLine("HSRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public override void Clear()
        {
            Root.Entries.Clear();
            CASCFile.FileNames.Clear();
        }

        public override void Dump()
        {

        }
    }
}
