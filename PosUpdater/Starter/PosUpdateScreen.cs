using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PosUpdater
{
    public partial class PosUpdateScreen : Form
    {
        private readonly Region _region;

        public PosUpdateScreen()
        {
            InitializeComponent();

            var border = GetRoundedRectanglePath(Bounds, new SizeF(50, 50));
            _region = new Region(border);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Region = _region;
        }

        private static GraphicsPath GetRoundedRectanglePath(RectangleF rect, SizeF roundSize)
        {
            var path = new GraphicsPath();
            path.AddLine(rect.Left + roundSize.Width/2, rect.Top, rect.Right - roundSize.Width/2, rect.Top);
            path.AddArc(rect.Right - roundSize.Width, rect.Top, roundSize.Width, roundSize.Height, 270, 90);
            path.AddLine(rect.Right, rect.Top + roundSize.Height/2, rect.Right, rect.Bottom - roundSize.Height/2);
            path.AddArc(rect.Right - roundSize.Width, rect.Bottom - roundSize.Height, roundSize.Width, roundSize.Height,0, 90);
            path.AddLine(rect.Right - roundSize.Width/2, rect.Bottom, rect.Left + roundSize.Width/2, rect.Bottom);
            path.AddArc(rect.Left, rect.Bottom - roundSize.Height, roundSize.Width, roundSize.Height, 90, 90);
            path.AddLine(rect.Left, rect.Bottom - roundSize.Height/2, rect.Left, rect.Top + roundSize.Height/2);
            path.AddArc(rect.Left, rect.Top, roundSize.Width, roundSize.Height, 180, 90);
            path.CloseFigure();
            return path;
        }

        private void PosUpdateScreen_Paint(object sender, PaintEventArgs e)
        {
            OnPaintBackground(e);
        }

        private void PosUpdateScreen_Load(object sender, System.EventArgs e)
        {
            Application.DoEvents();
        }
    }
}
