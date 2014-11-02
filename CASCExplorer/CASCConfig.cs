using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    class KeyValueConfig
    {
        Dictionary<string, List<string>> Data = new Dictionary<string, List<string>>();

        public KeyValueConfig(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    if (line == string.Empty) // skip empty lines
                        continue;

                    if (line.StartsWith("#")) // skip comments
                        continue;

                    string[] tokens = line.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                    if (tokens.Length != 2)
                        throw new Exception("KeyValueConfig: tokens.Length != 2");

                    var values = tokens[1].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var valuesList = new List<string>();
                    valuesList.AddRange(values);
                    Data.Add(tokens[0].Trim(), valuesList);
                }
            }
        }

        public List<string> this[string key]
        {
            get { return Data[key]; }
        }
    }

    class VerBarConfig
    {
        Dictionary<string, List<string>> Data = new Dictionary<string, List<string>>();

        public VerBarConfig(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                string line;

                int lineNum = 0;

                while ((line = sr.ReadLine()) != null)
                {
                    string[] tokens = line.Split(new char[] { '|' });

                    if (lineNum == 0) // keys
                    {
                        foreach (var token in tokens)
                        {
                            var tokens2 = token.Split(new char[] { '!' });
                            Data[tokens2[0]] = new List<string>();
                        }
                    }
                    else // values
                    {
                        //if (Data.Count != tokens.Length)
                        //    throw new Exception("VerBarConfig: Data.Count != tokens.Length");
                        if (Data.Count != tokens.Length)
                            continue;

                        for (int i = 0; i < Data.Count; ++i)
                        {
                            var element = Data.ElementAt(i);

                            switch (element.Key)
                            {
                                case "CDN Hosts":
                                    var cdnHosts = tokens[i].Split(' ');
                                    foreach (var cdnHost in cdnHosts)
                                        element.Value.Add(cdnHost);
                                    break;
                                default:
                                    element.Value.Add(tokens[i]);
                                    break;
                            }
                        }
                    }

                    lineNum++;
                }
            }
        }

        public List<string> this[string key]
        {
            get { return Data[key]; }
        }
    }

    class CASCConfig
    {
        static VerBarConfig _BuildInfo;
        static KeyValueConfig _BuildConfig;
        static KeyValueConfig _CDNConfig;

        static VerBarConfig _CDNData;
        static VerBarConfig _VersionsData;

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
                    _CDNData = new VerBarConfig(cdnsStream);

                using (var versionsStream = CDNHandler.OpenFileDirect("http://us.patch.battle.net/wow_beta/versions"))
                    _VersionsData = new VerBarConfig(versionsStream);

                string buildKey = _VersionsData["BuildConfig"][0];
                using (Stream buildConfigStream = CDNHandler.OpenConfigFileDirect(buildKey))
                    _BuildConfig = new KeyValueConfig(buildConfigStream);

                string cdnKey = _VersionsData["CDNConfig"][0];
                using (Stream CDNConfigStream = CDNHandler.OpenConfigFileDirect(cdnKey))
                    _CDNConfig = new KeyValueConfig(CDNConfigStream);
            }
            else
            {
                string buildInfoPath = Path.Combine(wowPath, ".build.info");

                using (Stream buildInfoStream = new FileStream(buildInfoPath, FileMode.Open))
                    _BuildInfo = new VerBarConfig(buildInfoStream);

                string buildKey = _BuildInfo["Build Key"][0];
                string buildCfgPath = Path.Combine(wowPath, "Data\\config\\", buildKey.Substring(0, 2), buildKey.Substring(2, 2), buildKey);
                using (Stream buildConfigStream = new FileStream(buildCfgPath, FileMode.Open))
                    _BuildConfig = new KeyValueConfig(buildConfigStream);

                string cdnKey = _BuildInfo["CDN Key"][0];
                string cdnCfgPath = Path.Combine(wowPath, "Data\\config\\", cdnKey.Substring(0, 2), cdnKey.Substring(2, 2), cdnKey);
                using (Stream CDNConfigStream = new FileStream(cdnCfgPath, FileMode.Open))
                    _CDNConfig = new KeyValueConfig(CDNConfigStream);
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
