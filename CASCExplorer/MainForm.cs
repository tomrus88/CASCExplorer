using CASCExplorer.Properties;
using SereniaBLPLib;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class MainForm : Form
    {
        private ScanForm scanForm;
        private SearchForm searchForm;
        private ExtractProgress extractProgress;
        private CASCHandler CASC;
        private CASCFolder Root;
        private CASCEntrySorter Sorter = new CASCEntrySorter();
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

        private void Form1_Load(object sender, EventArgs e)
        {
            iconsList.Images.Add(Resources.folder);
            iconsList.Images.Add(Resources.openFolder);
            iconsList.Images.Add(SystemIcons.WinLogo);

            folderTree.SelectedImageIndex = 1;

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

            NameValueCollection onlineStorageList = (NameValueCollection)ConfigurationManager.GetSection("OnlineStorageList");

            if (onlineStorageList != null)
            {
                foreach (string game in onlineStorageList)
                {
                    var item = new ToolStripMenuItem(onlineStorageList[game]);
                    item.Tag = game;
                    openOnlineStorageToolStripMenuItem.DropDownItems.Add(item);
                }
            }
            else
            {
                openOnlineStorageToolStripMenuItem.Enabled = false;
            }

            useLWToolStripMenuItem.Checked = Settings.Default.ContentFlags == ContentFlags.LowViolence;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.ExceptionObject.ToString());
        }

        private void OnStorageChanged()
        {
            CASCConfig cfg = CASC.Config;

            bool isWoW = cfg.BuildUID.IndexOf("wow") >= 0;
            bool isD3 = cfg.BuildUID.IndexOf("d3") >= 0;
            bool isPro = cfg.BuildUID.IndexOf("pro") >= 0;

            scanFilesToolStripMenuItem.Enabled = isWoW;
            analyseUnknownFilesToolStripMenuItem.Enabled = isWoW || isPro;
            localeFlagsToolStripMenuItem.Enabled = isWoW || isD3 || isPro;
            useLWToolStripMenuItem.Enabled = isWoW;

            TreeNode node = new TreeNode() { Name = Root.Name, Tag = Root, Text = "Root [Read only]" };
            folderTree.Nodes.Add(node);
            node.Nodes.Add(new TreeNode() { Name = "tempnode" }); // add dummy node
            node.Expand();
            folderTree.SelectedNode = node;

            if (cfg.OnlineMode)
            {
                cDNToolStripMenuItem.Enabled = true;
                foreach (var bld in cfg.Builds)
                {
                    cDNToolStripMenuItem.DropDownItems.Add(bld["build-name"][0]);
                }
            }

            statusProgress.Visible = false;
            statusLabel.Text = string.Format("Loaded {0} files ({1} names missing)", CASC.Root.CountSelect - CASC.Root.CountUnknown, CASC.Root.CountUnknown);
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            UpdateListView(e.Node.Tag as CASCFolder);

            statusLabel.Text = e.Node.FullPath;
        }

        private void UpdateListView(CASCFolder baseEntry)
        {
            // Sort
            baseEntry.Entries = baseEntry.Entries.OrderBy(v => v.Value, Sorter).ToDictionary(pair => pair.Key, pair => pair.Value);

            // Update
            fileList.Tag = baseEntry;
            fileList.VirtualListSize = 0;
            fileList.VirtualListSize = baseEntry.Entries.Count;

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

                var orderedEntries = baseEntry.Entries.OrderBy(v => v.Value.Name);

                // Create nodes dynamically
                foreach (var it in orderedEntries)
                {
                    CASCFolder entry = it.Value as CASCFolder;

                    if (entry != null && node.Nodes[entry.Name] == null)
                    {
                        TreeNode newNode = node.Nodes.Add(entry.Name);
                        newNode.Tag = entry;
                        newNode.Name = entry.Name;

                        if (entry.Entries.Count(v => v.Value is CASCFolder) > 0)
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

            if (e.ItemIndex < 0 || e.ItemIndex >= folder.Entries.Count)
                return;

            ICASCEntry entry = folder.Entries.ElementAt(e.ItemIndex).Value;

            var localeFlags = LocaleFlags.None;
            var contentFlags = ContentFlags.None;
            var size = "<DIR>";

            if (entry is CASCFile)
            {
                var rootInfosLocale = CASC.Root.GetEntries(entry.Hash);

                if (rootInfosLocale.Any())
                {
                    var enc = CASC.Encoding.GetEntry(rootInfosLocale.First().MD5);

                    if (enc != null)
                        size = enc.Size.ToString("N", sizeNumberFmt);
                    else
                        size = "0";

                    foreach (var rootInfo in rootInfosLocale)
                    {
                        if (rootInfo.Block != null)
                        {
                            localeFlags |= rootInfo.Block.LocaleFlags;
                            contentFlags |= rootInfo.Block.ContentFlags;
                        }
                    }
                }
                else
                {
                    var installInfos = CASC.Install.GetEntries(entry.Hash);

                    if (installInfos.Any())
                    {
                        var enc = CASC.Encoding.GetEntry(installInfos.First().MD5);

                        if (enc != null)
                            size = enc.Size.ToString("N", sizeNumberFmt);
                        else
                            size = "0";

                        //foreach (var rootInfo in rootInfosLocale)
                        //{
                        //    if (rootInfo.Block != null)
                        //    {
                        //        localeFlags |= rootInfo.Block.LocaleFlags;
                        //        contentFlags |= rootInfo.Block.ContentFlags;
                        //    }
                        //}
                    }
                }
            }

            var item = new ListViewItem(new string[]
            {
                entry.Name,
                entry is CASCFolder ? "Folder" : Path.GetExtension(entry.Name),
                localeFlags.ToString(),
                contentFlags.ToString(),
                size
            })
            { ImageIndex = entry is CASCFolder ? 0 : 2 };

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

            var file = folder.Entries.ElementAt(fileList.SelectedIndex).Value as CASCFile;

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
                    //case ".wav":
                    //case ".ogg":
                    //    {
                    //        PreviewSound(file);
                    //        break;
                    //    }
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
            using (var stream = CASC.OpenFile(file.Hash, file.FullName))
            {
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
        }

        private void PreviewBlp(CASCFile file)
        {
            using (var stream = CASC.OpenFile(file.Hash, file.FullName))
            {
                var blp = new BlpFile(stream);
                var bitmap = blp.GetBitmap(0);
                var form = new ImagePreviewForm(bitmap);
                form.Show(this);
            }
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
            CASCFolder baseEntry = folder.Entries.ElementAt(fileList.SelectedIndex).Value as CASCFolder;

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
            using (AboutBox about = new AboutBox())
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

        private void scanFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CASC == null || Root == null)
                return;

            if (scanForm == null)
            {
                scanForm = new ScanForm();
                scanForm.Initialize(CASC, Root);
            }
            scanForm.Reset();
            scanForm.ShowDialog();
        }

        private async void analyseUnknownFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CASC == null)
                return;

            try
            {
                statusProgress.Value = 0;
                statusProgress.Visible = true;
                analyseUnknownFilesToolStripMenuItem.Enabled = false;

                statusLabel.Text = "Analysing...";

                IProgress<int> progress = new Progress<int>((p) => statusProgress.Value = p);

                await Task.Run(() =>
                {
                    FileScanner scanner = new FileScanner(CASC, Root);

                    Dictionary<int, string> idToName = new Dictionary<int, string>();

                    if (CASC.Config.BuildUID.StartsWith("wow"))
                    {
                        using (Stream stream = CASC.OpenFile("DBFilesClient\\SoundEntries.db2"))
                        {
                            DB2Reader se = new DB2Reader(stream);

                            foreach (var row in se)
                            {
                                string name = row.Value.GetField<string>(2);

                                int type = row.Value.GetField<int>(1);

                                bool many = row.Value.GetField<int>(4) > 0;

                                for (int i = 3; i < 23; i++)
                                    idToName[row.Value.GetField<int>(i)] = "unknown\\sound\\" + name + (many ? "_" + (i - 2).ToString("D2") : "") + (type == 28 ? ".mp3" : ".ogg");
                            }
                        }
                    }

                    CASCFolder unknownFolder = Root.GetEntry("unknown") as CASCFolder;

                    if (unknownFolder == null)
                        return;

                    IEnumerable<CASCFile> files = unknownFolder.GetFiles(null, true);
                    int numTotal = files.Count();
                    int numDone = 0;

                    foreach (var unknownEntry in files)
                    {
                        CASCFile unknownFile = unknownEntry as CASCFile;

                        string name;
                        if (idToName.TryGetValue(CASC.Root.GetEntries(unknownFile.Hash).First().FileDataId, out name))
                            unknownFile.FullName = name;
                        else
                        {
                            string ext = scanner.GetFileExtension(unknownFile);
                            unknownFile.FullName += ext;
                        }

                        progress.Report((int)(++numDone / (float)numTotal * 100.0f));
                    }

                    CASC.Root.Dump();
                });

                statusLabel.Text = "All unknown files has been analyzed.";
            }
            catch (Exception exc)
            {
                statusLabel.Text = "Failed to analyze unknown files.";
                MessageBox.Show("Error during analysis of unknown files:\n" + exc.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                statusProgress.Value = 0;
                statusProgress.Visible = false;
                analyseUnknownFilesToolStripMenuItem.Enabled = true;
            }
        }

        private void localeToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (CASC == null)
                return;

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
            CASC.Install.MergeData(Root);
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

            long size = files.Sum(f => (long)f.GetSize(CASC));

            MessageBox.Show(string.Format(sizeNumberFmt, "{0:N} bytes", size));
        }

        private void contentFlagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CASC == null)
                return;

            useLWToolStripMenuItem.Checked = !useLWToolStripMenuItem.Checked;

            if (useLWToolStripMenuItem.Checked)
                Settings.Default.ContentFlags = ContentFlags.LowViolence;
            else
                Settings.Default.ContentFlags = ContentFlags.None;

            Settings.Default.Save();

            Root = CASC.Root.SetFlags(Settings.Default.LocaleFlags, Settings.Default.ContentFlags);
            CASC.Install.MergeData(Root);
            OnStorageChanged();
        }

        private void openStorageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (storageFolderBrowserDialog.ShowDialog() != DialogResult.OK)
            {
                MessageBox.Show("Please select storage folder!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(Path.Combine(storageFolderBrowserDialog.SelectedPath, ".build.info")))
            {
                MessageBox.Show("Invalid storage folder selected!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Cleanup();

            using (var initForm = new InitForm())
            {
                initForm.LoadLocalStorage(storageFolderBrowserDialog.SelectedPath);

                DialogResult res = initForm.ShowDialog();

                if (res != DialogResult.OK)
                    return;

                CASC = initForm.CASC;
                Root = initForm.Root;
            }

            Sorter.CASC = CASC;

            OnStorageChanged();
        }

        private void openLastStorageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not implemented!");
        }

        private void Cleanup()
        {
            Sorter.CASC = null;

            if (CASC != null)
            {
                CASC.Clear();
                CASC = null;
            }

            Root = null;

            fileList.VirtualListSize = 0;
            folderTree.Nodes.Clear();
            cDNToolStripMenuItem.Enabled = false;
            cDNToolStripMenuItem.DropDownItems.Clear();

            scanFilesToolStripMenuItem.Enabled = false;
            analyseUnknownFilesToolStripMenuItem.Enabled = false;
            localeFlagsToolStripMenuItem.Enabled = false;
            useLWToolStripMenuItem.Enabled = false;
            statusLabel.Text = "Ready.";
            statusProgress.Visible = false;

            GC.Collect();
        }

        private void findToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (searchForm == null)
                searchForm = new SearchForm(fileList);

            searchForm.Show(this);
        }

        private void fileList_SearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
        {
            bool ignoreCase = true;
            bool searchUp = false;
            int SelectedIndex = fileList.SelectedIndex;

            CASCFolder folder = fileList.Tag as CASCFolder;

            var comparisonType = ignoreCase
                                    ? StringComparison.InvariantCultureIgnoreCase
                                    : StringComparison.InvariantCulture;

            if (searchUp)
            {
                for (var i = SelectedIndex - 1; i >= 0; --i)
                {
                    var op = folder.Entries.ElementAt(i).Value.Name;
                    if (op.IndexOf(e.Text, comparisonType) != -1)
                    {
                        e.Index = i;
                        break;
                    }
                }
            }
            else
            {
                for (int i = SelectedIndex + 1; i < fileList.Items.Count; ++i)
                {
                    var op = folder.Entries.ElementAt(i).Value.Name;
                    if (op.IndexOf(e.Text, comparisonType) != -1)
                    {
                        e.Index = i;
                        break;
                    }
                }
            }
        }

        private async void dumpInstallToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CASC == null)
                return;

            try
            {
                statusProgress.Value = 0;
                statusProgress.Visible = true;
                dumpInstallToolStripMenuItem.Enabled = false;

                statusLabel.Text = "Extracting...";

                IProgress<int> progress = new Progress<int>((p) => statusProgress.Value = p);

                await Task.Run(() =>
                {
                    var installFiles = CASC.Install.GetEntries("Windows");
                    var build = CASC.Config.BuildName;

                    int numFiles = installFiles.Count();
                    int numDone = 0;

                    foreach (var file in installFiles)
                    {
                        CASC.ExtractFile(CASC.Encoding.GetEntry(file.MD5).Key, "data\\" + build + "\\install_files", file.Name);

                        progress.Report((int)(++numDone / (float)numFiles * 100.0f));
                    }
                });

                statusLabel.Text = "All install files has been extracted.";
            }
            catch (Exception exc)
            {
                statusLabel.Text = "Failed to extract install files.";
                MessageBox.Show("Error during extraction of install files:\n" + exc.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                statusProgress.Value = 0;
                statusProgress.Visible = false;
                dumpInstallToolStripMenuItem.Enabled = true;
            }
        }

        private void extractRootFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CASC == null)
                return;

            var files = new Dictionary<string, byte[]>()
            {
                { "root", CASC.Encoding.GetEntry(CASC.Config.RootMD5).Key },
                { "install", CASC.Encoding.GetEntry(CASC.Config.InstallMD5).Key },
                { "encoding", CASC.Config.EncodingKey },
                { "download", CASC.Encoding.GetEntry(CASC.Config.DownloadMD5).Key }
            };

            foreach (var file in files)
            {
                CASC.ExtractFile(file.Value, ".", file.Key);
            }
        }

        private void bruteforceNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (BruteforceForm bf = new BruteforceForm())
                bf.ShowDialog();
        }

        private void openOnlineStorageToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            Cleanup();

            using (var initForm = new InitForm())
            {
                initForm.LoadOnlineStorage((string)e.ClickedItem.Tag);

                DialogResult res = initForm.ShowDialog();

                if (res != DialogResult.OK)
                    return;

                CASC = initForm.CASC;
                Root = initForm.Root;
            }

            Sorter.CASC = CASC;

            OnStorageChanged();
        }

        private void closeStorageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cleanup();
        }
    }
}
