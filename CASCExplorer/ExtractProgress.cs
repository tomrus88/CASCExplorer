using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class ExtractProgress : Form
    {
        private string ExtractPath;
        private int NumFiles;
        private int NumExtracted;
        private CASCHandler cascHandler;
        private LocaleFlags locale;
        private IEnumerable<CASCFile> files;

        public ExtractProgress()
        {
            InitializeComponent();

            comboBox1.Items.AddRange(Enum.GetNames(typeof(LocaleFlags)));

            comboBox1.SelectedIndex = comboBox1.Items.Count - 1;
        }

        public void SetExtractData(CASCHandler cascHandler, ICollection<CASCFile> files)
        {
            this.cascHandler = cascHandler;
            NumExtracted = 0;
            NumFiles = files.Count;
            progressBar1.Value = 0;
            this.files = files;
        }

        private void ExtractFile(CASCFile file)
        {
            if (backgroundWorker1.CancellationPending)
                throw new OperationCanceledException();

            backgroundWorker1.ReportProgress((int)((float)++NumExtracted / (float)NumFiles * 100));

            cascHandler.SaveFileTo(file.FullName, ExtractPath, locale);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog1.ShowDialog();

            if (result != DialogResult.OK)
                return;

            ExtractPath = folderBrowserDialog1.SelectedPath;
            textBox1.Text = ExtractPath;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.IsBusy)
            {
                backgroundWorker1.CancelAsync();
                return;
            }

            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            comboBox1.Enabled = false;
            backgroundWorker1.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                foreach (var file in files)
                {
                    ExtractFile(file);
                }
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            button2.Enabled = true;
            comboBox1.Enabled = true;

            if (e.Cancelled)
            {
                NumExtracted = 0;
                progressBar1.Value = 0;
                MessageBox.Show("Operation cancelled!");
                return;
            }

            Hide();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            locale = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), comboBox1.SelectedItem as string);
        }
    }
}
