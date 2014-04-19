using System.Windows.Forms;

namespace CASCExplorer
{
    class NoFlickerListView : ListView
    {
        public NoFlickerListView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
