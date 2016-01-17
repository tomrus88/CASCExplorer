using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace CASCExplorer
{
    public class CacheMetaData
    {
        public long Size { get; private set; }
        public byte[] MD5 { get; private set; }

        public CacheMetaData(long size, byte[] md5)
        {
            Size = size;
            MD5 = md5;
        }

        public void Save(string file)
        {
            File.WriteAllText(file + ".dat", string.Format("{0} {1}", Size, MD5.ToHexString()));
        }

        public static CacheMetaData Load(string file)
        {
            if (File.Exists(file + ".dat"))
            {
                string[] tokens = File.ReadAllText(file + ".dat").Split(' ');
                return new CacheMetaData(Convert.ToInt64(tokens[0]), tokens[1].ToByteArray());
            }

            return null;
        }

        public static CacheMetaData AddToCache(HttpWebResponse resp, string file)
        {
            string md5 = resp.Headers[HttpResponseHeader.ETag].Split(':')[0].Substring(1);
            CacheMetaData meta = new CacheMetaData(resp.ContentLength, md5.ToByteArray());
            meta.Save(file);
            return meta;
        }
    }

    public class CDNCache
    {
        public bool Enabled { get; set; } = true;
        private bool CacheData { get; set; } = false;
        public bool Validate { get; set; } = true;

        private string cachePath;
        private SyncDownloader downloader = new SyncDownloader(null);

        private MD5 md5 = MD5.Create();

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

            string file = Path.Combine(cachePath, name);

            Logger.WriteLine("CDNCache: Opening file {0}", file);

            FileInfo fi = new FileInfo(file);

            if (!fi.Exists)
                downloader.DownloadFile(url, file);

            if (Validate)
            {
                CacheMetaData meta = CacheMetaData.Load(file) ?? downloader.GetMetaData(url, file);

                if (meta == null)
                    throw new Exception(string.Format("unable to validate file {0}", file));

                bool sizeOk, md5Ok;

                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    sizeOk = fs.Length == meta.Size;
                    md5Ok = md5.ComputeHash(fs).EqualsTo(meta.MD5);
                }

                if (!sizeOk || !md5Ok)
                    downloader.DownloadFile(url, file);
            }

            return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public bool HasFile(string name)
        {
            return File.Exists(Path.Combine(cachePath, name));
        }
    }
}
