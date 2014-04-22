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

        static Func<int, Stream> localWorker = i =>
        {
            var index = CASCConfig.CDNConfig["archives"][i];
            var path = Path.Combine(Properties.Settings.Default.WowPath, "Data\\indices\\", index + ".index");
            return File.OpenRead(path);
        };

        static Func<int, Stream> cdnWorker = i =>
        {
            var index = CASCConfig.CDNConfig["archives"][i];
            var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";
            return new MemoryStream(new WebClient().DownloadData(url));
        };

        public static void Initialize(bool online)
        {
            for (int i = 0; i < CASCConfig.CDNConfig["archives"].Count; ++i)
            {
                using (var stream = online ? cdnWorker(i) : localWorker(i))
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
        }

        public static Stream OpenFile(byte[] key)
        {
            var indexEntry = CDNIndexData[key];

            var index = CASCConfig.CDNConfig["archives"][indexEntry.Index];
            var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index;

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.AddRange(indexEntry.Offset, indexEntry.Offset + indexEntry.Size);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            return resp.GetResponseStream();
        }

        public static Stream OpenFileDirect(byte[] key)
        {
            var file = key.ToHexString().ToLower();
            var url = CASCConfig.CDNUrl + "/data/" + file.Substring(0, 2) + "/" + file.Substring(2, 2) + "/" + file;

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
