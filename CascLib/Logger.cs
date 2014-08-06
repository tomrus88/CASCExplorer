using System.IO;

namespace CASCExplorer
{
    public class Logger
    {
        static FileStream fs = new FileStream("debug.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        static StreamWriter logger = new StreamWriter(fs) { AutoFlush = true };

        public static void WriteLine(string format, params object[] args)
        {
            logger.WriteLine(format, args);
        }
    }
}
