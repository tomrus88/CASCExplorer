﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class ExtractProgress : Form
    {
        private string ExtractPath
        {
            get { return textBox1.Text; }
            set { textBox1.Text = value; }
        }
        private int NumFiles;
        private int NumExtracted;
        private CASCHandler cascHandler;
        private CASCFolder folder;
        private IEnumerable<int> selection;
        private LocaleFlags locale;

        public ExtractProgress()
        {
            InitializeComponent();

            // Show locales except All and None
            var localeFlagValues = Enum.GetValues(typeof(LocaleFlags));
            foreach (int localeFlagValue in localeFlagValues)
            {
                if (localeFlagValue > 0)
                {
                    comboBox1.Items.Add(Enum.GetName(typeof(LocaleFlags), localeFlagValue));
                }
            }

            comboBox1.SelectedIndex = comboBox1.Items.Count - 1;

            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            ExtractPath = System.IO.Path.GetDirectoryName(exePath) + "\\Work";
        }

        public void SetExtractData(CASCHandler _cascHandler, CASCFolder _folder, ListView.SelectedIndexCollection _selection)
        {
            cascHandler = _cascHandler;
            folder = _folder;
            selection = _selection.Cast<int>().ToArray();
            NumExtracted = 0;
            NumFiles = GetFilesCount(folder, selection);
            progressBar1.Value = 0;
        }

        private void ExtractFile(CASCFile file)
        {
            if (backgroundWorker1.CancellationPending)
                throw new OperationCanceledException();

            backgroundWorker1.ReportProgress((int)((float)++NumExtracted / (float)NumFiles * 100));

            var rootInfos = cascHandler.GetRootInfo(file.Hash);

            if (rootInfos == null)
                return;

            foreach (var rootInfo in rootInfos)
            {
                // only enUS atm
                if ((rootInfo.Block.Flags & locale) == 0)
                    continue;

                var encInfo = cascHandler.GetEncodingInfo(rootInfo.MD5);

                if (encInfo == null)
                    continue;

                foreach (var key in encInfo.Keys)
                {
                    cascHandler.ExtractFile(key, ExtractPath, file.FullName);
                    return;
                }
            }
        }

        private int GetFilesCount(CASCFolder _folder, IEnumerable<int> _selection)
        {
            int count = 0;

            if (_selection != null)
            {
                foreach (int index in _selection)
                {
                    var entry = _folder.SubEntries.ElementAt(index);

                    if (entry.Value is CASCFile)
                        count++;
                    else
                        count += GetFilesCount(entry.Value as CASCFolder, null);
                }
            }
            else
            {
                foreach (var entry in _folder.SubEntries)
                {
                    if (entry.Value is CASCFile)
                        count++;
                    else
                        count += GetFilesCount(entry.Value as CASCFolder, null);
                }
            }

            return count;
        }

        private void ExtractData(CASCFolder _folder, IEnumerable<int> _selection)
        {
            if (_selection != null)
            {
                foreach (int index in _selection)
                {
                    var entry = _folder.SubEntries.ElementAt(index);

                    if (entry.Value is CASCFile)
                        ExtractFile(entry.Value as CASCFile);
                    else
                        ExtractData(entry.Value as CASCFolder, null);
                }
            }
            else
            {
                foreach (var entry in _folder.SubEntries)
                {
                    if (entry.Value is CASCFile)
                        ExtractFile(entry.Value as CASCFile);
                    else
                        ExtractData(entry.Value as CASCFolder, null);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog1.ShowDialog();

            if (result != DialogResult.OK)
                return;

            textBox1.Text = folderBrowserDialog1.SelectedPath;
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
                ExtractData(folder, selection);
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
