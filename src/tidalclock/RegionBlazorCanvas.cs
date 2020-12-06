using Blazor.Extensions.Canvas.Canvas2D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Utilities;

namespace tidalclock
{
    // translate all calls to a specific region within the canvas

    public class RegionBlazorCanvas : ICanvas
    {
        public RegionBlazorCanvas(BlazorCanvas canvas, Point topleft, long regionwidth, long regionheight)
        {
			Canvas = canvas;
            TopLeft = topleft;
            Width = (int)regionwidth;
            Height = (int)regionheight;
        }

		public int Height { get; private set; }
		public int Width { get; private set; }

		public void Clear(RGBA color)
		{
			// clear only this region
			var points = new Point[]
            {
				new Point() {X = TopLeft.X, Y = TopLeft.Y},
				new Point() {X = TopLeft.X, Y = TopLeft.Y + Height},
				new Point() {X = TopLeft.X + Width, Y = TopLeft.Y + Height},
				new Point() {X = TopLeft.X + Width, Y = TopLeft.Y}
            };
			Canvas.Polygon(color, points, fill: true, border: false);
		}

		public void Ellipse(RGBA color, Point center, float width, float height, bool fill = true, bool border = true, float thickness = 5)
		{
			// shift the center by topleft x,y
			var rcenter = new Point() { X = center.X + TopLeft.X, Y = center.Y + TopLeft.Y };
			Canvas.Ellipse(color, rcenter, width, height, fill, border, thickness);
		}

		public void Polygon(RGBA color, Point[] points, bool fill = true, bool border = false, float thickness = 5)
		{
			// shift each point by topleft x,y
			var rpoints = new Point[points.Length];
			for (int i = 0; i < points.Length; i++)
			{
				rpoints[i].X = points[i].X + TopLeft.X;
				rpoints[i].Y = points[i].Y + TopLeft.Y;
			}
			Canvas.Polygon(color, rpoints, fill, border, thickness);
		}

		public void Text(RGBA color, Point topleft, string text, float fontsize = 16, string fontname = "Arial")
		{
			// shift the topleft by topleft x,y
			var rtopleft = new Point() { X = topleft.X + TopLeft.X, Y = topleft.Y + TopLeft.Y };
			Canvas.Text(color, rtopleft, text, fontsize, fontname);
		}

		public void SuspendLayout()
		{
		}

		public void ResumeLayout()
		{
		}

		#region private
		private BlazorCanvas Canvas;
        private Point TopLeft;
        #endregion
    }
}
