using System.Collections.Generic;
using System.IO;
using System.Net;

namespace CASCExplorer
{
    class IndexEntry
    {
        public int Index;
        public int Size;
        public int Offset;
    }

    class CDNHandler
    {
        static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        static Dictionary<byte[], IndexEntry> Indexes = new Dictionary<byte[], IndexEntry>(comparer);

        public static void Initialize(bool local)
        {
            if (local)
            {
                string wowPath = Properties.Settings.Default.WowPath;

                for (int i = 0; i < CASCConfig.CDNConfig["archives"].Count; ++i)
                {
                    var index = CASCConfig.CDNConfig["archives"][i];
                    var path = Path.Combine(wowPath, "Data\\indices\\", index + ".index");
                    ReadIndexFile(i, File.OpenRead(path));
                }
            }
            else
            {
                using (WebClient client = new WebClient())
                {
                    for (int i = 0; i < CASCConfig.CDNConfig["archives"].Count; ++i)
                    {
                        var index = CASCConfig.CDNConfig["archives"][i];
                        var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";
                        ReadIndexFile(i, client.OpenRead(url));
                    }
                }
            }
        }

        private static void ReadIndexFile(int index, Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                stream.Seek(-12, SeekOrigin.End);
                int count = br.ReadInt32();
                stream.Seek(0, SeekOrigin.Begin);

                for (int i = 0; i < count; ++i)
                {
                    byte[] key = br.ReadBytes(16);

                    if (key.IsZeroed()) // wtf?
                        continue;

                    IndexEntry entry = new IndexEntry();
                    entry.Index = index;
                    entry.Size = br.ReadInt32BE();
                    entry.Offset = br.ReadInt32BE();

                    Indexes.Add(key, entry);
                }
            }
        }

        public static Stream OpenFile(byte[] key)
        {
            var indexEntry = Indexes[key];

            var index = CASCConfig.CDNConfig["archives"][indexEntry.Index];
            var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index;

            HttpWebRequest part = (HttpWebRequest)WebRequest.Create(url);
            part.AddRange(indexEntry.Offset, indexEntry.Offset + indexEntry.Size);
            HttpWebResponse pr = (HttpWebResponse)part.GetResponse();
            return pr.GetResponseStream();
        }

        public static Stream OpenFileDirect(byte[] key)
        {
            var file = key.ToHexString().ToLower();
            var url = CASCConfig.CDNUrl + "/data/" + file.Substring(0, 2) + "/" + file.Substring(2, 2) + "/" + file;

            HttpWebRequest part = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse pr = (HttpWebResponse)part.GetResponse();
            return pr.GetResponseStream();
        }
    }
}
