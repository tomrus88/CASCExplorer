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
            get
            {
                int selCount = SelectedIndices.Count;

                if (selCount == 0)
                    return -1;
                else if (selCount == 1)
                    return SelectedIndices[0];
                else
                    return SelectedIndices.Cast<int>().Max();
            }
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
