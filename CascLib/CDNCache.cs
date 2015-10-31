using System.IO;

namespace CASCExplorer
{
    public class CDNCache
    {
        public bool Enabled { get; set; } = true;
        private bool CacheData { get; set; } = false;
        public bool Validate { get; set; } = false;

        private string cachePath;
        private SyncDownloader downloader = new SyncDownloader(null);

        public CDNCache(string path)
        {
            cachePath = path;
        }

        public Stream OpenFile(string name, string url, bool isData)
        {
            if (!Enabled)
                return null;

            if (isData && !CacheData)
                return null;

            string file = cachePath + "/" + name;

            Logger.WriteLine("CDNCache: Opening file {0}", file);

            FileInfo fi = new FileInfo(file);

            if (!fi.Exists || (Validate && fi.Length != downloader.GetFileSize(url)))
            {
                downloader.DownloadFile(url, file);
            }

            return new FileStream(file, FileMode.Open);
        }

        public bool HasFile(string name)
        {
            return File.Exists(cachePath + "/" + name);
        }
    }
}
