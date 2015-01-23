﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CASCExplorer
{
    [Flags]
    public enum LocaleFlags
    {
        All = -1,
        None = 0,
        Unk_1 = 0x1,
        enUS = 0x2,
        koKR = 0x4,
        Unk_8 = 0x8,
        frFR = 0x10,
        deDE = 0x20,
        zhCN = 0x40,
        esES = 0x80,
        zhTW = 0x100,
        enGB = 0x200,
        enCN = 0x400,
        enTW = 0x800,
        esMX = 0x1000,
        ruRU = 0x2000,
        ptBR = 0x4000,
        itIT = 0x8000,
        ptPT = 0x10000
    }

    public class RootBlock
    {
        public uint Unk1;
        public LocaleFlags Flags;
    }

    public class RootEntry
    {
        public RootBlock Block;
        public int Unk1;
        public byte[] MD5;
        public ulong Hash;

        public override string ToString()
        {
            return String.Format("Block: {0:X8} {1:X8}, File: {2:X8} {3}", Block.Unk1, Block.Flags, Unk1, MD5.ToHexString());
        }
    }

    public class EncodingEntry
    {
        public int Size;
        public List<byte[]> Keys;

        public EncodingEntry()
        {
            Keys = new List<byte[]>();
        }
    }

    public class IndexEntry
    {
        public int Index;
        public int Offset;
        public int Size;
    }

    public class CASCHandler
    {
        readonly string listFile = Path.Combine(Application.StartupPath, "listfile.txt");

        static readonly ByteArrayComparer comparer = new ByteArrayComparer();

        readonly Dictionary<ulong, List<RootEntry>> RootData = new Dictionary<ulong, List<RootEntry>>();
        readonly Dictionary<byte[], EncodingEntry> EncodingData = new Dictionary<byte[], EncodingEntry>(comparer);
        readonly Dictionary<byte[], IndexEntry> LocalIndexData = new Dictionary<byte[], IndexEntry>(comparer);

        public static readonly Dictionary<ulong, string> FileNames = new Dictionary<ulong, string>();
        public static readonly Dictionary<ulong, string> FolderNames = new Dictionary<ulong, string>();

        public static readonly Jenkins96 Hasher = new Jenkins96();

        public readonly Dictionary<int, FileStream> DataStreams = new Dictionary<int, FileStream>();

        public int NumRootEntries { get { return RootData.Count; } }
        public int NumFileNames { get { return FileNames.Count; } }

        public static bool OnlineMode
        {
            get { return Properties.Settings.Default.OnlineMode; }
        }

        public CASCHandler(CASCFolder root, BackgroundWorker worker)
        {
            if (!OnlineMode)
            {
                var idxFiles = GetIdxFiles(Properties.Settings.Default.WowPath);

                if (idxFiles.Count == 0)
                    throw new FileNotFoundException("idx files missing!");

                worker.ReportProgress(0);

                int idxIndex = 0;

                foreach (var idx in idxFiles)
                {
                    using (var fs = new FileStream(idx, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var br = new BinaryReader(fs))
                    {
                        int h2Len = br.ReadInt32();
                        int h2Check = br.ReadInt32();
                        byte[] h2 = br.ReadBytes(h2Len);

                        long padPos = (8 + h2Len + 0x0F) & 0xFFFFFFF0;
                        fs.Position = padPos;

                        int dataLen = br.ReadInt32();
                        int dataCheck = br.ReadInt32();

                        int numBlocks = dataLen / 18;

                        for (int i = 0; i < numBlocks; i++)
                        {
                            IndexEntry info = new IndexEntry();
                            byte[] key = br.ReadBytes(9);
                            int indexHigh = br.ReadByte();
                            int indexLow = br.ReadInt32BE();

                            info.Index = (int)((byte)(indexHigh << 2) | ((indexLow & 0xC0000000) >> 30));
                            info.Offset = (indexLow & 0x3FFFFFFF);
                            info.Size = br.ReadInt32();

                            // duplicate keys wtf...
                            //IndexData[key] = info; // use last key
                            if (!LocalIndexData.ContainsKey(key)) // use first key
                                LocalIndexData.Add(key, info);
                        }

                        padPos = (dataLen + 0x0FFF) & 0xFFFFF000;
                        fs.Position = padPos;

                        fs.Position += numBlocks * 18;
                        //for (int i = 0; i < numBlocks; i++)
                        //{
                        //    var bytes = br.ReadBytes(18); // unknown data
                        //}

                        if (fs.Position != fs.Position)
                            throw new Exception("idx file under read");
                    }

                    worker.ReportProgress((int)((float)++idxIndex / (float)idxFiles.Count * 100));
                }

                Logger.WriteLine("CASCHandler: loaded {0} indexes", LocalIndexData.Count);
            }

            worker.ReportProgress(0);

            using (var fs = OpenEncodingFile())
            using (var br = new BinaryReader(fs))
            {
                br.ReadBytes(2); // EN
                byte b1 = br.ReadByte();
                byte b2 = br.ReadByte();
                byte b3 = br.ReadByte();
                ushort s1 = br.ReadUInt16();
                ushort s2 = br.ReadUInt16();
                int numEntries = br.ReadInt32BE();
                int i1 = br.ReadInt32BE();
                byte b4 = br.ReadByte();
                int entriesOfs = br.ReadInt32BE();

                fs.Position += entriesOfs; // skip strings

                fs.Position += numEntries * 32;
                //for (int i = 0; i < numEntries; ++i)
                //{
                //    br.ReadBytes(16);
                //    br.ReadBytes(16);
                //}

                for (int i = 0; i < numEntries; ++i)
                {
                    ushort keysCount;

                    while ((keysCount = br.ReadUInt16()) != 0)
                    {
                        int fileSize = br.ReadInt32BE();
                        byte[] md5 = br.ReadBytes(16);

                        var entry = new EncodingEntry();
                        entry.Size = fileSize;

                        for (int ki = 0; ki < keysCount; ++ki)
                        {
                            byte[] key = br.ReadBytes(16);

                            entry.Keys.Add(key);
                        }

                        //Encodings[md5] = entry;
                        EncodingData.Add(md5, entry);
                    }

                    //br.ReadBytes(28);
                    while (br.PeekChar() == 0)
                        fs.Position++;

                    worker.ReportProgress((int)((float)fs.Position / (float)fs.Length * 100));
                }
                //var pos = br.BaseStream.Position;
                //for (int i = 0; i < i1; ++i)
                //{
                //    br.ReadBytes(16);
                //    br.ReadBytes(16);
                //}
                Logger.WriteLine("CASCHandler: loaded {0} encoding data", EncodingData.Count);
            }

            worker.ReportProgress(0);

            using (var fs = OpenRootFile())
            using (var br = new BinaryReader(fs))
            {
                while (fs.Position < fs.Length)
                {
                    int count = br.ReadInt32();

                    RootBlock block = new RootBlock();
                    block.Unk1 = br.ReadUInt32();
                    block.Flags = (LocaleFlags)br.ReadUInt32();

                    if (block.Flags == LocaleFlags.None)
                        throw new Exception("block.Flags == LocaleFlags.None");

                    RootEntry[] entries = new RootEntry[count];

                    for (var i = 0; i < count; ++i)
                    {
                        entries[i] = new RootEntry();
                        entries[i].Block = block;
                        entries[i].Unk1 = br.ReadInt32();
                    }

                    for (var i = 0; i < count; ++i)
                    {
                        entries[i].MD5 = br.ReadBytes(16);

                        ulong hash = br.ReadUInt64();
                        entries[i].Hash = hash;

                        // don't load other locales
                        //if (block.Flags != LocaleFlags.All && (block.Flags & LocaleFlags.enUS) == 0)
                        //    continue;

                        if (!RootData.ContainsKey(hash))
                        {
                            RootData[hash] = new List<RootEntry>();
                            RootData[hash].Add(entries[i]);
                        }
                        else
                            RootData[hash].Add(entries[i]);
                    }

                    worker.ReportProgress((int)((float)fs.Position / (float)fs.Length * 100));
                }

                Logger.WriteLine("CASCHandler: loaded {0} root data", RootData.Count);
            }

            worker.ReportProgress(0);

            if (File.Exists(listFile))
            {
                FolderNames[Hasher.ComputeHash("root")] = "root";

                using (StreamReader sr = new StreamReader(listFile))
                {
                    string file;
                    int filesCount = 0;

                    CASCFolder folder = root;

                    while ((file = sr.ReadLine()) != null)
                    {
                        ulong fileHash = Hasher.ComputeHash(file);

                        // skip invalid names
                        if (!RootData.ContainsKey(fileHash))
                        {
                            Logger.WriteLine("Invalid file name: {0}", file);
                            continue;
                        }

                        filesCount++;

                        string[] parts = file.Split('\\');

                        for (int i = 0; i < parts.Length; ++i)
                        {
                            bool isFile = (i == parts.Length - 1);

                            ulong hash = isFile ? fileHash : Hasher.ComputeHash(parts[i]);

                            ICASCEntry entry = folder.GetEntry(hash);

                            if (entry == null)
                            {
                                if (isFile)
                                {
                                    entry = new CASCFile(hash);
                                    FileNames[hash] = file;
                                }
                                else
                                {
                                    entry = new CASCFolder(hash);
                                    FolderNames[hash] = parts[i];
                                }

                                folder.SubEntries[hash] = entry;

                                if (isFile)
                                {
                                    folder = root;
                                    break;
                                }
                            }

                            folder = entry as CASCFolder;
                        }

                        if ((filesCount % 1000) == 0)
                            worker.ReportProgress((int)((float)sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }

                    Logger.WriteLine("CASCHandler: loaded {0} file names", FileNames.Count);
                }
            }
            else
            {
                throw new FileNotFoundException("list file missing!");
            }
        }

        private Stream OpenRootFile()
        {
            var encInfo = GetEncodingInfo(CASCConfig.RootMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for root file missing!");

            if (encInfo.Keys.Count > 1)
                throw new FileNotFoundException("multiple encoding info for root file found!");

            //ExtractFile(encInfo.Keys[0], ".", "root");

            return OpenFile(encInfo.Keys[0]);
        }

        private Stream OpenEncodingFile()
        {
            return OpenFile(CASCConfig.EncodingKey);
        }

        private Stream OpenFile(byte[] key)
        {
            try
            {
                if (OnlineMode)
                    throw new Exception();

                var idxInfo = GetLocalIndexInfo(key);

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
                    return blte.OpenFile();
            }
            catch
            {
                if (key.EqualsTo(CASCConfig.EncodingKey))
                {
                    int len;
                    using (Stream s = CDNHandler.OpenDataFileDirect(key, out len))
                    using (BLTEHandler blte = new BLTEHandler(s, len))
                        return blte.OpenFile();
                }
                else
                {
                    var idxInfo = CDNHandler.GetCDNIndexInfo(key);

                    if (idxInfo == null)
                        throw new Exception("CDN index missing");

                    using (Stream s = CDNHandler.OpenDataFile(key))
                    using (BLTEHandler blte = new BLTEHandler(s, idxInfo.Size))
                        return blte.OpenFile();
                }
            }
        }

        public void ExtractFile(byte[] key, string path, string name)
        {
            try
            {
                if (OnlineMode)
                    throw new Exception();

                var idxInfo = GetLocalIndexInfo(key);

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
                    blte.ExtractToFile(path, name);
            }
            catch
            {
                if (key.EqualsTo(CASCConfig.EncodingKey))
                {
                    int len;
                    using (Stream s = CDNHandler.OpenDataFileDirect(key, out len))
                    using (BLTEHandler blte = new BLTEHandler(s, len))
                        blte.ExtractToFile(path, name);
                }
                else
                {
                    var idxInfo = CDNHandler.GetCDNIndexInfo(key);

                    if (idxInfo == null)
                        throw new Exception("CDN index missing");

                    using (Stream s = CDNHandler.OpenDataFile(key))
                    using (BLTEHandler blte = new BLTEHandler(s, idxInfo.Size))
                        blte.ExtractToFile(path, name);
                }
            }
        }

        ~CASCHandler()
        {
            foreach (var stream in DataStreams)
                stream.Value.Close();
        }

        private List<string> GetIdxFiles(string wowPath)
        {
            List<string> latestIdx = new List<string>();

            for (int i = 0; i < 0x10; ++i)
            {
                var files = Directory.EnumerateFiles(Path.Combine(wowPath, "Data\\data\\"), String.Format("{0:X2}*.idx", i));

                if (files.Count() > 0)
                    latestIdx.Add(files.Last());
            }

            return latestIdx;
        }

        public List<RootEntry> GetRootInfo(ulong hash)
        {
            if (RootData.ContainsKey(hash))
                return RootData[hash];
            return null;
        }

        public EncodingEntry GetEncodingInfo(byte[] md5)
        {
            if (EncodingData.ContainsKey(md5))
                return EncodingData[md5];
            return null;
        }

        public IndexEntry GetLocalIndexInfo(byte[] key)
        {
            byte[] temp = key.Copy(9);
            if (LocalIndexData.ContainsKey(temp))
                return LocalIndexData[temp];

            Logger.WriteLine("CASCHandler: missing index: {0}", key.ToHexString());

            return null;
        }

        public FileStream GetDataStream(int index)
        {
            if (DataStreams.ContainsKey(index))
                return DataStreams[index];

            string dataFile = Path.Combine(Properties.Settings.Default.WowPath, String.Format("Data\\data\\data.{0:D3}", index));

            var fs = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            DataStreams[index] = fs;

            return fs;
        }
    }
}
