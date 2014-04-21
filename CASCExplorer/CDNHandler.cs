using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CASCExplorer
{
    class CDNHandler
    {
        static Dictionary<string, MemoryStream> Indexes = new Dictionary<string, MemoryStream>();

        public static void Initialize()
        {
            WebClient client = new WebClient();

            foreach (var index in CASCConfig.CDNConfig["archives"])
            {
                var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";
                Indexes.Add(index, new MemoryStream(client.DownloadData(url)));
            }
        }
    }
}
