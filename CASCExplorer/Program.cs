using System;
using System.IO;
using System.Windows.Forms;

namespace CASCExplorer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!Properties.Settings.Default.OnlineMode && !Directory.Exists(Properties.Settings.Default.WowPath))
            {
                var folderBrowser = new FolderBrowserDialog();

                if (folderBrowser.ShowDialog() != DialogResult.OK)
                {
                    MessageBox.Show("Please select wow folder!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!File.Exists(Path.Combine(folderBrowser.SelectedPath, ".build.info")))
                {
                    MessageBox.Show("Invalid folder selected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Properties.Settings.Default.WowPath = folderBrowser.SelectedPath;
                Properties.Settings.Default.Save();
            }

            Application.Run(new MainForm());
        }
    }
}
