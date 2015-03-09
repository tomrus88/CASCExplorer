using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CASCExplorer
{
    public class D3RootHandler : IRootHandler
    {
        private readonly MultiDictionary<ulong, RootEntry> RootData = new MultiDictionary<ulong, RootEntry>();
        private static readonly Jenkins96 Hasher = new Jenkins96();
        private LocaleFlags locale;
        private CASCFolder Root;
        private CoreTOCParser tocParser;

        public int Count { get { return RootData.Count; } }
        public int CountTotal { get { return RootData.Sum(re => re.Value.Count); } }
        public int CountSelect { get; private set; }
        public int CountUnknown { get; private set; }
        public LocaleFlags Locale { get { return locale; } }
        public ContentFlags Content { get { return ContentFlags.None; } }

        public D3RootHandler(Stream stream, AsyncAction worker, CASCHandler casc)
        {
            if (worker != null)
            {
                worker.ThrowOnCancel();
                worker.ReportProgress(0, "Loading \"root\"...");
            }

            using (var br = new BinaryReader(stream))
            {
                byte b1 = br.ReadByte();
                byte b2 = br.ReadByte();
                byte b3 = br.ReadByte();
                byte b4 = br.ReadByte();

                int count = br.ReadInt32();

                Dictionary<string, byte[]> data = new Dictionary<string, byte[]>();

                for (int i = 0; i < count; ++i)
                {
                    byte[] hash = br.ReadBytes(16);
                    string name = br.ReadCString();

                    data[name] = hash;

                    Logger.WriteLine("{0}: {1} {2}", i, hash.ToHexString(), name);
                }

                ParseCoreTOC(casc, data["Base"]);

                foreach (var kv in data)
                {
                    EncodingEntry enc = casc.Encoding.GetEntry(kv.Value);

                    using (Stream s = OpenD3SubRootFile(casc, enc.Key, kv.Value, "data\\" + casc.Config.BuildName + "\\subroot\\" + kv.Key))
                    {
                        if (s != null)
                        {
                            using (var br2 = new BinaryReader(s))
                            {
                                uint magic = br2.ReadUInt32();

                                int nEntries0 = br2.ReadInt32();

                                for (int i = 0; i < nEntries0; i++)
                                {
                                    byte[] md5 = br2.ReadBytes(16);
                                    int snoId = br2.ReadInt32();
                                    var sno = tocParser.GetSNO(snoId);

                                    string name = String.Format("{0}\\{1}{2}", sno.GroupId, sno.Name, sno.Ext);

                                    AddFile(kv.Key, md5, name);
                                }

                                int nEntries1 = br2.ReadInt32();

                                for (int i = 0; i < nEntries1; i++)
                                {
                                    byte[] md5 = br2.ReadBytes(16);
                                    int snoId = br2.ReadInt32();
                                    int fileNumber = br2.ReadInt32();
                                    var sno = tocParser.GetSNO(snoId);

                                    //file extensions are wrong in this case
                                    string name = String.Format("{0}\\{1}\\{2:D4}{3}", sno.GroupId, sno.Name, fileNumber, ".xxx");

                                    AddFile(kv.Key, md5, name);
                                }

                                int nNamedEntries = br2.ReadInt32();

                                for (int i = 0; i < nNamedEntries; i++)
                                {
                                    byte[] md5 = br2.ReadBytes(16);
                                    string name = br2.ReadCString();

                                    AddFile(kv.Key, md5, name);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddFile(string pkg, byte[] md5, string name)
        {
            RootEntry entry = new RootEntry();
            entry.MD5 = md5;

            LocaleFlags locale;

            entry.Block = new RootBlock();

            if (Enum.TryParse<LocaleFlags>(pkg, out locale))
                entry.Block.LocaleFlags = locale;
            else
                entry.Block.LocaleFlags = LocaleFlags.All;

            ulong fileHash = Hasher.ComputeHash(name);
            CASCFile.FileNames[fileHash] = name;

            RootData.Add(fileHash, entry);
        }

        private Stream OpenD3SubRootFile(CASCHandler casc, byte[] key, byte[] md5, string name)
        {
            Stream s = casc.TryLocalCache(key, md5, name);

            if (s != null)
                return s;

            s = casc.TryLocalCache(key, md5, name);

            if (s != null)
                return s;

            return casc.OpenFile(key);
        }

        private void ParseCoreTOC(CASCHandler casc, byte[] md5)
        {
            EncodingEntry enc = casc.Encoding.GetEntry(md5);

            using (Stream s = OpenD3SubRootFile(casc, enc.Key, md5, "data\\" + casc.Config.BuildName + "\\subroot\\Base"))
            {
                if (s != null)
                {
                    using (var br2 = new BinaryReader(s))
                    {
                        uint magic = br2.ReadUInt32();

                        int nEntries0 = br2.ReadInt32();

                        br2.BaseStream.Position += nEntries0 * (16 + 4);

                        int nEntries1 = br2.ReadInt32();

                        br2.BaseStream.Position += nEntries1 * (16 + 4 + 4);

                        int nNamedEntries = br2.ReadInt32();

                        for (int i = 0; i < nNamedEntries; i++)
                        {
                            byte[] md5_2 = br2.ReadBytes(16);
                            string name = br2.ReadCString();

                            if (name == "CoreTOC.dat")
                            {
                                EncodingEntry enc2 = casc.Encoding.GetEntry(md5_2);
                                tocParser = new CoreTOCParser(casc.OpenFile(enc2.Key));
                            }
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            RootData.Clear();
        }

        public IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            HashSet<RootEntry> result;
            RootData.TryGetValue(hash, out result);
            return result;
        }

        public IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            HashSet<RootEntry> result;
            RootData.TryGetValue(hash, out result);
            return result;
        }

        public void LoadListFile(string path, AsyncAction worker = null)
        {

        }

        private CASCFolder CreateStorageTree()
        {
            var rootHash = Hasher.ComputeHash("root");

            var root = new CASCFolder(rootHash);

            CASCFolder.FolderNames[rootHash] = "root";

            CountSelect = 0;

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.Block.LocaleFlags & locale) != 0);

                //if (rootInfosLocale.Count() > 1)
                //{
                //    var rootInfosLocaleAndContent = rootInfosLocale.Where(re => (re.Block.ContentFlags == content));

                //    if (rootInfosLocaleAndContent.Any())
                //        rootInfosLocale = rootInfosLocaleAndContent;
                //}

                if (!rootInfosLocale.Any())
                    continue;

                string file;

                if (!CASCFile.FileNames.TryGetValue(rootEntry.Key, out file))
                {
                    file = "unknown\\" + rootEntry.Key.ToString("X16");
                //    CountUnknown++;
                //    UnknownFiles.Add(rootEntry.Key);
                }

                CreateSubTree(root, rootEntry.Key, file);
                CountSelect++;
            }

            //Logger.WriteLine("D3RootHandler: {0} file names missing for locale {1}", CountUnknown, locale);

            return root;
        }

        private static void CreateSubTree(CASCFolder root, ulong filehash, string file)
        {
            string[] parts = file.Split('\\');

            CASCFolder folder = root;

            for (int i = 0; i < parts.Length; ++i)
            {
                bool isFile = (i == parts.Length - 1);

                ulong hash = isFile ? filehash : Hasher.ComputeHash(parts[i]);

                ICASCEntry entry = folder.GetEntry(hash);

                if (entry == null)
                {
                    if (isFile)
                    {
                        entry = new CASCFile(hash);
                        CASCFile.FileNames[hash] = file;
                    }
                    else
                    {
                        entry = new CASCFolder(hash);
                        CASCFolder.FolderNames[hash] = parts[i];
                    }

                    folder.SubEntries[hash] = entry;
                }

                folder = entry as CASCFolder;
            }
        }

        public CASCFolder SetFlags(LocaleFlags locale, ContentFlags content, bool createTree = true)
        {
            if (this.locale != locale)
            {
                this.locale = locale;

                if (createTree)
                    Root = CreateStorageTree();
            }

            return Root;
        }
    }

    public struct SNOInfo
    {
        public SNOGroup GroupId;
        public string Name;
        public string Ext;
    }

    public enum SNOGroup
    {
        Code = -2,
        None = -1,
        Actor = 1,
        Adventure = 2,
        AiBehavior = 3,
        AiState = 4,
        AmbientSound = 5,
        Anim = 6,
        Animation2D = 7,
        AnimSet = 8,
        Appearance = 9,
        Hero = 10,
        Cloth = 11,
        Conversation = 12,
        ConversationList = 13,
        EffectGroup = 14,
        Encounter = 15,
        Explosion = 17,
        FlagSet = 18,
        Font = 19,
        GameBalance = 20,
        Globals = 21,
        LevelArea = 22,
        Light = 23,
        MarkerSet = 24,
        Monster = 25,
        Observer = 26,
        Particle = 27,
        Physics = 28,
        Power = 29,
        Quest = 31,
        Rope = 32,
        Scene = 33,
        SceneGroup = 34,
        Script = 35,
        ShaderMap = 36,
        Shaders = 37,
        Shakes = 38,
        SkillKit = 39,
        Sound = 40,
        SoundBank = 41,
        StringList = 42,
        Surface = 43,
        Textures = 44,
        Trail = 45,
        UI = 46,
        Weather = 47,
        Worlds = 48,
        Recipe = 49,
        Condition = 51,
        TreasureClass = 52,
        Account = 53,
        Conductor = 54,
        TimedEvent = 55,
        Act = 56,
        Material = 57,
        QuestRange = 58,
        Lore = 59,
        Reverb = 60,
        PhysMesh = 61,
        Music = 62,
        Tutorial = 63,
        BossEncounter = 64,
        ControlScheme = 65,
        Accolade = 66,
        AnimTree = 67,
        Vibration = 68,
        DungeonFinder = 69,
    }

    public class CoreTOCParser
    {
        private const int NUM_SNO_GROUPS = 70;

        struct TOCHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
            public int[] entryCounts;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
            public int[] entryOffsets;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
            public int[] entryUnkCounts;
            public int unk;
        }

        Dictionary<int, SNOInfo> snoDic = new Dictionary<int, SNOInfo>();

        Dictionary<SNOGroup, string> extensions = new Dictionary<SNOGroup, string>()
        {
            { SNOGroup.Code, "" },
            { SNOGroup.None, "" },
            { SNOGroup.Actor, ".acr" },
            { SNOGroup.Adventure, ".adv" },
            { SNOGroup.AiBehavior, "" },
            { SNOGroup.AiState, "" },
            { SNOGroup.AmbientSound, ".ams" },
            { SNOGroup.Anim, ".ani" },
            { SNOGroup.Animation2D, ".an2" },
            { SNOGroup.AnimSet, ".ans" },
            { SNOGroup.Appearance, ".app" },
            { SNOGroup.Hero, "" },
            { SNOGroup.Cloth, ".clt" },
            { SNOGroup.Conversation, ".cnv" },
            { SNOGroup.ConversationList, "" },
            { SNOGroup.EffectGroup, ".efg" },
            { SNOGroup.Encounter, ".enc" },
            { SNOGroup.Explosion, ".xpl" },
            { SNOGroup.FlagSet, "" },
            { SNOGroup.Font, ".fnt" },
            { SNOGroup.GameBalance, ".gam" },
            { SNOGroup.Globals, ".glo" },
            { SNOGroup.LevelArea, ".lvl" },
            { SNOGroup.Light, ".lit" },
            { SNOGroup.MarkerSet, ".mrk" },
            { SNOGroup.Monster, ".mon" },
            { SNOGroup.Observer, ".obs" },
            { SNOGroup.Particle, ".prt" },
            { SNOGroup.Physics, ".phy" },
            { SNOGroup.Power, ".pow" },
            { SNOGroup.Quest, ".qst" },
            { SNOGroup.Rope, ".rop" },
            { SNOGroup.Scene, ".scn" },
            { SNOGroup.SceneGroup, ".scg" },
            { SNOGroup.Script, "" },
            { SNOGroup.ShaderMap, ".shm" },
            { SNOGroup.Shaders, ".shd" },
            { SNOGroup.Shakes, ".shk" },
            { SNOGroup.SkillKit, ".skl" },
            { SNOGroup.Sound, ".snd" },
            { SNOGroup.SoundBank, ".sbk" },
            { SNOGroup.StringList, ".stl" },
            { SNOGroup.Surface, ".srf" },
            { SNOGroup.Textures, ".tex" },
            { SNOGroup.Trail, ".trl" },
            { SNOGroup.UI, ".ui" },
            { SNOGroup.Weather, ".wth" },
            { SNOGroup.Worlds, ".wrl" },
            { SNOGroup.Recipe, ".rcp" },
            { SNOGroup.Condition, ".cnd" },
            { SNOGroup.TreasureClass, "" },
            { SNOGroup.Account, "" },
            { SNOGroup.Conductor, "" },
            { SNOGroup.TimedEvent, "" },
            { SNOGroup.Act, ".act" },
            { SNOGroup.Material, ".mat" },
            { SNOGroup.QuestRange, ".qsr" },
            { SNOGroup.Lore, ".lor" },
            { SNOGroup.Reverb, ".rev" },
            { SNOGroup.PhysMesh, ".phm" },
            { SNOGroup.Music, ".mus" },
            { SNOGroup.Tutorial, ".tut" },
            { SNOGroup.BossEncounter, ".bos" },
            { SNOGroup.ControlScheme, "" },
            { SNOGroup.Accolade, ".aco" },
            { SNOGroup.AnimTree, ".ant" },
            { SNOGroup.Vibration, "" },
            { SNOGroup.DungeonFinder, "" },
        };

        public CoreTOCParser(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                TOCHeader hdr = br.Read<TOCHeader>();

                for (int i = 0; i < NUM_SNO_GROUPS; i++)
                {
                    if (hdr.entryCounts[i] > 0)
                    {
                        br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr);

                        for (int j = 0; j < hdr.entryCounts[i]; j++)
                        {
                            SNOGroup snoGroup = (SNOGroup)br.ReadInt32();
                            int snoId = br.ReadInt32();
                            int pName = br.ReadInt32();

                            long oldPos = br.BaseStream.Position;
                            br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr) + 12 * hdr.entryCounts[i] + pName;
                            string name = br.ReadCString();
                            br.BaseStream.Position = oldPos;

                            snoDic.Add(snoId, new SNOInfo() { GroupId = snoGroup, Name = name, Ext = extensions[snoGroup] });
                        }
                    }
                }
            }
        }

        public SNOInfo GetSNO(int id)
        {
            SNOInfo sno;
            snoDic.TryGetValue(id, out sno);
            return sno;
        }
    }
}
