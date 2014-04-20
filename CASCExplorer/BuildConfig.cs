using System;
using System.IO;

namespace CASCExplorer
{
    class BuildConfig
    {
        static byte[] encodingKey;
        static byte[] rootMD5;

        public static void Load(string wowPath)
        {
            using (var sr = new StreamReader(Path.Combine(wowPath, ".build.info")))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains("Build Key"))
                        continue;

                    string[] tokens = line.Split('|');

                    if (tokens.Length != 12)
                        throw new Exception("BuildConfig::Load: tokens.Length != 12");

                    string buildConfig = Path.Combine(wowPath, "Data\\config\\", tokens[2].Substring(0, 2), tokens[2].Substring(2, 2), tokens[2]);

                    using (var sr2 = new StreamReader(buildConfig))
                    {
                        while ((line = sr2.ReadLine()) != null)
                        {
                            if (line.StartsWith("encoding"))
                            {
                                string[] tokens2 = line.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                                encodingKey = tokens2[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1].ToByteArray();
                            }
                            else if (line.StartsWith("root"))
                            {
                                string[] tokens2 = line.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                                rootMD5 = tokens2[1].ToByteArray();
                            }
                        }
                    }
                }
            }
        }

        public static byte[] EncodingKey
        {
            get { return encodingKey; }
        }

        public static byte[] RootMD5
        {
            get { return rootMD5; }
        }
    }
}
