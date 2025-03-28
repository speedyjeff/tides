using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace wintideclock
{
	public class GraphicsCanvas : ICanvas
	{
		public GraphicsCanvas(BufferedGraphicsContext context, Graphics g, int width, int height)
		{
			// init
			Context = context;
			Graphics = g;
			Width = width;
			Height = height;
			SolidBrushCache = new Dictionary<int, SolidBrush>();
			PenCache = new Dictionary<long, Pen>();
			FontCache = new Dictionary<string, Dictionary<float, Font>>();
			LayoutLock = new ReaderWriterLockSlim();
			// most common types, so use one as a cache (to avoid the allocation)
			TriPoints = new PointF[3];
			DuoPoints = new PointF[2];

			// get graphics ready
			if (Context != null) RawResize(g, height, width);
		}

		public int Height { get; private set; }
		public int Width { get; private set; }

		public void Clear(RGBA color)
		{
			Graphics.FillRectangle(GetCachedSolidBrush(color), 0, 0, Width, Height);
		}

		public void Ellipse(RGBA color, Utilities.Point center, float width, float height, bool fill, bool border, float thickness)
		{
			if (fill)
			{
				Graphics.FillEllipse(GetCachedSolidBrush(color), center.X - (width/2f), center.Y - (height/2f), width, height);
				if (border) Graphics.DrawEllipse(GetCachedPen(RGBA.Black, thickness), center.X - (width / 2f), center.Y - (height / 2f), width, height);
			}
			else
			{
				Graphics.DrawEllipse(GetCachedPen(color, thickness), center.X - (width / 2f), center.Y - (height / 2f), width, height);
			}
		}

		public void Polygon(RGBA color, Utilities.Point[] points, bool fill = false, bool border = false, float thickness = 5f)
		{
			if (points == null || points.Length <= 1) throw new Exception("Must provide a valid number of points");

			// convert points into PointF
			PointF[] edges = null;
			if (points.Length == 2)
			{
				DuoPoints[0].X = points[0].X; DuoPoints[0].Y = points[0].Y;
				DuoPoints[1].X = points[1].X; DuoPoints[1].Y = points[1].Y;

				edges = DuoPoints;
			}
			else if (points.Length == 3)
			{
				TriPoints[0].X = points[0].X; TriPoints[0].Y = points[0].Y;
				TriPoints[1].X = points[1].X; TriPoints[1].Y = points[1].Y;
				TriPoints[2].X = points[2].X; TriPoints[2].Y = points[2].Y;

				edges = TriPoints;
			}
			else
			{
				edges = new System.Drawing.PointF[points.Length];
				for (int i = 0; i < points.Length; i++)
				{
					edges[i] = new System.Drawing.PointF(points[i].X, points[i].Y);
				}
			}

			if (fill)
			{
				Graphics.FillPolygon(GetCachedSolidBrush(color), edges);
				if (border) Graphics.DrawPolygon(GetCachedPen(RGBA.Black, thickness), edges);
			}
			else
			{
				Graphics.DrawPolygon(GetCachedPen(color, thickness), edges);
			}
		}

		public void Text(RGBA color, Utilities.Point topleft, string text, float fontsize = 16, string fontname = "Arial")
		{
			Graphics.DrawString(text, GetCachedFont(fontname, fontsize), GetCachedSolidBrush(color), topleft.X, topleft.Y);
		}

		public void SuspendLayout()
        {
			LayoutLock.EnterWriteLock();
		}

		public void ResumeLayout()
        {
			LayoutLock.ExitWriteLock();
		}

		#region private
		private Graphics Graphics;
		private BufferedGraphics Surface;
		private BufferedGraphicsContext Context;
		private PointF[] TriPoints;
		private PointF[] DuoPoints;
		private ReaderWriterLockSlim LayoutLock;

		// caches
		private Dictionary<int, SolidBrush> SolidBrushCache;
		private Dictionary<long, Pen> PenCache;
		private Dictionary<string, Dictionary<float, Font>> FontCache;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private System.Drawing.Bitmap LoadImage(string path, Stream stream = null)
		{
			System.Drawing.Image img = null;
			if (stream != null) img = System.Drawing.Image.FromStream(stream);
			else img = System.Drawing.Image.FromFile(path);
			var bitmap = new Bitmap(img);
			bitmap.MakeTransparent(bitmap.GetPixel(0, 0));
			return bitmap;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private SolidBrush GetCachedSolidBrush(RGBA color)
		{
			var key = color.GetHashCode();
			SolidBrush brush = null;
			if (!SolidBrushCache.TryGetValue(key, out brush))
			{
				brush = new SolidBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
				SolidBrushCache.Add(key, brush);
			}
			return brush;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Pen GetCachedPen(RGBA color, float thickness)
		{
			var key = (long)color.GetHashCode() | ((long)thickness << 32);
			Pen pen = null;
			if (!PenCache.TryGetValue(key, out pen))
			{
				pen = new Pen(Color.FromArgb(color.A, color.R, color.G, color.B), thickness);
				PenCache.Add(key, pen);
			}
			return pen;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Font GetCachedFont(string name, float size)
		{
			// apply an overall reduction to font size
			size *= 0.25f;

			Dictionary<float, Font> fonts = null;
			if (!FontCache.TryGetValue(name, out fonts))
			{
				fonts = new Dictionary<float, Font>();
				FontCache.Add(name, fonts);
			}

			var key = (float)Math.Round(size, 2);
			Font font = null;
			if (!fonts.TryGetValue(key, out font))
			{
				font = new Font(name, key);
				fonts.Add(key, font);
			}
			return font;
		}

		internal void Release()
		{
			if (Graphics != null) Graphics.Dispose();
			if (Surface != null) Surface.Dispose();
			if (Context != null) Context.Dispose();
			Graphics = null;
			Surface = null;
			Context = null;
		}

		internal void RawResize(Graphics g, int width, int height)
		{
			if (Context == null) return;

			// initialize the double buffer
			Width = width;
			Height = height;
			Context.MaximumBuffer = new Size(width + 1, height + 1);

			// cleanup
			if (Surface != null) Surface.Dispose();

			// recreate
			Surface = Context.Allocate(g, new Rectangle(0, 0, width, height));
			Graphics = Surface.Graphics;
		}
		#endregion
	}
}