using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CASCExplorer.Properties;
using SereniaBLPLib;

namespace CASCExplorer
{
    public partial class MainForm : Form
    {
        CASCFolder root;
        CASCHandler cascHandler;
        ExtractProgress extractProgress;

        public MainForm()
        {
            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            iconsList.Images.Add(Resources.folder);
            iconsList.Images.Add(Resources.openFolder);
            iconsList.Images.Add(SystemIcons.WinLogo);

            folderTree.SelectedImageIndex = 1;

            statusLabel.Text = "Loading...";

            loadDataWorker.RunWorkerAsync();
        }

        private void loadDataWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            statusProgress.Value = e.ProgressPercentage;
        }

        private void loadDataWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show("Error initializing required data files:\n" + e.Error.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //else if(e.Cancelled)
            //{

            //}
            else
            {
                TreeNode node = folderTree.Nodes.Add("Root [Read only]");
                node.Tag = root;
                node.Name = root.Name;
                node.Nodes.Add(new TreeNode() { Name = "tempnode" });
                node.Expand();
                folderTree.SelectedNode = node;

                int numFileNames = (int)e.Result;

                statusProgress.Visible = false;
                statusLabel.Text = String.Format("Loaded {0} files ({1} names missing)", numFileNames, cascHandler.NumRootEntries - numFileNames);
            }
        }

        private void loadDataWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;

            cascHandler = Settings.Default.OnlineMode
                ? CASCHandler.OpenOnlineStorage(worker)
                : CASCHandler.OpenLocalStorage(Settings.Default.WowPath, worker);

            root = cascHandler.LoadListFile(Path.Combine(Application.StartupPath, "listfile.txt"));
            e.Result = CASCFile.FileNames.Count;
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            UpdateListView(e.Node.Tag as CASCFolder);

            statusLabel.Text = e.Node.FullPath;
        }

        private void UpdateListView(CASCFolder baseEntry)
        {
            // Sort
            Dictionary<ulong, ICASCEntry> orderedEntries;

            if (fileList.Sorting == SortOrder.Ascending)
                orderedEntries = baseEntry.SubEntries.OrderBy(v => v.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
            else
                orderedEntries = baseEntry.SubEntries.OrderByDescending(v => v.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

            baseEntry.SubEntries = orderedEntries;

            // Update
            fileList.Tag = baseEntry;
            fileList.VirtualListSize = 0;
            fileList.VirtualListSize = baseEntry.SubEntries.Count;
            fileList.EnsureVisible(0);
            fileList.SelectedIndices.Add(0);
            fileList.FocusedItem = fileList.Items[0];
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;

            CASCFolder baseEntry = node.Tag as CASCFolder;

            if (node.Nodes["tempnode"] != null)
            {
                node.Nodes.Clear();

                var orderedEntries = baseEntry.SubEntries.OrderBy(v => v.Value);

                // Create nodes dynamically
                foreach (var it in orderedEntries)
                {
                    CASCFolder entry = it.Value as CASCFolder;

                    if (entry != null && node.Nodes[entry.Name] == null)
                    {
                        TreeNode newNode = node.Nodes.Add(entry.Name);
                        newNode.Tag = entry;
                        newNode.Name = entry.Name;

                        if (entry.SubEntries.Count(v => v.Value is CASCFolder) > 0)
                            newNode.Nodes.Add(new TreeNode() { Name = "tempnode" });
                    }
                }
            }
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            fileList.Sorting = fileList.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;

            UpdateListView(fileList.Tag as CASCFolder);
        }

        private void listView1_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            CASCFolder folder = fileList.Tag as CASCFolder;

            if (folder == null)
                return;

            if (e.ItemIndex < 0 || e.ItemIndex >= folder.SubEntries.Count)
                return;

            ICASCEntry entry = folder.SubEntries.ElementAt(e.ItemIndex).Value;

            var flags = LocaleFlags.None;

            if (entry is CASCFile)
            {
                var rootInfos = cascHandler.GetRootInfo(entry.Hash);

                if (rootInfos == null)
                    throw new Exception("root entry missing!");

                foreach (var rootInfo in rootInfos)
                    flags |= rootInfo.Block.Flags;
            }

            var item = new ListViewItem(new string[] { entry.Name, entry is CASCFolder ? "Folder" : "File", flags.ToString() });
            item.ImageIndex = entry is CASCFolder ? 0 : 2;
            e.Item = item;
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!NavigateFolder())
                PreviewFile();
        }

        private void PreviewFile()
        {
            CASCFolder folder = fileList.Tag as CASCFolder;

            if (folder == null)
                return;

            var files = folder.GetFiles(fileList.SelectedIndices.Cast<int>());

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.Name);
                if (extension != null)
                {
                    switch (extension.ToLower())
                    {
                        case ".blp":
                            {
                                PreviewBlp(file.FullName);
                                break;
                            }
                        case ".txt":
                        case ".ini":
                        case ".wtf":
                        case ".lua":
                        case ".toc":
                        case ".xml":
                        case ".htm":
                        case ".html":
                        case ".lst":
                            {
                                PreviewText(file.FullName);
                                break;
                            }
                        default:
                            {
                                MessageBox.Show(string.Format("Preview of {0} is not supported yet", extension), "Not supported file");
                                break;
                            }
                    }
                }
            }
        }

        private void PreviewText(string fullName)
        {
            var stream = cascHandler.OpenFile(fullName, LocaleFlags.All);
            var text = new StreamReader(stream).ReadToEnd();
            var form = new Form { FormBorderStyle = FormBorderStyle.SizableToolWindow };
            form.Controls.Add(new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Text = text,
                ScrollBars = ScrollBars.Both
            });
            form.Show();
        }

        private void PreviewBlp(string fullName)
        {
            var stream = cascHandler.OpenFile(fullName, LocaleFlags.All);
            var blp = new BlpFile(stream);
            var bitmap = blp.GetBitmap(0);
            var form = new ImagePreviewForm(bitmap)
            {
                StartPosition = FormStartPosition.CenterParent
            };
            form.Show(this);
        }

        private void listView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                NavigateFolder();
            else if (e.KeyCode == Keys.Back)
            {
                TreeNode node = folderTree.SelectedNode;
                if (node != null && node != folderTree.Nodes["root"])
                    folderTree.SelectedNode = node.Parent;
            }
        }

        private bool NavigateFolder()
        {
            // Current folder
            CASCFolder folder = fileList.Tag as CASCFolder;

            if (folder == null)
                return false;

            // Selected folder
            CASCFolder baseEntry = folder.SubEntries.ElementAt(fileList.SelectedIndices[0]).Value as CASCFolder;

            if (baseEntry == null)
                return false;

            folderTree.SelectedNode.Expand();
            folderTree.SelectedNode.Nodes[baseEntry.Name].Expand();
            folderTree.SelectedNode = folderTree.SelectedNode.Nodes[baseEntry.Name];

            UpdateListView(baseEntry);

            statusLabel.Text = folderTree.SelectedNode.FullPath;
            return true;
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CASCFolder folder = fileList.Tag as CASCFolder;

            if (folder == null)
                return;

            if (extractProgress == null)
                extractProgress = new ExtractProgress();

            var files = folder.GetFiles(fileList.SelectedIndices.Cast<int>()).ToList();
            extractProgress.SetExtractData(cascHandler, files);
            extractProgress.ShowDialog();
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            toolStripMenuItem1.Enabled = fileList.SelectedIndices.Count > 0;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox about = new AboutBox();
            about.ShowDialog();
        }
    }
}
