using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using CASCExplorer.Properties;

namespace CASCExplorer
{
    class CASC
    {
        static CASCFolder root;
        static CASCHandler cascHandler;

        public static CASCFolder Root
        {
            get { return root; }
        }

        public static CASCHandler Handler
        {
            get { return cascHandler; }
        }

        public static void Load(BackgroundWorker worker = null)
        {
            cascHandler = Settings.Default.OnlineMode
                ? CASCHandler.OpenOnlineStorage(worker)
                : CASCHandler.OpenLocalStorage(Settings.Default.WowPath, worker);

            root = cascHandler.LoadListFile2(Path.Combine(Application.StartupPath, "listfile.txt"));
        }
    }
}
