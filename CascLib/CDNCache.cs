using System.IO;

namespace CASCExplorer
{
    class CDNCache
    {
        public bool Enabled { get; set; }
        private bool CacheData { get; set; }
        private string cachePath;
        private SyncDownloader downloader = new SyncDownloader(null);

        public CDNCache(string path)
        {
            cachePath = path;
        }

        public MMStream OpenFile(string name, string url)
        {
            if (!Enabled)
                return null;

            string file = cachePath + "/" + name;

            Logger.WriteLine("CDNCache: Opening file {0}", file);

            if (!File.Exists(file))
            {
                downloader.DownloadFile(url, file);
            }

            return new MMStream(file);
        }

        public bool HasFile(string name)
        {
            return File.Exists(cachePath + "/" + name);
        }
    }
}
