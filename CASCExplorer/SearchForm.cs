using System;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class SearchForm : Form
    {
        private NoFlickerListView filelist;

        public SearchForm(NoFlickerListView filelist)
        {
            this.filelist = filelist;

            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (filelist.SelectedIndex < 0)
                return;

            var item = filelist.FindItemWithText(textBox1.Text, false, filelist.SelectedIndex, true);

            if (item != null)
            {
                filelist.EnsureVisible(item.Index);
                filelist.SelectedIndex = item.Index;
                filelist.FocusedItem = item;
            }
            else
            {
                MessageBox.Show(string.Format("Can't find:'{0}'", textBox1.Text),
                                "CASCExplorer",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
        }

        private void SearchForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                Owner.Activate();
            }
        }
    }
}
