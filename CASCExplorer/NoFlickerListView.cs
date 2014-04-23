using System.Windows.Forms;

namespace CASCExplorer
{
    class NoFlickerListView : ListView
    {
        public NoFlickerListView()
        {
            DoubleBuffered = true;
        }
    }
}
