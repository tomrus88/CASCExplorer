using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    public class KeyValueConfig
    {
        private readonly Dictionary<string, List<string>> Data = new Dictionary<string, List<string>>();

        public List<string> this[string key]
        {
            get { return Data[key]; }
        }

        public static KeyValueConfig ReadKeyValueConfig(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                return ReadKeyValueConfig(sr);
            }
        }

        public static KeyValueConfig ReadKeyValueConfig(TextReader reader)
        {
            var result = new KeyValueConfig();
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("#")) // skip comments
                    continue;

                string[] tokens = line.Split(new char[] {'='}, StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length != 2)
                    throw new Exception("KeyValueConfig: tokens.Length != 2");

                var values = tokens[1].Trim().Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                var valuesList = values.ToList();
                result.Data.Add(tokens[0].Trim(), valuesList);
            }
            return result;
        }

        public static KeyValueConfig ReadVerBarConfig(Stream stream)
        {
            using (var sr = new StreamReader(stream))
                return ReadVerBarConfig(sr);
        }

        public static KeyValueConfig ReadVerBarConfig(TextReader reader)
        {
            var result = new KeyValueConfig();
            string line;

            int lineNum = 0;

            while ((line = reader.ReadLine()) != null)
            {
                string[] tokens = line.Split(new char[] {'|'});

                if (lineNum == 0) // keys
                {
                    foreach (var token in tokens)
                    {
                        var tokens2 = token.Split(new char[] {'!'});
                        result.Data[tokens2[0]] = new List<string>();
                    }
                }
                else // values
                {
                    //if (Data.Count != tokens.Length)
                    //    throw new Exception("VerBarConfig: Data.Count != tokens.Length");
                    if (result.Data.Count != tokens.Length)
                        continue;

                    for (int i = 0; i < result.Data.Count; i++)
                        result.Data.ElementAt(i).Value.Add(tokens[i]);
                }

                lineNum++;
            }

            return result;
        }
    }

    public class CASCConfig
    {
        static KeyValueConfig _BuildInfo;
        static KeyValueConfig _BuildConfig;
        static KeyValueConfig _CDNConfig;

        static KeyValueConfig _CDNData;
        static KeyValueConfig _VersionsData;

        public static KeyValueConfig BuildConfig
        {
            get { return _BuildConfig; }
        }

        public static KeyValueConfig CDNConfig
        {
            get { return _CDNConfig; }
        }

        public static void Load(bool online)
        {
            string wowPath = Properties.Settings.Default.WowPath;

            if (online)
            {
                using (var cdnsStream = CDNHandler.OpenFileDirect("http://us.patch.battle.net/wow_beta/cdns"))
                {
                    _CDNData = KeyValueConfig.ReadVerBarConfig(cdnsStream);
                }

                using (var versionsStream = CDNHandler.OpenFileDirect("http://us.patch.battle.net/wow_beta/versions"))
                {
                    _VersionsData = KeyValueConfig.ReadVerBarConfig(versionsStream);
                }

                string buildKey = _VersionsData["BuildConfig"][0];
                using (Stream buildConfigStream = CDNHandler.OpenConfigFileDirect(buildKey))
                {
                    _BuildConfig = KeyValueConfig.ReadKeyValueConfig(buildConfigStream);
                }

                string cdnKey = _VersionsData["CDNConfig"][0];
                using (Stream CDNConfigStream = CDNHandler.OpenConfigFileDirect(cdnKey))
                {
                    _CDNConfig = KeyValueConfig.ReadKeyValueConfig(CDNConfigStream);
                }
            }
            else
            {
                string buildInfoPath = Path.Combine(wowPath, ".build.info");

                using (Stream buildInfoStream = new FileStream(buildInfoPath, FileMode.Open))
                {
                    _BuildInfo = KeyValueConfig.ReadVerBarConfig(buildInfoStream);
                }

                string buildKey = _BuildInfo["Build Key"][0];
                string buildCfgPath = Path.Combine(wowPath, "Data\\config\\", buildKey.Substring(0, 2), buildKey.Substring(2, 2), buildKey);
                using (Stream buildConfigStream = new FileStream(buildCfgPath, FileMode.Open))
                {
                    _BuildConfig = KeyValueConfig.ReadKeyValueConfig(buildConfigStream);
                }

                string cdnKey = _BuildInfo["CDN Key"][0];
                string cdnCfgPath = Path.Combine(wowPath, "Data\\config\\", cdnKey.Substring(0, 2), cdnKey.Substring(2, 2), cdnKey);
                using (Stream CDNConfigStream = new FileStream(cdnCfgPath, FileMode.Open))
                {
                    _CDNConfig = KeyValueConfig.ReadKeyValueConfig(CDNConfigStream);
                }
            }
        }

        public static byte[] EncodingKey
        {
            get { return BuildConfig["encoding"][1].ToByteArray(); }
        }

        public static byte[] RootMD5
        {
            get { return BuildConfig["root"][0].ToByteArray(); }
        }

        public static string CDNUrl
        {
            get
            {
                if (CASCHandler.OnlineMode)
                    return String.Format("http://{0}/{1}", _CDNData["Hosts"][0], _CDNData["Path"][0]);
                else
                    return String.Format("http://{0}{1}", _BuildInfo["CDN Hosts"][0], _BuildInfo["CDN Path"][0]);
            }
        }
    }
}
