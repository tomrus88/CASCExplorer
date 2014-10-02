using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;

namespace CASCExplorer
{
    internal class UserState
    {
        public int Index;
        public string Path;
        public Stream Stream;
        public ManualResetEvent ResetEvent = new ManualResetEvent(false);
    }

    internal class CDNIndexHandler
    {
        private static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        private readonly Dictionary<byte[], IndexEntry> CDNIndexData = new Dictionary<byte[], IndexEntry>(comparer);

        private CASCConfig CASCConfig;
        private AsyncAction worker;
        private Semaphore downloadSemaphore = new Semaphore(1, 1);

        public int Count
        {
            get { return CDNIndexData.Count; }
        }

        private CDNIndexHandler(CASCConfig cascConfig, AsyncAction worker)
        {
            CASCConfig = cascConfig;
            this.worker = worker;
        }

        public static CDNIndexHandler Initialize(CASCConfig config, AsyncAction worker)
        {
            var handler = new CDNIndexHandler(config, worker);

            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0);
            }

            for (int i = 0; i < config.Archives.Count; i++)
            {
                string index = config.Archives[i];

                if (config.OnlineMode)
                    handler.DownloadFile(index, i);
                else
                    handler.OpenFile(index, i);

                if (worker != null)
                {
                    worker.ThrowOnCancel();
                    worker.ReportProgress((int)((float)i / (float)config.Archives.Count * 100));
                }
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
                    ParseIndex(fs, i);
                return;
            }

            try
            {
                var url = CASCConfig.CDNUrl + "/data/" + index.Substring(0, 2) + "/" + index.Substring(2, 2) + "/" + index + ".index";

                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
                    webClient.DownloadFileCompleted += WebClient_DownloadFileCompleted;
                    webClient.DownloadFileAsync(new Uri(url), path, new UserState() { Index = i, Path = path });
                    downloadSemaphore.WaitOne();
                }
            }
            catch
            {
                throw new Exception("DownloadFile failed!");
            }
        }

        private void WebClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
                return;

            downloadSemaphore.Release();

            var state = (UserState)e.UserState;

            using (FileStream fs = File.OpenRead(state.Path))
                ParseIndex(fs, state.Index);
        }

        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (worker != null)
            {
                if (worker.IsCancellationRequested)
                    (sender as WebClient).CancelAsync();

                //worker.ThrowOnCancel();
                //worker.ReportProgress(e.ProgressPercentage);
            }
        }

        private void OpenFile(string index, int i)
        {
            try
            {
                var path = Path.Combine(CASCConfig.BasePath, "Data\\indices\\", index + ".index");

                using (FileStream fs = new FileStream(path, FileMode.Open))
                    ParseIndex(fs, i);
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

        public Stream OpenDataFileDirect(byte[] key)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0);
            }

            var file = key.ToHexString().ToLower();
            var url = CASCConfig.CDNUrl + "/data/" + file.Substring(0, 2) + "/" + file.Substring(2, 2) + "/" + file;

            WebClient client = new WebClient();
            client.DownloadProgressChanged += Client_DownloadProgressChanged;
            client.DownloadDataCompleted += Client_DownloadDataCompleted;

            UserState state = new UserState();

            client.DownloadDataAsync(new Uri(url), state);
            state.ResetEvent.WaitOne();
            return state.Stream;
        }

        private void Client_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Cancelled)
                return;

            UserState state = e.UserState as UserState;

            state.Stream = new MemoryStream(e.Result);
            state.ResetEvent.Set();
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (worker != null)
            {
                if (worker.IsCancellationRequested)
                    (sender as WebClient).CancelAsync();

                //worker.ThrowOnCancel();
                worker.ReportProgress(e.ProgressPercentage);
            }
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
