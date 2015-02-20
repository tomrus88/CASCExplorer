using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class SearchForm : Form
    {
        private NoFlickerListView filelist;
        private int SearchIndex;

        public SearchForm(NoFlickerListView filelist)
        {
            this.filelist = filelist;

            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CASCFolder folder = filelist.Tag as CASCFolder;

            if (SearchIndex >= folder.SubEntries.Count)
                SearchIndex = 0;

            var item = filelist.FindItemWithText(textBox1.Text, false, filelist.SelectedIndex, true);

            if (item != null)
            {
                filelist.EnsureVisible(item.Index);
                filelist.SelectedIndices.Clear();
                filelist.SelectedIndices.Add(item.Index);
            }
            else
            {
                MessageBox.Show(string.Format("Can't find:'{0}'", textBox1.Text),
                                "CASCExplorer",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
        }
    }
}
