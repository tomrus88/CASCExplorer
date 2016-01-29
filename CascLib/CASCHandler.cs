using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class IndexEntry
    {
        public int Index;
        public int Offset;
        public int Size;
    }

    public class CASCHandler
    {
        private LocalIndexHandler LocalIndex;
        private CDNIndexHandler CDNIndex;

        private EncodingHandler EncodingHandler;
        private DownloadHandler DownloadHandler;
        private RootHandlerBase RootHandler;
        private InstallHandler InstallHandler;

        private static readonly Jenkins96 Hasher = new Jenkins96();

        private readonly Dictionary<int, Stream> DataStreams = new Dictionary<int, Stream>();

        public EncodingHandler Encoding { get { return EncodingHandler; } }
        public DownloadHandler Download { get { return DownloadHandler; } }
        public RootHandlerBase Root { get { return RootHandler; } }
        public InstallHandler Install { get { return InstallHandler; } }

        public CASCConfig Config { get; private set; }

        private CASCHandler(CASCConfig config, BackgroundWorkerEx worker)
        {
            Config = config;

            Logger.WriteLine("CASCHandler: loading CDN indices...");

            using (var _ = new PerfCounter("CDNIndexHandler.Initialize()"))
            {
                CDNIndex = CDNIndexHandler.Initialize(config, worker);
            }

            Logger.WriteLine("CASCHandler: loaded {0} CDN indexes", CDNIndex.Count);

            if (!config.OnlineMode)
            {
                CDNIndexHandler.Cache.Enabled = false;

                Logger.WriteLine("CASCHandler: loading local indices...");

                using (var _ = new PerfCounter("LocalIndexHandler.Initialize()"))
                {
                    LocalIndex = LocalIndexHandler.Initialize(config, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} local indexes", LocalIndex.Count);
            }

            Logger.WriteLine("CASCHandler: loading encoding data...");

            using (var _ = new PerfCounter("new EncodingHandler()"))
            {
                using (var fs = OpenEncodingFile())
                    EncodingHandler = new EncodingHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandler: loaded {0} encoding data", EncodingHandler.Count);

            if ((CASCConfig.LoadFlags & LoadFlags.Download) != 0)
            {
                Logger.WriteLine("CASCHandler: loading download data...");

                using (var _ = new PerfCounter("new DownloadHandler()"))
                {
                    using (var fs = OpenDownloadFile())
                        DownloadHandler = new DownloadHandler(fs, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} download data", EncodingHandler.Count);
            }

            Logger.WriteLine("CASCHandler: loading root data...");

            using (var _ = new PerfCounter("new RootHandler()"))
            {
                using (var fs = OpenRootFile())
                {
                    if (config.GameType == CASCGameType.S2 || config.GameType == CASCGameType.HotS)
                        RootHandler = new MNDXRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.D3)
                        RootHandler = new D3RootHandler(fs, worker, this);
                    else if (config.GameType == CASCGameType.WoW)
                        RootHandler = new WowRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.Agent || config.GameType == CASCGameType.Bna)
                        RootHandler = new AgentRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.Hearthstone)
                        RootHandler = new HSRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.Overwatch)
                        RootHandler = new OWRootHandler(fs, worker, this);
                    else
                        throw new Exception("Unsupported game " + config.BuildUID);
                }
            }

            Logger.WriteLine("CASCHandler: loaded {0} root data", RootHandler.Count);

            if ((CASCConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                Logger.WriteLine("CASCHandler: loading install data...");

                using (var _ = new PerfCounter("new InstallHandler()"))
                {
                    using (var fs = OpenInstallFile())
                        InstallHandler = new InstallHandler(fs, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} install data", InstallHandler.Count);
            }
        }

        private BinaryReader OpenInstallFile()
        {
            var encInfo = EncodingHandler.GetEntry(Config.InstallMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for install file missing!");

            //ExtractFile(encInfo.Key, ".", "install");

            return new BinaryReader(OpenFile(encInfo.Key));
        }

        private BinaryReader OpenDownloadFile()
        {
            var encInfo = EncodingHandler.GetEntry(Config.DownloadMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for download file missing!");

            //ExtractFile(encInfo.Key, ".", "download");

            return new BinaryReader(OpenFile(encInfo.Key));
        }

        private BinaryReader OpenRootFile()
        {
            var encInfo = EncodingHandler.GetEntry(Config.RootMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for root file missing!");

            //ExtractFile(encInfo.Key, ".", "root");

            return new BinaryReader(OpenFile(encInfo.Key));
        }

        private BinaryReader OpenEncodingFile()
        {
            //ExtractFile(Config.EncodingKey, ".", "encoding");

            return new BinaryReader(OpenFile(Config.EncodingKey));
        }

        public Stream OpenFile(byte[] key)
        {
            try
            {
                if (Config.OnlineMode)
                    return OpenFileOnline(key);
                else
                    return OpenFileLocal(key);
            }
            catch
            {
                return OpenFileOnline(key);
            }
        }

        private Stream OpenFileOnline(byte[] key)
        {
            IndexEntry idxInfo = CDNIndex.GetIndexInfo(key);

            if (idxInfo != null)
            {
                using (Stream s = CDNIndex.OpenDataFile(idxInfo))
                using (BLTEHandler blte = new BLTEHandler(s, key))
                {
                    return blte.OpenFile(true);
                }
            }
            else
            {
                using (Stream s = CDNIndex.OpenDataFileDirect(key))
                using (BLTEHandler blte = new BLTEHandler(s, key))
                {
                    return blte.OpenFile(true);
                }
            }
        }

        private Stream OpenFileLocal(byte[] key)
        {
            Stream stream = GetLocalIndexData(key);

            using (BLTEHandler blte = new BLTEHandler(stream, key))
            {
                return blte.OpenFile(true);
            }
        }

        private Stream GetLocalIndexData(byte[] key)
        {
            IndexEntry idxInfo = LocalIndex.GetIndexInfo(key);

            if (idxInfo == null)
                throw new Exception("local index missing");

            Stream dataStream = GetDataStream(idxInfo.Index);
            dataStream.Position = idxInfo.Offset;

            using (BinaryReader reader = new BinaryReader(dataStream, System.Text.Encoding.ASCII, true))
            {
                byte[] md5 = reader.ReadBytes(16);
                Array.Reverse(md5);

                if (!md5.EqualsTo(key))
                    throw new Exception("local data corrupted");

                int size = reader.ReadInt32();

                if (size != idxInfo.Size)
                    throw new Exception("local data corrupted");

                //byte[] unkData1 = reader.ReadBytes(2);
                //byte[] unkData2 = reader.ReadBytes(8);
                dataStream.Position += 10;

                byte[] data = reader.ReadBytes(idxInfo.Size - 30);

                return new MemoryStream(data);
            }
        }

        public void ExtractFile(byte[] key, string path, string name)
        {
            try
            {
                if (Config.OnlineMode)
                    ExtractFileOnline(key, path, name);
                else
                    ExtractFileLocal(key, path, name);
            }
            catch
            {
                ExtractFileOnline(key, path, name);
            }
        }

        private void ExtractFileOnline(byte[] key, string path, string name)
        {
            IndexEntry idxInfo = CDNIndex.GetIndexInfo(key);

            if (idxInfo != null)
            {
                using (Stream s = CDNIndex.OpenDataFile(idxInfo))
                using (BLTEHandler blte = new BLTEHandler(s, key))
                {
                    blte.ExtractToFile(path, name);
                }
            }
            else
            {
                using (Stream s = CDNIndex.OpenDataFileDirect(key))
                using (BLTEHandler blte = new BLTEHandler(s, key))
                {
                    blte.ExtractToFile(path, name);
                }
            }
        }

        private void ExtractFileLocal(byte[] key, string path, string name)
        {
            Stream stream = GetLocalIndexData(key);

            using (BLTEHandler blte = new BLTEHandler(stream, key))
            {
                blte.ExtractToFile(path, name);
            }
        }

        private Stream GetDataStream(int index)
        {
            Stream stream;

            if (DataStreams.TryGetValue(index, out stream))
                return stream;

            string dataFolder = CASCGame.GetDataFolder(Config.GameType);

            string dataFile = Path.Combine(Config.BasePath, dataFolder, "data", string.Format("data.{0:D3}", index));

            stream = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            DataStreams[index] = stream;

            return stream;
        }

        public static CASCHandler OpenStorage(CASCConfig config, BackgroundWorkerEx worker = null)
        {
            return Open(worker, config);
        }

        public static CASCHandler OpenLocalStorage(string basePath, BackgroundWorkerEx worker = null)
        {
            CASCConfig config = CASCConfig.LoadLocalStorageConfig(basePath);

            return Open(worker, config);
        }

        public static CASCHandler OpenOnlineStorage(string product, string region = "us", BackgroundWorkerEx worker = null)
        {
            CASCConfig config = CASCConfig.LoadOnlineStorageConfig(product, region);

            return Open(worker, config);
        }

        private static CASCHandler Open(BackgroundWorkerEx worker, CASCConfig config)
        {
            using (var _ = new PerfCounter("new CASCHandler()"))
            {
                return new CASCHandler(config, worker);
            }
        }

        public bool FileExists(int fileDataId)
        {
            WowRootHandler rh = Root as WowRootHandler;

            if (rh != null)
            {
                var hash = rh.GetHashByFileDataId(fileDataId);

                return FileExists(hash);
            }

            return false;
        }

        public bool FileExists(string file)
        {
            var hash = Hasher.ComputeHash(file);
            return FileExists(hash);
        }

        public bool FileExists(ulong hash)
        {
            var rootInfos = RootHandler.GetAllEntries(hash);
            return rootInfos != null && rootInfos.Any();
        }

        public EncodingEntry GetEncodingEntry(ulong hash)
        {
            var rootInfos = RootHandler.GetEntries(hash);
            if (rootInfos.Any())
                return EncodingHandler.GetEntry(rootInfos.First().MD5);

            if ((CASCConfig.LoadFlags & LoadFlags.Install) != 0)
            {
                var installInfos = Install.GetEntries().Where(e => Hasher.ComputeHash(e.Name) == hash);
                if (installInfos.Any())
                    return EncodingHandler.GetEntry(installInfos.First().MD5);
            }

            return null;
        }

        public Stream OpenFile(int fileDataId)
        {
            WowRootHandler rh = Root as WowRootHandler;

            if (rh != null)
            {
                var hash = rh.GetHashByFileDataId(fileDataId);

                return OpenFile(hash, "FileData_" + fileDataId.ToString());
            }

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException("FileData: " + fileDataId.ToString());
            return null;
        }

        public Stream OpenFile(string fullName)
        {
            var hash = Hasher.ComputeHash(fullName);

            return OpenFile(hash, fullName);
        }

        public Stream OpenFile(ulong hash, string fullName)
        {
            EncodingEntry encInfo = GetEncodingEntry(hash);

            if (encInfo != null)
                return OpenFile(encInfo.Key);

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException(fullName);
            return null;
        }

        public void SaveFileTo(string fullName, string extractPath)
        {
            var hash = Hasher.ComputeHash(fullName);

            SaveFileTo(hash, extractPath, fullName);
        }

        public void SaveFileTo(ulong hash, string extractPath, string fullName)
        {
            EncodingEntry encInfo = GetEncodingEntry(hash);

            if (encInfo != null)
            {
                ExtractFile(encInfo.Key, extractPath, fullName);
                return;
            }

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException(fullName);
        }

        public void Clear()
        {
            CDNIndex.Clear();
            CDNIndex = null;

            foreach (var stream in DataStreams)
                stream.Value.Close();

            DataStreams.Clear();

            EncodingHandler.Clear();
            EncodingHandler = null;

            if (InstallHandler != null)
            {
                InstallHandler.Clear();
                InstallHandler = null;
            }

            if (LocalIndex != null)
            {
                LocalIndex.Clear();
                LocalIndex = null;
            }

            RootHandler.Clear();
            RootHandler = null;

            if (DownloadHandler != null)
            {
                DownloadHandler.Clear();
                DownloadHandler = null;
            }
        }
    }
}
