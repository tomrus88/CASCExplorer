using System.Drawing;
using System.Windows.Forms;

namespace CASCExplorer
{
    public partial class ImagePreviewForm : Form
    {
        private readonly Bitmap _bitmap;

        public ImagePreviewForm(Bitmap bitmap)
        {
            _bitmap = bitmap;
            InitializeComponent();
            Size = bitmap.Size;
        }

        private void FormPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(_bitmap, 0, 0);
        }
    }
}
