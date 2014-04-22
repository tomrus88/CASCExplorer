using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace CASCExplorer
{
    class CDNHandler
    {
        static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        static Dictionary<byte[], IndexEntry> CDNIndexData = new Dictionary<byte[], IndexEntry>(comparer);

        public static async void Initialize(bool online)
        {
            if (online)
            {
                await Task.WhenAll(CASCConfig.CDNConfig["archives"].Select((index, i) => DownloadFileAsync(index, i)));
            }
            else
            {
                await Task.WhenAll(CASCConfig.CDNConfig["archives"].Select((index, i) => OpenFileAsync(index, i)));
            }
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

        private static async Task DownloadFileAsync(string index, int i)
        {
            try
            {
                var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";

                using (WebClient webClient = new WebClient())
                using (Stream s = await webClient.OpenReadTaskAsync(url))
                using (MemoryStream ms = new MemoryStream())
                {
                    await s.CopyToAsync(ms);

                    ParseIndex(ms, i);
                }
            }
            catch
            {
                throw new Exception("DownloadFileAsync failed!");
            }
        }

        private static async Task OpenFileAsync(string index, int i)
        {
            try
            {
                var path = Path.Combine(Properties.Settings.Default.WowPath, "Data\\indices\\", index + ".index");

                using (FileStream fs = new FileStream(path, FileMode.Open))
                using (MemoryStream ms = new MemoryStream())
                {
                    await fs.CopyToAsync(ms);

                    ParseIndex(ms, i);
                }
                //using (FileStream fs = new FileStream(path, FileMode.Open))
                //{
                //    ParseIndex(fs, i);
                //}
            }
            catch
            {
                throw new Exception("OpenFileAsync failed!");
            }
        }

        public static Stream OpenDataFile(byte[] key)
        {
            var indexEntry = CDNIndexData[key];

            var index = CASCConfig.CDNConfig["archives"][indexEntry.Index];
            var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index;

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.AddRange(indexEntry.Offset, indexEntry.Offset + indexEntry.Size);
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
            return null;
        }
    }
}
