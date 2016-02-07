using System;
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

    public sealed class CASCHandler : CASCHandlerBase
    {
        private EncodingHandler EncodingHandler;
        private DownloadHandler DownloadHandler;
        private RootHandlerBase RootHandler;
        private InstallHandler InstallHandler;

        public EncodingHandler Encoding { get { return EncodingHandler; } }
        public DownloadHandler Download { get { return DownloadHandler; } }
        public RootHandlerBase Root { get { return RootHandler; } }
        public InstallHandler Install { get { return InstallHandler; } }

        private CASCHandler(CASCConfig config, BackgroundWorkerEx worker) : base(config, worker)
        {
            Logger.WriteLine("CASCHandler: loading encoding data...");

            using (var _ = new PerfCounter("new EncodingHandler()"))
            {
                using (var fs = OpenEncodingFile(this))
                    EncodingHandler = new EncodingHandler(fs, worker);
            }

            Logger.WriteLine("CASCHandler: loaded {0} encoding data", EncodingHandler.Count);

            if ((CASCConfig.LoadFlags & LoadFlags.Download) != 0)
            {
                Logger.WriteLine("CASCHandler: loading download data...");

                using (var _ = new PerfCounter("new DownloadHandler()"))
                {
                    using (var fs = OpenDownloadFile(EncodingHandler, this))
                        DownloadHandler = new DownloadHandler(fs, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} download data", EncodingHandler.Count);
            }

            Logger.WriteLine("CASCHandler: loading root data...");

            using (var _ = new PerfCounter("new RootHandler()"))
            {
                using (var fs = OpenRootFile(EncodingHandler, this))
                {
                    if (config.GameType == CASCGameType.S2 || config.GameType == CASCGameType.HotS)
                        RootHandler = new MNDXRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.D3)
                        RootHandler = new D3RootHandler(fs, worker, this);
                    else if (config.GameType == CASCGameType.WoW)
                        RootHandler = new WowRootHandler(fs, worker);
                    else if (config.GameType == CASCGameType.Agent || config.GameType == CASCGameType.Bna || config.GameType == CASCGameType.Client)
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
                    using (var fs = OpenInstallFile(EncodingHandler, this))
                        InstallHandler = new InstallHandler(fs, worker);
                }

                Logger.WriteLine("CASCHandler: loaded {0} install data", InstallHandler.Count);
            }
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

        public override Stream OpenFile(int fileDataId)
        {
            WowRootHandler rh = Root as WowRootHandler;

            if (rh != null)
            {
                var hash = rh.GetHashByFileDataId(fileDataId);

                return OpenFile(hash);
            }

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException("FileData: " + fileDataId.ToString());
            return null;
        }

        public override Stream OpenFile(string name)
        {
            var hash = Hasher.ComputeHash(name);

            return OpenFile(hash);
        }

        public override Stream OpenFile(ulong hash)
        {
            EncodingEntry encInfo = GetEncodingEntry(hash);

            if (encInfo != null)
                return OpenFile(encInfo.Key);

            if (CASCConfig.ThrowOnFileNotFound)
                throw new FileNotFoundException(string.Format("{0:X16}", hash));
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
