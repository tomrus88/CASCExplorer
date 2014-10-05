using System;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CASCExplorer.Properties;
using SereniaBLPLib;

namespace CASCExplorer
{
    public partial class MainForm : Form
    {
        private ExtractProgress extractProgress;
        private CASCHandler CASC;
        private CASCFolder Root;
        private AsyncAction bgAction;
        private CASCEntrySorter Sorter;
        private NumberFormatInfo sizeNumberFmt = new NumberFormatInfo()
        {
            NumberGroupSizes = new int[] { 3, 3, 3, 3, 3 },
            NumberDecimalDigits = 0,
            NumberGroupSeparator = " "
        };

        public MainForm()
        {
            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            iconsList.Images.Add(Resources.folder);
            iconsList.Images.Add(Resources.openFolder);
            iconsList.Images.Add(SystemIcons.WinLogo);

            folderTree.SelectedImageIndex = 1;

            onlineModeToolStripMenuItem.Checked = Settings.Default.OnlineMode;

            statusLabel.Text = "Loading...";

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var locales = Enum.GetNames(typeof(LocaleFlags));
            foreach (var locale in locales)
            {
                if (locale == "None")
                    continue;

                var item = new ToolStripMenuItem(locale);
                item.Checked = Settings.Default.LocaleFlags.ToString() == locale;
                localeFlagsToolStripMenuItem.DropDownItems.Add(item);
            }

            useLWToolStripMenuItem.Checked = Settings.Default.ContentFlags == ContentFlags.LowViolence;

            bgAction = new AsyncAction(() => LoadData());
            bgAction.ProgressChanged += new EventHandler<AsyncActionProgressChangedEventArgs>(bgAction_ProgressChanged);

            try
            {
                await bgAction.DoAction();

                OnStorageChanged();
            }
            catch (OperationCanceledException)
            {
                Application.Exit();
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error during initialization of required data files:\n" + exc.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void bgAction_ProgressChanged(object sender, AsyncActionProgressChangedEventArgs progress)
        {
            if (bgAction.IsCancellationRequested)
                return;

            statusProgress.Value = progress.Progress;

            if (progress.UserData != null)
                statusLabel.Text = progress.UserData as string;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString());
        }

        private void OnStorageChanged()
        {
            folderTree.Nodes.Clear();

            TreeNode node = folderTree.Nodes.Add("Root [Read only]");
            node.Tag = Root;
            node.Name = Root.Name;
            node.Nodes.Add(new TreeNode() { Name = "tempnode" }); // add dummy node
            node.Expand();
            folderTree.SelectedNode = node;

            statusProgress.Visible = false;
            statusLabel.Text = String.Format("Loaded {0} files ({1} names missing)", CASC.Root.CountSelect - CASC.Root.CountUnknown, CASC.Root.CountUnknown);
        }

        private void LoadData()
        {
            CASC = Settings.Default.OnlineMode
                ? CASCHandler.OpenOnlineStorage(Settings.Default.Product, bgAction)
                : CASCHandler.OpenLocalStorage(Settings.Default.WowPath, bgAction);

            CASC.Root.LoadListFile(Path.Combine(Application.StartupPath, "listfile.txt"), bgAction);
            Root = CASC.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);

            Sorter = new CASCEntrySorter(CASC);
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            UpdateListView(e.Node.Tag as CASCFolder);

            statusLabel.Text = e.Node.FullPath;
        }

        private void UpdateListView(CASCFolder baseEntry)
        {
            // Sort
            baseEntry.SubEntries = baseEntry.SubEntries.OrderBy(v => v.Value, Sorter).ToDictionary(pair => pair.Key, pair => pair.Value);

            // Update
            fileList.Tag = baseEntry;
            fileList.VirtualListSize = 0;
            fileList.VirtualListSize = baseEntry.SubEntries.Count;

            if (fileList.VirtualListSize > 0)
            {
                fileList.EnsureVisible(0);
                fileList.SelectedIndex = 0;
                fileList.FocusedItem = fileList.Items[0];
            }
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;

            CASCFolder baseEntry = node.Tag as CASCFolder;

            // check if we have dummy node
            if (node.Nodes["tempnode"] != null)
            {
                // remove dummy node
                node.Nodes.Clear();

                var orderedEntries = baseEntry.SubEntries.OrderBy(v => v.Value.Name);

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
                            newNode.Nodes.Add(new TreeNode() { Name = "tempnode" }); // add dummy node
                    }
                }
            }
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            Sorter.SortColumn = e.Column;
            Sorter.Order = Sorter.Order == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
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

            var localeFlags = LocaleFlags.None;
            var contentFlags = ContentFlags.None;
            var size = "<DIR>";

            if (entry is CASCFile)
            {
                var rootInfosLocale = CASC.Root.GetEntries(entry.Hash, Settings.Default.LocaleFlags, Settings.Default.ContentFlags);

                if (rootInfosLocale.Any())
                {
                    var enc = CASC.Encoding.GetEntry(rootInfosLocale.First().MD5);

                    if (enc != null)
                        size = enc.Size.ToString("N", sizeNumberFmt);
                    else
                        size = "0";

                    foreach (var rootInfo in rootInfosLocale)
                    {
                        localeFlags |= rootInfo.Block.LocaleFlags;
                        contentFlags |= rootInfo.Block.ContentFlags;
                    }
                }
            }

