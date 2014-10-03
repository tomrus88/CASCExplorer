using System;
using System.Collections.Generic;
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
        private readonly LocalIndexHandler LocalIndex;
        private readonly CDNIndexHandler CDNIndex;

        private readonly EncodingHandler EncodingHandler;
        private readonly WowRootHandler RootHandler;

        private static readonly Jenkins96 Hasher = new Jenkins96();

        private readonly Dictionary<int, FileStream> DataStreams = new Dictionary<int, FileStream>();

        private readonly CASCConfig config;

        public EncodingHandler Encoding { get { return EncodingHandler; } }
        public WowRootHandler Root { get { return RootHandler; } }

        private CASCHandler(CASCConfig config, AsyncAction worker)
        {
            this.config = config;

            CDNIndex = CDNIndexHandler.Initialize(config, worker);

            if (!config.OnlineMode)
                LocalIndex = LocalIndexHandler.Initialize(config, worker);

            using (var fs = OpenEncodingFile())
                EncodingHandler = new EncodingHandler(fs, worker);

            Logger.WriteLine("CASCHandler: loaded {0} encoding data", EncodingHandler.Count);

            using (var fs = OpenRootFile())
                RootHandler = new WowRootHandler(fs, worker);

            Logger.WriteLine("CASCHandler: loaded {0} root data", RootHandler.Count);
        }

        private Stream OpenRootFile()
        {
            var encInfo = EncodingHandler.GetEntry(config.RootMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for root file missing!");

            Stream s = TryLocalCache(encInfo.Key, config.RootMD5, Path.Combine("data", config.Build.ToString(), "root"));

            if (s != null)
                return s;

            s = TryLocalCache(encInfo.Key, config.RootMD5, Path.Combine("data", config.Build.ToString(), "root"));

            if (s != null)
                return s;

            return OpenFile(encInfo.Key);
        }

        private Stream OpenEncodingFile()
        {
            Stream s = TryLocalCache(config.EncodingKey, config.EncodingMD5, Path.Combine("data", config.Build.ToString(), "encoding"));

            if (s != null)
                return s;

            s = TryLocalCache(config.EncodingKey, config.EncodingMD5, Path.Combine("data", config.Build.ToString(), "encoding"));

            if (s != null)
                return s;

            return OpenFile(config.EncodingKey);
        }

        private Stream TryLocalCache(byte[] key, byte[] md5, string name)
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

        private Stream OpenFile(byte[] key)
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
                //blte.ExtractToFile(".", key.ToHexString());
                //int __size = reader.ReadInt32();
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

        private void ExtractFile(byte[] key, string path, string name)
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
                //byte[] unkHash = reader.ReadBytes(16);
                //int __size = reader.ReadInt32();
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

            string dataFile = Path.Combine(config.BasePath, String.Format("Data\\data\\data.{0:D3}", index));

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
            CASCConfig config = CASCConfig.LoadOnlineStorageConfig(product);

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
            var rootInfos = RootHandler.GetEntries(hash);
            return rootInfos != null && rootInfos.Count > 0;
        }

        private EncodingEntry GetEncodingEntry(ulong hash, LocaleFlags locale, ContentFlags content)
        {
            var rootInfos = RootHandler.GetEntries(hash);

            var rootInfosLocale = rootInfos.Where(re => (re.Block.LocaleFlags & locale) != 0);

            if (rootInfosLocale.Count() > 1)
            {
                if (content != ContentFlags.None)
                {
                    var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags & content) == 0);

                    if (rootInfosLocaleAndContent.Any())
                        rootInfosLocale = rootInfosLocaleAndContent;
                }
            }

            return EncodingHandler.GetEntry(rootInfosLocale.First().MD5);
        }

        private EncodingEntry GetEncodingEntryOld(ulong hash, LocaleFlags locale, ContentFlags content)
        {
            var rootInfos = RootHandler.GetEntries(hash);

            foreach (var rootInfo in rootInfos)
            {
                if ((rootInfo.Block.LocaleFlags & locale) != 0 && (rootInfo.Block.ContentFlags & content) == 0)
                {
                    var encInfo = EncodingHandler.GetEntry(rootInfo.MD5);

                    if (encInfo != null)
                        return encInfo;
                }
            }

            return null;
        }

        public Stream OpenFile(string fullName, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            var hash = Hasher.ComputeHash(fullName);

            return OpenFile(hash, fullName, locale, content);
        }

        public Stream OpenFile(ulong hash, string fullName, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            EncodingEntry encInfo = GetEncodingEntry(hash, locale, content);

            if (encInfo != null)
                return OpenFile(encInfo.Key);

            throw new FileNotFoundException(fullName);
        }

        public void SaveFileTo(string fullName, string extractPath, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            var hash = Hasher.ComputeHash(fullName);

            SaveFileTo(hash, fullName, extractPath, locale, content);
        }

        public void SaveFileTo(ulong hash, string fullName, string extractPath, LocaleFlags locale, ContentFlags content = ContentFlags.None)
        {
            EncodingEntry encInfo = GetEncodingEntry(hash, locale, content);

            if (encInfo != null)
            {
                ExtractFile(encInfo.Key, extractPath, fullName);
                return;
            }

            throw new FileNotFoundException(fullName);
        }
    }
}
