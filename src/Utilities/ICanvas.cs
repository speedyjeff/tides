using System;

namespace Utilities
{
	public interface ICanvas
	{
		int Height { get; }
		int Width { get; }
		void Clear(RGBA color);
		void Ellipse(RGBA color, Point center, float width, float height, bool fill = true, bool border = true, float thickness = 5f);
		void Polygon(RGBA color, Point[] points, bool fill = true, bool border = false, float thickness = 5f);
		void Text(RGBA color, Point topleft, string text, float fontsize = 16, string fontname = "Arial");
		void SuspendLayout();
		void ResumeLayout();
	}
}
