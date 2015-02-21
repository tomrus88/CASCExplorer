using System.Windows.Forms;
using System.Linq;

namespace CASCExplorer
{
    public class NoFlickerListView : ListView
    {
        public NoFlickerListView()
        {
            DoubleBuffered = true;
        }

        public bool HasSingleSelection
        {
            get { return SelectedIndices.Count == 1; }
        }

        public bool HasSelection
        {
            get { return SelectedIndices.Count >= 1; }
        }

        public int SelectedIndex
        {
            get { return HasSingleSelection ? SelectedIndices[0] : SelectedIndices.Cast<int>().Max(); }
            set
            {
                SelectedIndices.Clear();

                if (value >= 0)
                {
                    SelectedIndices.Add(value);
                    Items[value].Selected = true;
                }
            }
        }
    }
}
