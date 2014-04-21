using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
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
        public byte[] MD5;
        public List<byte[]> Keys;

        public EncodingEntry()
        {
            Keys = new List<byte[]>();
        }
    }

    public class IndexEntry
    {
        public int DataIndex;
        public int Offset;
        public int Size;
    }

    public class CASCHandler
    {
        readonly string listFile = Path.Combine(Application.StartupPath, "listfile.txt");

        static readonly ByteArrayComparer comparer = new ByteArrayComparer();

        readonly Dictionary<ulong, List<RootEntry>> RootData = new Dictionary<ulong, List<RootEntry>>();
        readonly Dictionary<byte[], EncodingEntry> EncodingData = new Dictionary<byte[], EncodingEntry>(comparer);
        readonly Dictionary<byte[], IndexEntry> IndexData = new Dictionary<byte[], IndexEntry>(comparer);

        public static readonly Dictionary<ulong, string> FileNames = new Dictionary<ulong, string>();
        public static readonly Dictionary<ulong, string> FolderNames = new Dictionary<ulong, string>();

        public static readonly Jenkins96 Hasher = new Jenkins96();

        public readonly Dictionary<int, BinaryReader> DataStreams = new Dictionary<int, BinaryReader>();

        public int NumRootEntries { get { return RootData.Count; } }
        public int NumFileNames { get { return FileNames.Count; } }

        public CASCHandler(CASCFolder root, BackgroundWorker worker)
        {
            string wowPath = Properties.Settings.Default.WowPath;

            if (!Directory.Exists(wowPath))
                throw new DirectoryNotFoundException(wowPath);

            var idxFiles = GetIdxFiles(wowPath);

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
                    br.BaseStream.Position = padPos;

                    int dataLen = br.ReadInt32();
                    int dataCheck = br.ReadInt32();

                    int numBlocks = dataLen / 18;

                    for (int i = 0; i < numBlocks; i++)
                    {
                        IndexEntry info = new IndexEntry();
                        byte[] key = br.ReadBytes(9);
                        int indexHigh = br.ReadByte();
                        int indexLow = br.ReadInt32BE();

                        info.DataIndex = (int)((byte)(indexHigh << 2) | ((indexLow & 0xC0000000) >> 30));
                        info.Offset = (indexLow & 0x3FFFFFFF);
                        info.Size = br.ReadInt32();

                        // duplicate keys wtf...
                        //IndexData[key] = info; // use last key
                        if (!IndexData.ContainsKey(key)) // use first key
                            IndexData.Add(key, info);
                    }

                    padPos = (dataLen + 0x0FFF) & 0xFFFFF000;
                    br.BaseStream.Position = padPos;

                    for (int i = 0; i < numBlocks; i++)
                    {
                        var bytes = br.ReadBytes(18); // unknown data
                    }

                    if (br.BaseStream.Position != br.BaseStream.Length)
                    {
                        throw new Exception("idx file under read");
                    }
                }

                worker.ReportProgress((int)((float)++idxIndex / (float)idxFiles.Count * 100));
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

                br.BaseStream.Position += entriesOfs; // skip strings

                for (int i = 0; i < numEntries; ++i)
                {
                    br.ReadBytes(16);
                    br.ReadBytes(16);
                }

                for (int i = 0; i < numEntries; ++i)
                {
                    ushort keysCount;

                    while ((keysCount = br.ReadUInt16()) != 0)
                    {
                        int fileSize = br.ReadInt32BE();
                        byte[] md5 = br.ReadBytes(16);

                        var entry = new EncodingEntry();
                        entry.Size = fileSize;
                        entry.MD5 = md5;

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
                        br.BaseStream.Position++;

                    worker.ReportProgress((int)((float)br.BaseStream.Position / (float)br.BaseStream.Length * 100));
                }
                //var pos = br.BaseStream.Position;
                //for (int i = 0; i < i1; ++i)
                //{
                //    br.ReadBytes(16);
                //    br.ReadBytes(16);
                //}
            }

            worker.ReportProgress(0);

            using (var fs = OpenRootFile())
            using (var br = new BinaryReader(fs))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
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

                    worker.ReportProgress((int)((float)br.BaseStream.Position / (float)br.BaseStream.Length * 100));
                }
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
                        filesCount++;

                        string[] parts = file.Split('\\');

                        for (int i = 0; i < parts.Length; ++i)
                        {
                            bool isFile = (i == parts.Length - 1);

                            ulong hash = isFile ? Hasher.ComputeHash(file) : Hasher.ComputeHash(parts[i]);

                            // skip invalid names
                            if (isFile && !RootData.ContainsKey(hash))
                                break;

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
                }
            }
            else
            {
                throw new FileNotFoundException("list file missing!");
            }
        }

        private MemoryStream OpenRootFile()
        {
            var encInfo = GetEncodingInfo(CASCConfig.RootMD5);

            if (encInfo == null)
                throw new FileNotFoundException("encoding info for root file missing!");

            if (encInfo.Keys.Count > 1)
                throw new FileNotFoundException("multiple encoding info for root file found!");

            var idxInfo = GetIndexInfo(encInfo.Keys[0]);

            if (idxInfo == null)
                return null;

            return OpenBLTE(idxInfo);
        }

        private MemoryStream OpenEncodingFile()
        {
            var idxInfo = GetIndexInfo(CASCConfig.EncodingKey);

            if (idxInfo == null)
                return null;

            return OpenBLTE(idxInfo);
        }

        public void ExtractBLTE(IndexEntry idxInfo, string path, string name)
        {
            var stream = GetDataStream(idxInfo.DataIndex);

            stream.BaseStream.Position = idxInfo.Offset;

            byte[] unkHash = stream.ReadBytes(16);
            int size = stream.ReadInt32();
            byte[] unkData1 = stream.ReadBytes(2);
            byte[] unkData2 = stream.ReadBytes(8);

            BLTEHandler blte = new BLTEHandler(stream, idxInfo.Size);
            blte.ExtractToFile(path, name);
        }

        public MemoryStream OpenBLTE(IndexEntry idxInfo)
        {
            var stream = GetDataStream(idxInfo.DataIndex);

            stream.BaseStream.Position = idxInfo.Offset;

            byte[] unkHash = stream.ReadBytes(16);
            int size = stream.ReadInt32();
            byte[] unkData1 = stream.ReadBytes(2);
            byte[] unkData2 = stream.ReadBytes(8);

            BLTEHandler blte = new BLTEHandler(stream, idxInfo.Size);
            return blte.OpenFile();
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

        public IndexEntry GetIndexInfo(byte[] key)
        {
            byte[] temp = key.Take(9).ToArray();
            if (IndexData.ContainsKey(temp))
                return IndexData[temp];
            return null;
        }

        public BinaryReader GetDataStream(int index)
        {
            if (DataStreams.ContainsKey(index))
                return DataStreams[index];

            string dataFile = Path.Combine(Properties.Settings.Default.WowPath, String.Format("Data\\data\\data.{0:D3}", index));

            var file = new FileStream(dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var br = new BinaryReader(file, Encoding.ASCII);
            DataStreams[index] = br;

            return br;
        }
    }
}
