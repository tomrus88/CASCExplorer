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
            this.Config = config;

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

            Logger.WriteLine("CASCHandler: loading download data...");

            using (var _ = new PerfCounter("new DownloadHandler()"))
            {
                using (var fs = OpenDownloadFile())
                    DownloadHandler = new DownloadHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandler: loaded {0} download data", EncodingHandler.Count);

            Logger.WriteLine("CASCHandler: loading root data...");

            using (var _ = new PerfCounter("new RootHandler()"))
            {
                using (var fs = OpenRootFile())
                {
                    byte[] magic = fs.ReadBytes(4);
                    fs.Position = 0;

                    if (magic[0] == 0x4D && magic[1] == 0x4E && magic[2] == 0x44 && magic[3] == 0x58) // MNDX
                    {
                        RootHandler = new MNDXRootHandler(fs, worker);
                    }
                    else if (config.BuildUID.StartsWith("d3", StringComparison.OrdinalIgnoreCase))
                    {
                        RootHandler = new D3RootHandler(fs, worker, this);
                    }
                    else if (config.BuildUID.StartsWith("wow", StringComparison.OrdinalIgnoreCase))
                    {
                        RootHandler = new WowRootHandler(fs, worker);
                    }
                    else if (config.BuildUID.StartsWith("agent", StringComparison.OrdinalIgnoreCase))
                    {
                        RootHandler = new AgentRootHandler(fs, worker);
                    }
                    else if (config.BuildUID.StartsWith("hsb", StringComparison.OrdinalIgnoreCase))
                    {
                        RootHandler = new HSRootHandler(fs, worker);
                    }
                    else if (config.BuildUID.StartsWith("pro", StringComparison.OrdinalIgnoreCase))
                    {
                        RootHandler = new OWRootHandler(fs, worker, this);
                    }
                    else
                    {
                        throw new Exception("Unsupported game " + config.BuildUID);
                    }
                }
            }

            Logger.WriteLine("CASCHandler: loaded {0} root data", RootHandler.Count);

            Logger.WriteLine("CASCHandler: loading install data...");

            using (var _ = new PerfCounter("new InstallHandler()"))
            {
                using (var fs = OpenInstallFile())
                    InstallHandler = new InstallHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandler: loaded {0} install data", InstallHandler.Count);
        }

        private MMStream OpenInstallFile()
        {
            var encInfo = EncodingHandler.GetEntry(Config.InstallMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for install file missing!");

            //ExtractFile(encInfo.Key, ".", "install");

            return new MMStream(OpenFile(encInfo.Key));
        }

        private MMStream OpenDownloadFile()
        {
            var encInfo = EncodingHandler.GetEntry(Config.DownloadMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for download file missing!");

            //ExtractFile(encInfo.Key, ".", "download");

            return new MMStream(OpenFile(encInfo.Key));
        }

        private MMStream OpenRootFile()
        {
            var encInfo = EncodingHandler.GetEntry(Config.RootMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for root file missing!");

            //ExtractFile(encInfo.Key, ".", "root");

            return new MMStream(OpenFile(encInfo.Key));
        }

        private MMStream OpenEncodingFile()
        {
            //ExtractFile(Config.EncodingKey, ".", "encoding");

            return new MMStream(OpenFile(Config.EncodingKey));
        }

        public Stream OpenFile(byte[] key)
        {
            try
            {
                if (Config.OnlineMode)
                    throw new Exception("OnlineMode=true");

                var idxInfo = LocalIndex.GetIndexInfo(key);

                if (idxInfo == null)
                    throw new Exception("local index missing");

                var stream = GetDataStream(idxInfo.Index);

                stream.Position = idxInfo.Offset;

                stream.Position += 30;
                //byte[] compressedMD5 = reader.ReadBytes(16);
                //int size = reader.ReadInt32();
                //byte[] unkData1 = reader.ReadBytes(2);
                //byte[] unkData2 = reader.ReadBytes(8);

                using (BLTEHandler blte = new BLTEHandler(stream, idxInfo.Size - 30))
                {
                    return blte.OpenFile();
                }
            }
            catch
            {
                var idxInfo = CDNIndex.GetIndexInfo(key);

                if (idxInfo != null)
                {
                    using (Stream s = CDNIndex.OpenDataFile(idxInfo))
                    using (BLTEHandler blte = new BLTEHandler(s, idxInfo.Size))
                    {
                        return blte.OpenFile();
                    }
                }
                else
                {
                    try
                    {
                        using (Stream s = CDNIndex.OpenDataFileDirect(key))
                        using (BLTEHandler blte = new BLTEHandler(s, (int)s.Length))
                        {
                            return blte.OpenFile();
                        }
                    }
                    catch
                    {
                        throw new Exception("CDN index missing");
                    }
                }
            }
        }

        public void ExtractFile(byte[] key, string path, string name)
        {
            try
            {
                if (Config.OnlineMode)
                    throw new Exception("OnlineMode=true");

                var idxInfo = LocalIndex.GetIndexInfo(key);

                if (idxInfo == null)
                    throw new Exception("local index missing");

                var stream = GetDataStream(idxInfo.Index);

                stream.Position = idxInfo.Offset;

                stream.Position += 30;
                //byte[] compressedMD5 = reader.ReadBytes(16);
                //int size = reader.ReadInt32();
                //byte[] unkData1 = reader.ReadBytes(2);
                //byte[] unkData2 = reader.ReadBytes(8);

                using (BLTEHandler blte = new BLTEHandler(stream, idxInfo.Size - 30))
                {
                    blte.ExtractToFile(path, name);
                }
            }
            catch
            {
                var idxInfo = CDNIndex.GetIndexInfo(key);

                if (idxInfo != null)
                {
                    using (Stream s = CDNIndex.OpenDataFile(idxInfo))
                    using (BLTEHandler blte = new BLTEHandler(s, idxInfo.Size))
                    {
                        blte.ExtractToFile(path, name);
                    }
                }
                else
                {
                    try
                    {
                        using (Stream s = CDNIndex.OpenDataFileDirect(key))
                        using (BLTEHandler blte = new BLTEHandler(s, (int)s.Length))
                        {
                            blte.ExtractToFile(path, name);
                        }
                    }
                    catch
                    {
                        throw new Exception("CDN index missing");
                    }
                }
            }
        }

        ~CASCHandler()
        {
            foreach (var stream in DataStreams)
                stream.Value.Close();
        }

        private Stream GetDataStream(int index)
        {
            Stream stream;
            if (DataStreams.TryGetValue(index, out stream))
                return stream;

            string dataFolder = CASCGame.GetDataFolder(Config.GameType);

            string dataFile = Path.Combine(Config.BasePath, String.Format("{0}\\data\\data.{1:D3}", dataFolder, index));

            stream = new FileStream(dataFile, FileMode.Open);

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

        public static CASCHandler OpenOnlineStorage(string product, BackgroundWorkerEx worker = null)
        {
            CASCConfig config = CASCConfig.LoadOnlineStorageConfig(product, "us");

            return Open(worker, config);
        }

        private static CASCHandler Open(BackgroundWorkerEx worker, CASCConfig config)
        {
            using (var _ = new PerfCounter("new CASCHandler()"))
            {
                return new CASCHandler(config, worker);
            }
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

            var installInfos = Install.GetEntries().Where(e => Hasher.ComputeHash(e.Name) == hash);
            if (installInfos.Any())
                return EncodingHandler.GetEntry(installInfos.First().MD5);

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

            throw new FileNotFoundException(fullName);
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

            throw new FileNotFoundException(fullName);
        }

        public void Clear()
        {
            CDNIndex.Clear();
            CDNIndex = null;

            DataStreams.Clear();

            EncodingHandler.Clear();
            EncodingHandler = null;

            InstallHandler.Clear();
            InstallHandler = null;

            if (LocalIndex != null)
            {
                LocalIndex.Clear();
                LocalIndex = null;
            }

            RootHandler.Clear();
            RootHandler = null;
        }
    }
}
