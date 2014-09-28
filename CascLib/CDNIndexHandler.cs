using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;

namespace CASCExplorer
{
    internal class CDNIndexHandler
    {
        private static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        private readonly Dictionary<byte[], IndexEntry> CDNIndexData = new Dictionary<byte[], IndexEntry>(comparer);

        private CASCConfig CASCConfig;

        public int Count
        {
            get { return CDNIndexData.Count; }
        }

        private CDNIndexHandler(CASCConfig cascConfig)
        {
            CASCConfig = cascConfig;
        }

        public static CDNIndexHandler Initialize(CASCConfig config, BackgroundWorker worker)
        {
            var handler = new CDNIndexHandler(config);

            for (int i = 0; i < config.Archives.Count; i++)
            {
                string index = config.Archives[i];

                if (config.OnlineMode)
                    handler.DownloadFile(index, i);
                else
                    handler.OpenFile(index, i);

                if (worker != null)
                    worker.ReportProgress((int)((float)i / (float)config.Archives.Count * 100));
            }

            Logger.WriteLine("CDNIndexHandler: loaded {0} indexes", handler.Count);

            return handler;
        }

        private void ParseIndex(Stream stream, int i)
        {
            using (var br = new BinaryReader(stream))
            {
                stream.Seek(-12, SeekOrigin.End);
                int count = br.ReadInt32();
                stream.Seek(0, SeekOrigin.Begin);

                for (int j = 0; j < count; ++j)
                {
                    byte[] key = br.ReadBytes(16);

                    if (key.IsZeroed()) // wtf?
                        key = br.ReadBytes(16);

                    if (key.IsZeroed()) // wtf?
                        throw new Exception("key.IsZeroed()");

                    IndexEntry entry = new IndexEntry();
                    entry.Index = i;
                    entry.Size = br.ReadInt32BE();
                    entry.Offset = br.ReadInt32BE();

                    CDNIndexData.Add(key, entry);
                }
            }
        }

        private void DownloadFile(string index, int i)
        {
            var rootPath = Path.Combine("data", CASCConfig.Build.ToString(), "indices");

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            var path = Path.Combine(rootPath, index + ".index");

            if (File.Exists(path))
            {
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    ParseIndex(fs, i);
                }
                return;
            }

            try
            {
                var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";

                using (WebClient webClient = new WebClient())
                using (Stream s = webClient.OpenRead(url))
                using (MemoryStream ms = new MemoryStream())
                using (FileStream fs = File.Create(path))
                {
                    s.CopyTo(ms);
                    ms.Position = 0;
                    ms.CopyTo(fs);

                    ParseIndex(ms, i);
                }
            }
            catch
            {
                throw new Exception("DownloadFile failed!");
            }
        }

        private void OpenFile(string index, int i)
        {
            try
            {
                var path = Path.Combine(CASCConfig.BasePath, "Data\\indices\\", index + ".index");

                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    ParseIndex(fs, i);
                }
            }
            catch
            {
                throw new Exception("OpenFile failed!");
            }
        }

        public Stream OpenDataFile(byte[] key)
        {
            var indexEntry = CDNIndexData[key];

            var index = CASCConfig.Archives[indexEntry.Index];
            var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index;

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.AddRange(indexEntry.Offset, indexEntry.Offset + indexEntry.Size - 1);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            return resp.GetResponseStream();
        }

        public Stream OpenDataFileDirect(byte[] key, out int len)
        {
            var file = key.ToHexString().ToLower();
            var url = CASCConfig.CDNUrl + "/data/" + file.Substring(0, 2) + "/" + file.Substring(2, 2) + "/" + file;

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            len = (int)resp.ContentLength;
            return resp.GetResponseStream();
        }

        public static Stream OpenConfigFileDirect(string cdnUrl, string key)
        {
            var url = cdnUrl + "/config/" + key.Substring(0, 2) + "/" + key.Substring(2, 2) + "/" + key;

            return OpenFileDirect(url);
        }

        public static Stream OpenFileDirect(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            return resp.GetResponseStream();
        }

        public IndexEntry GetIndexInfo(byte[] key)
        {
            IndexEntry result;
            if (!CDNIndexData.TryGetValue(key, out result))
                Logger.WriteLine("CDNHandler: missing index: {0}", key.ToHexString());

            return result;
        }
    }
}
