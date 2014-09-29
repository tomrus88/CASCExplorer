using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

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

    [Flags]
    public enum ContentFlags : uint
    {
        None = 0,
        LowViolence = 0x80, // many models have this flag
        NoCompression = 0x80000000 // sounds have this flag
    }

    public class RootBlock
    {
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
    }

    public class RootEntry
    {
        public RootBlock Block;
        public int Unk1;
        public byte[] MD5;
        public ulong Hash;

        public override string ToString()
        {
            return String.Format("RootBlock: {0:X8} {1:X8}, File: {2:X8} {3}", Block.ContentFlags, Block.LocaleFlags, Unk1, MD5.ToHexString());
        }
    }

    class WowRootHandler
    {
        public readonly Dictionary<ulong, List<RootEntry>> RootData = new Dictionary<ulong, List<RootEntry>>();

        public int Count
        {
            get { return RootData.Count; }
        }

        public WowRootHandler(Stream stream, BackgroundWorker worker)
        {
            if (worker != null)
            {
                if (worker.CancellationPending)
                    throw new OperationCanceledException();
                worker.ReportProgress(0);
            }

            using (var br = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    int count = br.ReadInt32();

                    RootBlock block = new RootBlock();
                    block.ContentFlags = (ContentFlags)br.ReadUInt32();
                    block.LocaleFlags = (LocaleFlags)br.ReadUInt32();

                    if (block.LocaleFlags == LocaleFlags.None)
                        throw new Exception("block.LocaleFlags == LocaleFlags.None");

                    if (block.ContentFlags != ContentFlags.None && (block.ContentFlags & (ContentFlags.LowViolence | ContentFlags.NoCompression)) == 0)
                        throw new Exception("block.ContentFlags != ContentFlags.None");

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

                    if (worker != null)
                    {
                        if (worker.CancellationPending)
                            throw new OperationCanceledException();
                        worker.ReportProgress((int)((float)stream.Position / (float)stream.Length * 100));
                    }
                }
            }
        }

        public List<RootEntry> GetRootInfo(ulong hash)
        {
            List<RootEntry> result;
            RootData.TryGetValue(hash, out result);
            return result;
        }
    }
}
