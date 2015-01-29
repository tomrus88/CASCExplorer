using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class ScanForm : Form
    {
        private static readonly Jenkins96 Hasher = new Jenkins96();

        private FileScanner scanner;
        private HashSet<ScanResult> uniqueFileNames = new HashSet<ScanResult>();
        private Dictionary<string, int> extCounter = new Dictionary<string, int>();

        private CASCHandler CASC;
        private CASCFolder Root;

        private int NumFiles;
        private int NumScanned;
        private bool running = false;

        private class ScanResult
        {
            public string NewFile { get; set; }
            public string FoundInFile { get; set; }

            public ScanResult(string newFileName, string foundInFileName)
            {
                NewFile = newFileName;
                FoundInFile = foundInFileName;
            }

            public override int GetHashCode()
            {
                return NewFile.ToLower().GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                    return false;
                if (!(obj is ScanResult))
                    return false;
                ScanResult res = (ScanResult)obj;
                if (!res.NewFile.ToLower().Equals(NewFile.ToLower()))
                    return false;
                return true;
            }

            public override string ToString()
            {
                return NewFile + " (in: " + FoundInFile + ")";
            }
        }

        public ScanForm()
        {
            InitializeComponent();
        }

        public void Initialize(CASCHandler casc, CASCFolder root)
        {
            CASC = casc;
            Root = root;
            scanner = new FileScanner(CASC, Root);
        }

        public void Reset()
        {
            running = false;
            NumScanned = 0;
            NumFiles = CASC.Root.CountTotal;
            scanButton.Enabled = true;
            scanButton.Text = "Start";
            scanProgressBar.Value = 0;
            scanLabel.Text = "Ready";
            progressLabel.Text = "";
            filenameTextBox.Text = "";
            uniqueFileNames.Clear();
            extCounter.Clear();
        }

        private void updateExtCounter(string ext, int count)
        {
            if (extCounter.ContainsKey(ext))
                extCounter[ext] += count;
            else
                extCounter.Add(ext, count);
        }

        private void UpdateFileNames(string newFileName, string foundInFileName)
        {
            updateExtCounter(Path.GetExtension(foundInFileName).ToLower(), 1);
            ScanResult res = new ScanResult(newFileName.Replace("/", "\\"), foundInFileName);
            uniqueFileNames.Add(res);
            filenameTextBox.AppendText(res.ToString() + Environment.NewLine);
        }

        // public delegate void UpdateFileNameBoxCallback(string newFileName, string foundInFileName);

        private void RefineFileNames()
        {
            // display unique file names without finding place
            filenameTextBox.Clear();
            foreach (var uniqueFileName in uniqueFileNames)
                filenameTextBox.AppendText(uniqueFileName.NewFile + Environment.NewLine);

            // display stats
            filenameTextBox.AppendText(Environment.NewLine + Environment.NewLine + "Found file names in the following file types (some types were skipped):" + Environment.NewLine);
            foreach (var ext in extCounter)
                filenameTextBox.AppendText(ext.Value + "x in \"" + ext.Key + "\"" + Environment.NewLine);
        }

        private void scanButton_Click(object sender, EventArgs e)
        {
            running = !running;
            if (running)
            {
                scanButton.Text = "Cancel";
                scanBackgroundWorker.RunWorkerAsync();
            }
            else
            {
                scanButton.Enabled = false;
                scanBackgroundWorker.CancelAsync();
            }
        }

        private void scanBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                Scan();
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
        }

        private void scanBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            scanProgressBar.Value = e.ProgressPercentage;
            if (e.UserState is ScanProgressState)
            {
                ScanProgressState state = (ScanProgressState)e.UserState;
                scanLabel.Text = "Scanning '" + state.CurrentFileName + "' ...";
                progressLabel.Text = state.NumFilesScanned + "/" + state.NumFilesTotal;
                //if (state.MissingFileName != null)
                //{
                //    ScanResult res = new ScanResult(state.MissingFileName.Replace("/", "\\"), state.CurrentFileName);
                //    uniqueFileNames.Add(res);
                //    UpdateFileNameBox();
                //    Console.WriteLine(res.ToString());
                //}
            }
        }

        private void scanBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            running = false;
            scanButton.Enabled = false;
            scanLabel.Text = "Scan completed.";
            scanProgressBar.Value = 100;
            RefineFileNames();
            //if (e.Cancelled)
            //    Reset();
        }

        private void Scan()
        {
            ScanFolder(Root);
        }

        private void ScanFolder(CASCFolder folder)
        {
            foreach (var entry in folder.SubEntries)
            {
                if (entry.Value is CASCFile)
                {
                    var rootEntries = CASC.Root.GetEntries(entry.Value.Hash);

                    foreach (var rootEntry in rootEntries)
                        ScanFile(entry.Value as CASCFile);
                }
                else
                    ScanFolder(entry.Value as CASCFolder);
            }
        }

        private class ScanProgressState
        {
            public int NumFilesScanned;
            public int NumFilesTotal;
            public string CurrentFileName;
        }

        private void ScanFile(CASCFile file)
        {
            if (scanBackgroundWorker.CancellationPending)
                throw new OperationCanceledException();
            NumScanned++;

            var ext = Path.GetExtension(file.FullName).ToLower();
            this.BeginInvoke((MethodInvoker)(() => updateExtCounter(ext, 0)));

            HashSet<string> fileNames = scanner.ScanFile(file);

            if (fileNames != null)
            {
                // only report progress when not skipping a file, it's faster that way
                int progress = (int)((float)NumScanned / (float)NumFiles * 100);
                ScanProgressState state = new ScanProgressState();
                state.NumFilesScanned = NumScanned;
                state.NumFilesTotal = NumFiles;
                state.CurrentFileName = file.FullName;
                scanBackgroundWorker.ReportProgress(progress, state);

                foreach (var fileName in fileNames)
                {
                    ulong hash = Hasher.ComputeHash(fileName);

                    if ((CASC.Root as WowRootHandler).IsUnknownFile(hash))
                    {
                        this.BeginInvoke((MethodInvoker)(() => UpdateFileNames(fileName, file.FullName)));
                    }
                    else if (CASC.FileExists(hash))
                    {
                        this.BeginInvoke((MethodInvoker)(() => updateExtCounter(ext, 1)));
                    }
                }
            }
        }
    }
}
