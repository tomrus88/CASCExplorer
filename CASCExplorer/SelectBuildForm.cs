using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class SelectBuildForm : Form
    {
        public int SelectedIndex { get; private set; }

        public SelectBuildForm(CASCConfig config)
        {
            InitializeComponent();

            foreach (var cfg in config.Builds)
            {
                listBox1.Items.Add(cfg["build-name"][0]);
            }

            listBox1.SelectedIndex = 0;
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            SelectedIndex = listBox1.SelectedIndex;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
