using System;
using System.IO;
using System.Text.RegularExpressions;
using CASCConsole.Properties;
using CASCExplorer;

namespace CASCConsole
{
    class Program
    {
        static CASCHandler cascHandler;
        static CASCFolder root;

        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Invalid arguments count!");
                Console.WriteLine("Usage: CASCConsole <pattern> <destination> <localeFlags> <contentFlags>");
                return;
            }

            Console.WriteLine("Settings:");
            Console.WriteLine("    WowPath: {0}", Settings.Default.WowPath);
            Console.WriteLine("    OnlineMode: {0}", Settings.Default.OnlineMode);

            Console.WriteLine("Loading...");

            cascHandler = Settings.Default.OnlineMode
                ? CASCHandler.OpenOnlineStorage(Settings.Default.Product)
                : CASCHandler.OpenLocalStorage(Settings.Default.WowPath);

            root = cascHandler.LoadListFile(Path.Combine(Environment.CurrentDirectory, "listfile.txt"));

            Console.WriteLine("Loaded.");

            string pattern = args[0];
            string dest = args[1];
            LocaleFlags locale = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), args[2]);
            ContentFlags content = (ContentFlags)Enum.Parse(typeof(ContentFlags), args[3]);

            Console.WriteLine("Extract params:", pattern, dest, locale);
            Console.WriteLine("    Pattern: {0}", pattern);
            Console.WriteLine("    Destination: {0}", dest);
            Console.WriteLine("    LocaleFlags: {0}", locale);
            Console.WriteLine("    ContentFlags: {0} (exclusive!)", content);

            Wildcard wildcard = new Wildcard(pattern, RegexOptions.IgnoreCase);

            foreach (var file in root.GetFiles())
            {
                if (wildcard.IsMatch(file.FullName))
                {
                    Console.Write("Extracting '{0}'...", file.FullName);

                    try
                    {
                        cascHandler.SaveFileTo(file.FullName, dest, locale, content);
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
    }
}
