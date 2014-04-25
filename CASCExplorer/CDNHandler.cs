using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace CASCExplorer
{
    class CDNHandler
    {
        static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        static Dictionary<byte[], IndexEntry> CDNIndexData = new Dictionary<byte[], IndexEntry>(comparer);

        public static void Initialize(bool online)
        {
            for (int i = 0; i < CASCConfig.CDNConfig["archives"].Count; ++i)
            {
                string index = CASCConfig.CDNConfig["archives"][i];

                if (online)
                    DownloadFile(index, i);
                else
                    OpenFile(index, i);
            }

            Logger.WriteLine("CDNHandler: loaded {0} indexes", CDNIndexData.Count);
        }

        private static void ParseIndex(Stream stream, int i)
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

        private static void DownloadFile(string index, int i)
        {
            try
            {
                var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";

                using (WebClient webClient = new WebClient())
                using (Stream s = webClient.OpenRead(url))
                using (MemoryStream ms = new MemoryStream())
                {
                    s.CopyTo(ms);

                    ParseIndex(ms, i);
                }
            }
            catch
            {
                throw new Exception("DownloadFile failed!");
            }
        }

        private static void OpenFile(string index, int i)
        {
            try
            {
                var path = Path.Combine(Properties.Settings.Default.WowPath, "Data\\indices\\", index + ".index");

                using (FileStream fs = new FileStream(path, FileMode.Open))
                    ParseIndex(fs, i);
            }
            catch
            {
                throw new Exception("OpenFile failed!");
            }
        }

        public static Stream OpenDataFile(byte[] key)
        {
            var indexEntry = CDNIndexData[key];

            var index = CASCConfig.CDNConfig["archives"][indexEntry.Index];
            var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index;

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.AddRange(indexEntry.Offset, indexEntry.Offset + indexEntry.Size - 1);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            return resp.GetResponseStream();
        }

        public static Stream OpenDataFileDirect(byte[] key, out int len)
        {
            var file = key.ToHexString().ToLower();
            var url = CASCConfig.CDNUrl + "/data/" + file.Substring(0, 2) + "/" + file.Substring(2, 2) + "/" + file;

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            len = (int)resp.ContentLength;
            return resp.GetResponseStream();
        }

        public static Stream OpenConfigFileDirect(string key)
        {
            var url = CASCConfig.CDNUrl + "/config/" + key.Substring(0, 2) + "/" + key.Substring(2, 2) + "/" + key;

            return OpenFileDirect(url);
        }

        public static Stream OpenFileDirect(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            return resp.GetResponseStream();
        }

        public static IndexEntry GetCDNIndexInfo(byte[] key)
        {
            if (CDNIndexData.ContainsKey(key))
                return CDNIndexData[key];

            Logger.WriteLine("CDNHandler: missing index: {0}", key.ToHexString());

            return null;
        }
    }
}
