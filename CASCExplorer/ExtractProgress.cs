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
        private CASCFolder folder;
        private IEnumerable<int> selection;

        public ExtractProgress()
        {
            InitializeComponent();
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
            backgroundWorker1.ReportProgress((int)((float)++NumExtracted / (float)NumFiles * 100));

            var rootInfos = cascHandler.GetRootInfo(file.Hash);

            if (rootInfos == null)
                return;

            foreach (var rootInfo in rootInfos)
            {
                // only enUS and All atm
                if (rootInfo.Block.Flags != LocaleFlags.All && (rootInfo.Block.Flags & LocaleFlags.enUS) == 0)
                    continue;

                var encInfo = cascHandler.GetEncodingInfo(rootInfo.MD5);

                if (encInfo == null)
                    continue;

                foreach (var key in encInfo.Keys)
                {
                    var idxInfo = cascHandler.GetIndexInfo(key);

                    if (idxInfo != null)
                    {
                        var stream = cascHandler.GetDataStream(idxInfo.DataIndex);

                        stream.BaseStream.Position = idxInfo.Offset;

                        byte[] unkHash = stream.ReadBytes(16);
                        int size = stream.ReadInt32();
                        byte[] unkData1 = stream.ReadBytes(2);
                        byte[] unkData2 = stream.ReadBytes(8);

                        BLTEHandler blte = new BLTEHandler(stream, size);
                        blte.ExtractData(ExtractPath, file.FullName);
                    }
                }
            }
        }

        private int GetFilesCount(CASCFolder folder, IEnumerable<int> selection)
        {
            int count = 0;

            if (selection != null)
            {
                foreach (int index in selection)
                {
                    ICASCEntry entry = folder.SubEntries.ElementAt(index).Value;

                    if (entry is CASCFile)
                        count++;
                    else
                        count += GetFilesCount(entry as CASCFolder, null);
                }
            }
            else
            {
                foreach (var entry in folder.SubEntries)
                {
                    if (entry.Value is CASCFile)
                        count++;
                    else
                        count += GetFilesCount(entry.Value as CASCFolder, null);
                }
            }

            return count;
        }

        private void ExtractData(CASCFolder folder, IEnumerable<int> selection)
        {
            if (selection != null)
            {
                foreach (int index in selection)
                {
                    ICASCEntry entry = folder.SubEntries.ElementAt(index).Value;

                    if (entry is CASCFile)
                        ExtractFile(entry as CASCFile);
                    else
                        ExtractData(entry as CASCFolder, null);
                }
            }
            else
            {
                foreach (var entry in folder.SubEntries)
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

            ExtractPath = folderBrowserDialog1.SelectedPath;
            textBox1.Text = ExtractPath;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            backgroundWorker1.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            ExtractData(folder, selection);
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Hide();
        }
    }
}
