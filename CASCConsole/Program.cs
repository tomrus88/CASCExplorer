using System;
using System.IO;
using System.Text.RegularExpressions;
using CASCConsole.Properties;
using CASCExplorer;

namespace CASCConsole
{
    class Program
    {
        static object progressLock = new object();

        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Invalid arguments count!");
                Console.WriteLine("Usage: CASCConsole <pattern> <destination> <localeFlags> <contentFlags>");
                return;
            }

            Console.WriteLine("Settings:");
            Console.WriteLine("    WowPath: {0}", Settings.Default.StoragePath);
            Console.WriteLine("    OnlineMode: {0}", Settings.Default.OnlineMode);

            Console.WriteLine("Loading...");

            AsyncAction bgLoader = new AsyncAction(() => { });
            bgLoader.ProgressChanged += BgLoader_ProgressChanged;
            CASCHandler cascHandler = Settings.Default.OnlineMode
                ? CASCHandler.OpenOnlineStorage(Settings.Default.Product, bgLoader)
                : CASCHandler.OpenLocalStorage(Settings.Default.StoragePath, bgLoader);

            string pattern = args[0];
            string dest = args[1];
            LocaleFlags locale = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), args[2]);
            ContentFlags content = (ContentFlags)Enum.Parse(typeof(ContentFlags), args[3]);

            cascHandler.Root.LoadListFile(Path.Combine(Environment.CurrentDirectory, "listfile.txt"), bgLoader);
            CASCFolder root = cascHandler.Root.SetFlags(locale, content);

            Console.WriteLine("Loaded.");

            Console.WriteLine("Extract params:", pattern, dest, locale);
            Console.WriteLine("    Pattern: {0}", pattern);
            Console.WriteLine("    Destination: {0}", dest);
            Console.WriteLine("    LocaleFlags: {0}", locale);
            Console.WriteLine("    ContentFlags: {0}", content);

            Wildcard wildcard = new Wildcard(pattern, RegexOptions.IgnoreCase);

            foreach (var file in root.GetFiles())
            {
                if (wildcard.IsMatch(file.FullName))
                {
                    Console.Write("Extracting '{0}'...", file.FullName);

                    try
                    {
                        cascHandler.SaveFileTo(file.FullName, dest);
                        Console.WriteLine(" Ok!");
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(" Error!");
                        Logger.WriteLine(exc.Message);
                    }
                }
            }

            Console.WriteLine("Extracted.");
        }

        private static void BgLoader_ProgressChanged(object sender, AsyncActionProgressChangedEventArgs e)
        {
            lock (progressLock)
            {
                if (e.UserData != null)
                    Console.WriteLine(e.UserData);

                DrawProgressBar(e.Progress, 100, 72, '#');
            }
        }

        private static void DrawProgressBar(long complete, long maxVal, int barSize, char progressCharacter)
        {
            float perc = (float)complete / (float)maxVal;
            DrawProgressBar(perc, barSize, progressCharacter);
        }

        private static void DrawProgressBar(float percent, int barSize, char progressCharacter)
        {
            Console.CursorVisible = false;
            int left = Console.CursorLeft;
            int chars = (int)Math.Round(percent / (1.0f / (float)barSize));
            string p1 = String.Empty, p2 = String.Empty;

            for (int i = 0; i < chars; i++)
                p1 += progressCharacter;
            for (int i = 0; i < barSize - chars; i++)
                p2 += progressCharacter;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(p1);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write(p2);

            Console.ResetColor();
            Console.Write(" {0}%", (percent * 100).ToString("N2"));
            Console.CursorLeft = left;
        }
    }
}
