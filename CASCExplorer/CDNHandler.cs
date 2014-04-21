using System.Collections.Generic;
using System.IO;
using System.Net;

namespace CASCExplorer
{
    class CDNHandler
    {
        static Dictionary<string, MemoryStream> Indexes = new Dictionary<string, MemoryStream>();

        public static void Initialize(bool local)
        {
            if (local)
            {
                string wowPath = Properties.Settings.Default.WowPath;

                foreach (var index in CASCConfig.CDNConfig["archives"])
                {
                    var path = Path.Combine(wowPath, "Data\\indices\\", index + ".index");
                    Indexes.Add(index, new MemoryStream(File.ReadAllBytes(path)));
                }
            }
            else
            {
                using (WebClient client = new WebClient())
                {
                    foreach (var index in CASCConfig.CDNConfig["archives"])
                    {
                        var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";
                        Indexes.Add(index, new MemoryStream(client.DownloadData(url)));
                    }
                }
            }
        }
    }
}
