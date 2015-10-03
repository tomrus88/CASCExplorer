using CASCExplorer.Properties;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class InitForm : Form
    {
        public CASCHandler CASC { get; set; }
        public CASCFolder Root { get; set; }

        public InitForm()
        {
            InitializeComponent();
        }

        private void InitForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(e.CloseReason != CloseReason.None)
                backgroundWorker1.CancelAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            CASCConfig config = Settings.Default.OnlineMode
                ? CASCConfig.LoadOnlineStorageConfig((string)e.Argument, "us")
                : CASCConfig.LoadLocalStorageConfig((string)e.Argument);

            if (Settings.Default.OnlineMode)
            {
                using (SelectBuildForm sb = new SelectBuildForm(config))
                {
                    var result = sb.ShowDialog();

                    if (result != DialogResult.OK || sb.SelectedIndex == -1)
                    {
                        e.Cancel = true;
                        return;
                    }

                    config.ActiveBuild = sb.SelectedIndex;
                }
            }

            CASC = CASCHandler.OpenStorage(config, backgroundWorker1);

            CASC.Root.LoadListFile(Path.Combine(Application.StartupPath, "listfile.txt"), backgroundWorker1);
            Root = CASC.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);

            CASC.Install.MergeData(Root);

            e.Result = CASC;
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;

            string arg = (string)e.UserState;

            if (arg != null)
                label1.Text = arg;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DialogResult = DialogResult.Cancel;

            if (e.Cancelled)
            {
                MessageBox.Show("Loading cancelled", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //Application.Exit();
                return;
            }

            if (e.Error != null)
            {
                MessageBox.Show("Loading failed due to:\n" + e.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //Application.Exit();
                return;
            }

            if (e.Result == null)
            {
                MessageBox.Show("Loading failed: Result is null", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //Application.Exit();
                return;
            }

            DialogResult = DialogResult.OK;
        }

        public void LoadLocalStorage(string path)
        {
            Settings.Default.OnlineMode = false;
            backgroundWorker1.RunWorkerAsync(path);
        }

        public void LoadOnlineStorage(string product)
        {
            Settings.Default.OnlineMode = true;
            backgroundWorker1.RunWorkerAsync(product);
        }
    }
}
