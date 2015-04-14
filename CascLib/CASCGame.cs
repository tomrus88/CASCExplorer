using System.IO;

namespace CASCExplorer
{
    public enum CASCGameType
    {
        Unknown,
        HotS,
        WoW,
        D3,
        S2
    }

    class CASCGame
    {
        public static CASCGameType DetectLocalGame(string path)
        {
            if (Directory.Exists(Path.Combine(path, "HeroesData")))
                return CASCGameType.HotS;

            if (Directory.Exists(Path.Combine(path, "SC2Data")))
                return CASCGameType.S2;

            if (Directory.Exists(Path.Combine(path, "Data")))
            {
                if (File.Exists(Path.Combine(path, "Diablo III.exe")))
                    return CASCGameType.D3;

                if (File.Exists(Path.Combine(path, "Wow.exe")))
                    return CASCGameType.WoW;

                if (File.Exists(Path.Combine(path, "WowT.exe")))
                    return CASCGameType.WoW;

                if (File.Exists(Path.Combine(path, "WowB.exe")))
                    return CASCGameType.WoW;
            }

            return CASCGameType.Unknown;
        }

        public static CASCGameType DetectOnlineGame(string uid)
        {
            if (uid == "hero")
                return CASCGameType.HotS;

            if (uid == "sc2" || uid == "s2b")
                return CASCGameType.S2;

            if (uid.StartsWith("wow"))
                return CASCGameType.WoW;

            if (uid.StartsWith("d3"))
                return CASCGameType.D3;

            return CASCGameType.Unknown;
        }

        public static string GetDataFolder(CASCGameType gameType)
        {
            if (gameType == CASCGameType.HotS)
                return "HeroesData";

            if (gameType == CASCGameType.S2)
                return "SC2Data";

            if (gameType == CASCGameType.WoW || gameType == CASCGameType.D3)
                return "Data";

            return null;
        }
    }
}
