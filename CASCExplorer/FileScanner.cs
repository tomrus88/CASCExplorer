using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CASCExplorer
{
    class FileScanner
    {
        static readonly HashSet<string> excludeFileTypes = new HashSet<string>()
        {
            ".ogg", ".mp3", ".wav", ".avi", ".ttf", ".blp", ".sig", ".toc", ".blob", ".anim", ".skin", ".phys"
        };

        static readonly Dictionary<byte[], string> MagicNumbers = new Dictionary<byte[], string>()
        {
            { new byte[] { 0x42, 0x4c, 0x50, 0x32 }, ".blp" },
            { new byte[] { 0x4d, 0x44, 0x32, 0x30 }, ".m2" },
            { new byte[] { 0x53, 0x4b, 0x49, 0x4e }, ".skin" },
            { new byte[] { 0x57, 0x44, 0x42, 0x43 }, ".dbc" },
            { new byte[] { 0x52, 0x56, 0x58, 0x54 }, ".tex" },
            { new byte[] { 0x4f, 0x67, 0x67, 0x53 }, ".ogg" },
            { new byte[] { 0x48, 0x53, 0x58, 0x47 }, ".bls" },
            { new byte[] { 0x49, 0x44, 0x33 }, ".mp3" },
            { new byte[] { 0xff, 0xfb }, ".mp3" },
        };

        private CASCHandler CASC;
        private CASCFolder Root;

        public FileScanner(CASCHandler casc, CASCFolder root)
        {
            CASC = casc;
            Root = root;
        }

        public HashSet<string> ScanFile(CASCFile file)
        {
            if (!excludeFileTypes.Contains(Path.GetExtension(file.FullName).ToLower()))
            {
                HashSet<string> strings = new HashSet<string>();
                try
                {
                    using (Stream fileStream = CASC.OpenFile(file.Hash, file.FullName))
                    {
                        //if (fileStream == null)
                        //    return null;
                        int b;
                        int state = 1;
                        StringBuilder sb = new StringBuilder();

                        // look for regex a+(da+)*\.a+ where a = IsAlphaNum() and d = IsFileDelim()
                        // using a simple state machine
                        while ((b = fileStream.ReadByte()) > -1)
                        {
                            if (state == 1 && IsAlphaNum(b) || state == 2 && IsAlphaNum(b) || state == 3 && IsAlphaNum(b)) // alpha
                            {
                                state = 2;
                                sb.Append((char)b);
                            }
                            else if (state == 2 && IsFileDelim(b)) // delimiter
                            {
                                state = 3;
                                sb.Append((char)b);
                            }
                            else if (state == 2 && b == 46) // dot
                            {
                                state = 4;
                                sb.Append((char)b);
                            }
                            else if (state == 4 && IsAlphaNum(b)) // extension
                            {
                                state = 5;
                                sb.Append((char)b);
                            }
                            else if (state == 5 && IsAlphaNum(b)) // extension
                            {
                                sb.Append((char)b);
                            }
                            else if (state == 5 && !IsFileChar(b)) // accept
                            {
                                state = 1;
                                if (sb.Length >= 10)
                                    strings.Add(sb.ToString());
                                sb.Clear();
                            }
                            else
                            {
                                state = 1;
                                sb.Clear();
                            }
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Skipped " + file.FullName + " because of both local and CDN indices are missing.");
                    return null;
                }
                return strings;
            }
            return null;
        }

        // dash, space, underscore, point, slash, backslash, a-z, A-Z, 0-9
        private bool IsFileChar(int i)
        {
            return ((i >= 46 && i <= 57) || (i >= 65 && i <= 90) || i == 92 || i == 95 || (i >= 97 && i <= 122));
        }

        // dash, underscore, a-z, A-Z, 0-9
        private bool IsAlphaNum(int i)
        {
            return (i == 45 || (i >= 48 && i <= 57) || (i >= 65 && i <= 90) || i == 95 || (i >= 97 && i <= 122));
        }

        // slash or backslash
        private bool IsFileDelim(int i)
        {
            return (i == 47 || i == 92);
        }

        public string GetFileExtension(CASCFile file)
        {
            try
            {
                using (Stream stream = CASC.OpenFile(file.Hash, file.FullName))
                {
                    byte[] magic = new byte[4];

                    stream.Read(magic, 0, 4);

                    foreach (var number in MagicNumbers)
                    {
                        if (number.Key.EqualsToIgnoreLength(magic))
                        {
                            return number.Value;
                        }
                    }
                }
            }
            catch
            { }
            return "";
        }
    }
}
