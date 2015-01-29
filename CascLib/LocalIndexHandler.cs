using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    class LocalIndexHandler
    {
        private static readonly ByteArrayComparer comparer = new ByteArrayComparer();
        private readonly Dictionary<byte[], IndexEntry> LocalIndexData = new Dictionary<byte[], IndexEntry>(comparer);

        public int Count
        {
            get { return LocalIndexData.Count; }
        }

        private LocalIndexHandler()
        {

        }

        public static LocalIndexHandler Initialize(CASCConfig config, AsyncAction worker)
        {
            var handler = new LocalIndexHandler();

            var idxFiles = GetIdxFiles(config);

            if (idxFiles.Count == 0)
                throw new FileNotFoundException("idx files missing!");

            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"local indexes\"...");
            }

            int idxIndex = 0;

            foreach (var idx in idxFiles)
            {
                handler.ParseIndex(idx);

                if (worker != null)
                {
                    worker.ThrowOnCancel();
                    worker.ReportProgress((int)((float)++idxIndex / (float)idxFiles.Count * 100));
                }
            }

            Logger.WriteLine("LocalIndexHandler: loaded {0} indexes", handler.Count);

            return handler;
        }

        private void ParseIndex(string idx)
        {
            using (var fs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                int h2Len = br.ReadInt32();
                int h2Check = br.ReadInt32();
                byte[] h2 = br.ReadBytes(h2Len);

                long padPos = (8 + h2Len + 0x0F) & 0xFFFFFFF0;
                fs.Position = padPos;

                int dataLen = br.ReadInt32();
                int dataCheck = br.ReadInt32();

                int numBlocks = dataLen / 18;

                for (int i = 0; i < numBlocks; i++)
                {
                    IndexEntry info = new IndexEntry();
                    byte[] key = br.ReadBytes(9);
                    int indexHigh = br.ReadByte();
                    int indexLow = br.ReadInt32BE();

                    info.Index = (int)((byte)(indexHigh << 2) | ((indexLow & 0xC0000000) >> 30));
                    info.Offset = (indexLow & 0x3FFFFFFF);
                    info.Size = br.ReadInt32();

                    // duplicate keys wtf...
                    //IndexData[key] = info; // use last key
                    if (!LocalIndexData.ContainsKey(key)) // use first key
                        LocalIndexData.Add(key, info);
                }

                padPos = (dataLen + 0x0FFF) & 0xFFFFF000;
                fs.Position = padPos;

                fs.Position += numBlocks * 18;
                //for (int i = 0; i < numBlocks; i++)
                //{
                //    var bytes = br.ReadBytes(18); // unknown data
                //}

                if (fs.Position != fs.Position)
                    throw new Exception("idx file under read");
            }
        }

        private static List<string> GetIdxFiles(CASCConfig config)
        {
            List<string> latestIdx = new List<string>();

            string dataFolder = config.BuildUID == "hero" ? "HeroesData" : "Data";
            string dataPath = String.Format("{0}\\data\\", dataFolder);

            for (int i = 0; i < 0x10; ++i)
            {
                var files = Directory.EnumerateFiles(Path.Combine(config.BasePath, dataPath), String.Format("{0:X2}*.idx", i));

                if (files.Count() > 0)
                    latestIdx.Add(files.Last());
            }

            return latestIdx;
        }

        public IndexEntry GetIndexInfo(byte[] key)
        {
            byte[] temp = key.Copy(9);

            IndexEntry result;
            if (!LocalIndexData.TryGetValue(temp, out result))
                Logger.WriteLine("LocalIndexHandler: missing index: {0}", key.ToHexString());

            return result;
        }

        public void Clear()
        {
            LocalIndexData.Clear();
        }
    }
}