            var item = new ListViewItem(new string[]
            {
                entry.Name,
                entry is CASCFolder ? "Folder" : "File",
                localeFlags.ToString(),
                contentFlags.ToString(),
                size
            });

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

            if (!fileList.HasSingleSelection)
                return;

            var file = folder.SubEntries.ElementAt(fileList.SelectedIndex).Value as CASCFile;

            var extension = Path.GetExtension(file.Name);

            if (extension != null)
            {
                switch (extension.ToLower())
                {
                    case ".blp":
                        {
                            PreviewBlp(file);
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
                            PreviewText(file);
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

        private void PreviewText(CASCFile file)
        {
            var stream = CASC.OpenFile(file.Hash, file.FullName, LocaleFlags.All);
            var text = new StreamReader(stream).ReadToEnd();
            var form = new Form { FormBorderStyle = FormBorderStyle.SizableToolWindow, StartPosition = FormStartPosition.CenterParent };
            form.Controls.Add(new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Text = text,
                ScrollBars = ScrollBars.Both
            });
            form.Show(this);
        }

        private void PreviewBlp(CASCFile file)
        {
            var stream = CASC.OpenFile(file.Hash, file.FullName, LocaleFlags.All);
            var blp = new BlpFile(stream);
            var bitmap = blp.GetBitmap(0);
            var form = new ImagePreviewForm(bitmap);
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

            if (!fileList.HasSingleSelection)
                return false;

            // Selected folder
            CASCFolder baseEntry = folder.SubEntries.ElementAt(fileList.SelectedIndex).Value as CASCFolder;

            if (baseEntry == null)
                return false;

            folderTree.SelectedNode.Expand();
            folderTree.SelectedNode.Nodes[baseEntry.Name].Expand();
            folderTree.SelectedNode = folderTree.SelectedNode.Nodes[baseEntry.Name];

            UpdateListView(baseEntry);

            statusLabel.Text = folderTree.SelectedNode.FullPath;
            return true;
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CASCFolder folder = fileList.Tag as CASCFolder;

            if (folder == null)
                return;

            if (!fileList.HasSelection)
                return;

            if (extractProgress == null)
                extractProgress = new ExtractProgress();

            var files = folder.GetFiles(fileList.SelectedIndices.Cast<int>()).ToList();
            extractProgress.SetExtractData(CASC, files);
            extractProgress.ShowDialog();
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            extractToolStripMenuItem.Enabled = fileList.HasSelection;
            copyNameToolStripMenuItem.Enabled = (fileList.HasSelection && (fileList.Tag as CASCFolder).GetFiles(fileList.SelectedIndices.Cast<int>(), false).Count() > 0) || false;
            getSizeToolStripMenuItem.Enabled = fileList.HasSelection;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox about = new AboutBox();
            about.ShowDialog();
        }

        private void copyNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CASCFolder folder = fileList.Tag as CASCFolder;

            if (folder == null)
                return;

            if (!fileList.HasSelection)
                return;

            var files = folder.GetFiles(fileList.SelectedIndices.Cast<int>(), false).Select(f => f.FullName);

            string temp = string.Join(Environment.NewLine, files);

            Clipboard.SetText(temp);
        }

        private void onlineModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.OnlineMode = onlineModeToolStripMenuItem.Checked = !onlineModeToolStripMenuItem.Checked;
            Settings.Default.Save();

            MessageBox.Show("Please restart CASCExplorer to apply changes", "Restart required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            bgAction.Cancel();
        }

        private void localeToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var item = e.ClickedItem as ToolStripMenuItem;

            var parent = (sender as ToolStripMenuItem);

            foreach (var dropdown in parent.DropDownItems)
            {
                if (dropdown != item)
                    (dropdown as ToolStripMenuItem).Checked = false;
                else
                    (dropdown as ToolStripMenuItem).Checked = true;
            }

            Settings.Default.LocaleFlags = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), item.Text);
            Settings.Default.Save();

            Root = CASC.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);
            OnStorageChanged();
        }

        private void getSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CASCFolder folder = fileList.Tag as CASCFolder;

            if (folder == null)
                return;

            if (!fileList.HasSelection)
                return;

            var files = folder.GetFiles(fileList.SelectedIndices.Cast<int>());

            long size = files.Sum(f => (long)f.GetSize(CASC, Settings.Default.LocaleFlags, Settings.Default.ContentFlags));

            MessageBox.Show(String.Format(sizeNumberFmt, "{0:N} bytes", size));
        }

        private void contentFlagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            useLWToolStripMenuItem.Checked = !useLWToolStripMenuItem.Checked;

            if (useLWToolStripMenuItem.Checked)
                Settings.Default.ContentFlags = ContentFlags.LowViolence;
            else
                Settings.Default.ContentFlags = ContentFlags.None;

            Settings.Default.Save();

            Root = CASC.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);
            OnStorageChanged();
        }
    }
}
