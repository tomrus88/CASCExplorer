using System.Windows.Forms;

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
            get { return HasSingleSelection ? SelectedIndices[0] : -1; }
            set
            {
                SelectedIndices.Clear();

                if (value >= 0)
                    SelectedIndices.Add(value);
            }
        }
    }
}
