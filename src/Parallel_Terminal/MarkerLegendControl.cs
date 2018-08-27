using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Parallel_Terminal
{
    public partial class MarkerLegendControl : Control
    {
        public TerminalControl Source;
        public MarkerLegendControl()
        {            
            InitializeComponent();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);

            Graphics g = pe.Graphics;

            const int LeftMargin = 10;

            SizeF szGenericText = g.MeasureString("gY", Font);

            int ClientHeight = ClientRectangle.Height;
            int TopMargin = (int)(ClientHeight / 2 - szGenericText.Height / 2);
            int VerticalBarMargin = 5;

            if (Source == null)
            {
                g.DrawString("No source provided.", Font, Brushes.Black, new PointF(LeftMargin, TopMargin));
                return;
            }            

            List<Slave> Markers = Source.GetMarkerLegend();            

            int xx = LeftMargin;
            int MarkerWidth = 4;
            int TextMargin = 3;
            int BetweenMarkers = 8;
            for (int ii = 0; ii < Markers.Count; ii++)
            {
                Slave ss = Markers[ii];                

                Rectangle rr = new Rectangle(xx, VerticalBarMargin, MarkerWidth, ClientHeight - 2 * VerticalBarMargin);
                g.FillRectangle(new SolidBrush(ss.DarkColor), rr);
                xx += MarkerWidth + TextMargin;
                g.DrawString(ss.HostName, Font, Brushes.Black, new PointF(xx, TopMargin));
                SizeF szText = g.MeasureString(ss.HostName, Font);
                xx += (int)szText.Width;
                xx += TextMargin + BetweenMarkers;
            }
        }
    }
}
