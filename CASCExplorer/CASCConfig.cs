using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CASCExplorer
{
    class KeyValueConfig
    {
        Dictionary<string, List<string>> Data = new Dictionary<string, List<string>>();

        public KeyValueConfig(string config)
        {
            using (var sr = new StreamReader(config))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
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

        public VerBarConfig(string config)
        {
            using (var sr = new StreamReader(config))
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
                        if (Data.Count != tokens.Length)
                            throw new Exception("VerBarConfig: Data.Count != tokens.Length");

                        for (int i = 0; i < Data.Count; ++i)
                        {
                            Data.ElementAt(i).Value.Add(tokens[i]);
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

        public static VerBarConfig BuildInfo
        {
            get { return _BuildInfo; }
        }

        public static KeyValueConfig BuildConfig
        {
            get { return _BuildConfig; }
        }

        public static KeyValueConfig CDNConfig
        {
            get { return _CDNConfig; }
        }

        public static void Load()
        {
            string wowPath = Properties.Settings.Default.WowPath;

            string buildInfoPath = Path.Combine(wowPath, ".build.info");

            _BuildInfo = new VerBarConfig(buildInfoPath);

            // Build Configuration
            string buildKey = BuildInfo["Build Key"][0];

            string buildCfgPath = Path.Combine(wowPath, "Data\\config\\", buildKey.Substring(0, 2), buildKey.Substring(2, 2), buildKey);

            _BuildConfig = new KeyValueConfig(buildCfgPath);

            // CDN Configuration 
            string cdnKey = BuildInfo["CDN Key"][0];

            string cdnCfgPath = Path.Combine(wowPath, "Data\\config\\", cdnKey.Substring(0, 2), cdnKey.Substring(2, 2), cdnKey);

            _CDNConfig = new KeyValueConfig(cdnCfgPath);
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
            get { return String.Format("http://{0}{1}", BuildInfo["CDN Hosts"][0], BuildInfo["CDN Path"][0]); }
        }
    }
}
