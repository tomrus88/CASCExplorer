using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CASCExplorer
{
    internal class IndexEntry
    {
        public int Index;
        public int Offset;
        public int Size;
    }

    public class CASCHandler
    {
        private LocalIndexHandler LocalIndex;
        private CDNIndexHandler CDNIndex;

        private InstallHandler InstallHandler;
        private EncodingHandler EncodingHandler;
        private IRootHandler RootHandler;

        private static readonly Jenkins96 Hasher = new Jenkins96();

        private readonly Dictionary<int, FileStream> DataStreams = new Dictionary<int, FileStream>();

        private readonly CASCConfig config;

        public InstallHandler Install { get { return InstallHandler; } }
        public EncodingHandler Encoding { get { return EncodingHandler; } }
        public IRootHandler Root { get { return RootHandler; } }
        public CASCConfig Config { get { return config; } }

        private CASCHandler(CASCConfig config, AsyncAction worker)
        {
            this.config = config;

            Logger.WriteLine("CASCHandler: loading CDN indices...");

            Stopwatch sw = new Stopwatch();

            sw.Restart();
            CDNIndex = CDNIndexHandler.Initialize(config, worker);
            sw.Stop();

            Logger.WriteLine("CASCHandler: loaded {0} CDN indexes in {1}", CDNIndex.Count, sw.Elapsed);

            if (!config.OnlineMode)
            {
                Logger.WriteLine("CASCHandler: loading local indices...");

                sw.Restart();
                LocalIndex = LocalIndexHandler.Initialize(config, worker);
                sw.Stop();

                Logger.WriteLine("CASCHandler: loaded {0} local indexes in {1}", LocalIndex.Count, sw.Elapsed);
            }

            Logger.WriteLine("CASCHandler: loading encoding data...");

            sw.Restart();
            using (var fs = OpenEncodingFile())
                EncodingHandler = new EncodingHandler(fs, worker);
            sw.Stop();

            Logger.WriteLine("CASCHandler: loaded {0} encoding data in {1}", EncodingHandler.Count, sw.Elapsed);

            Logger.WriteLine("CASCHandler: loading install data...");

            sw.Restart();
            using (var fs = OpenInstallFile())
                InstallHandler = new InstallHandler(fs, worker);
            sw.Stop();

            Logger.WriteLine("CASCHandler: loaded {0} install data in {1}", InstallHandler.Count, sw.Elapsed);

            Logger.WriteLine("CASCHandler: loading root data...");

            sw.Restart();
            using (var fs = OpenRootFile())
            {
                byte[] magic = new byte[4];
                fs.Read(magic, 0, magic.Length);
                fs.Position = 0;

                if (magic[0] == 0x4D && magic[1] == 0x4E && magic[2] == 0x44 && magic[3] == 0x58) // MNDX
                {
                    RootHandler = new MNDXRootHandler(fs, worker);
                }
                else if (config.BuildUID.StartsWith("d3"))
                {
                    RootHandler = new D3RootHandler(fs, worker, this);
                }
                else
                {
                    RootHandler = new WowRootHandler(fs, worker);
                }
            }
            sw.Stop();

            Logger.WriteLine("CASCHandler: loaded {0} root data in {1}", RootHandler.Count, sw.Elapsed);
        }

        private Stream OpenInstallFile()
        {
            var encInfo = EncodingHandler.GetEntry(config.InstallMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for install file missing!");

            Stream s = TryLocalCache(encInfo.Key, config.InstallMD5, Path.Combine("data", config.BuildName, "install"));

            if (s != null)
                return s;

            s = TryLocalCache(encInfo.Key, config.InstallMD5, Path.Combine("data", config.BuildName, "install"));

            if (s != null)
                return s;

            return OpenFile(encInfo.Key);
        }

        private Stream OpenRootFile()
        {
            var encInfo = EncodingHandler.GetEntry(config.RootMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for root file missing!");

            Stream s = TryLocalCache(encInfo.Key, config.RootMD5, Path.Combine("data", config.BuildName, "root"));

            if (s != null)
                return s;

            s = TryLocalCache(encInfo.Key, config.RootMD5, Path.Combine("data", config.BuildName, "root"));

            if (s != null)
                return s;

            return OpenFile(encInfo.Key);
        }

        private Stream OpenEncodingFile()
        {
            Stream s = TryLocalCache(config.EncodingKey, config.EncodingMD5, Path.Combine("data", config.BuildName, "encoding"));

            if (s != null)
                return s;

            s = TryLocalCache(config.EncodingKey, config.EncodingMD5, Path.Combine("data", config.BuildName, "encoding"));

            if (s != null)
                return s;

            return OpenFile(config.EncodingKey);
        }

        public Stream TryLocalCache(byte[] key, byte[] md5, string name)
        {
            if (File.Exists(name))
            {
                var fs = File.OpenRead(name);

                if (MD5.Create().ComputeHash(fs).EqualsTo(md5))
                {
                    fs.Position = 0;
                    return fs;
                }

                fs.Close();

                File.Delete(name);
            }

            ExtractFile(key, ".", name);

            return null;
        }

        public Stream OpenFile(byte[] key)
        {
            try
            {
                if (config.OnlineMode)
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
                    using (Stream s = CDNIndex.OpenDataFile(key))
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
                if (config.OnlineMode)
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
                    using (Stream s = CDNIndex.OpenDataFile(key))
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

        private FileStream GetDataStream(int index)
        {
            FileStream stream;
            if (DataStreams.TryGetValue(index, out stream))
                return stream;

            string dataFolder = "Data";

            if (RootHandler is MNDXRootHandler)
                dataFolder = "HeroesData";

            string dataFile = Path.Combine(config.BasePath, String.Format("{0}\\data\\data.{1:D3}", dataFolder, index));

            stream = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            DataStreams[index] = stream;

            return stream;
        }

        public static CASCHandler OpenLocalStorage(string basePath, AsyncAction worker = null)
        {
            CASCConfig config = CASCConfig.LoadLocalStorageConfig(basePath);

            return Open(worker, config);
        }

        public static CASCHandler OpenOnlineStorage(string product, AsyncAction worker = null)
        {
            CASCConfig config = CASCConfig.LoadOnlineStorageConfig(product, "us");

            return Open(worker, config);
        }

        private static CASCHandler Open(AsyncAction worker, CASCConfig config)
        {
            return new CASCHandler(config, worker);
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
            if (rootInfos == null)
                return null;
            return EncodingHandler.GetEntry(rootInfos.First().MD5);
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
