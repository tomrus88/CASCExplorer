using System.IO;

namespace CASCExplorer
{
    public class Logger
    {
        static StreamWriter logger = new StreamWriter("debug.log") { AutoFlush = true };

        public static void WriteLine(string format, params object[] args)
        {
            logger.WriteLine(format, args);
        }
    }
}
