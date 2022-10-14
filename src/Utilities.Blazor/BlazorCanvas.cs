using Blazor.Extensions.Canvas.Canvas2D;
using System.Runtime.CompilerServices;


namespace Utilities.Blazor
{
    public class BlazorCanvas : ICanvas
    {
        public BlazorCanvas(Canvas2DContext surface, long width, long height)
        {
            Surface = surface;
            Width = (int)width;
            Height = (int)height;
            Colors = new Dictionary<int, string>();
        }

        public int Height { get; private set; }
        public int Width { get; private set; }

        public async void Clear(RGBA color)
        {
            await Surface.SetFillStyleAsync(ColorCache(color));
            await Surface.FillRectAsync(x: 0, y: 0, Width, Height);
        }

        public async void Ellipse(RGBA color, Point center, float width, float height, bool fill = true, bool border = true, float thickness = 5)
        {
            // http://www.williammalone.com/briefs/how-to-draw-ellipse-html5-canvas/
            var hex = ColorCache(color);
            if (border)
            {
                await Surface.SetLineWidthAsync(thickness < 1f ? 1f : thickness);
                await Surface.SetStrokeStyleAsync(hex);
            }
            else
            {
                await Surface.SetStrokeStyleAsync(hex);
            }
            await Surface.BeginPathAsync();
            {
                await Surface.MoveToAsync(center.X, center.Y - (height / 2f));
                await Surface.BezierCurveToAsync(
                  center.X + (width * 0.67f), center.Y - (height / 2f), // C1
                  center.X + (width * 0.67f), center.Y + (height / 2f), // C2
                  center.X, center.Y + (height / 2f)); // A2

                await Surface.BezierCurveToAsync(
                  center.X - (width * 0.67f), center.Y + (height / 2f), // C3
                  center.X - (width * 0.67f), center.Y - (height / 2f), // C4
                  center.X, center.Y - (height / 2f)); // A1
                if (fill)
                {
                    await Surface.SetFillStyleAsync(hex);
                    await Surface.FillAsync();
                }
                else
                {
                    await Surface.StrokeAsync();
                }
            }
            await Surface.ClosePathAsync();
        }

        public async void Polygon(RGBA color, Point[] points, bool fill = true, bool border = false, float thickness = 5)
        {
            var hex = ColorCache(color);
            if (border)
            {
                await Surface.SetLineWidthAsync(thickness < 1f ? 1f : thickness);
                await Surface.SetStrokeStyleAsync(ColorCache(RGBA.Black));
            }
            else
            {
                await Surface.SetStrokeStyleAsync(hex);
            }
            await Surface.BeginPathAsync();
            {
                await Surface.MoveToAsync(points[0].X, points[0].Y);
                for (int i = 1; i < points.Length; i++) await Surface.LineToAsync(points[i].X, points[i].Y);
                await Surface.LineToAsync(points[0].X, points[0].Y);
                if (fill)
                {
                    await Surface.SetFillStyleAsync(hex);
                    await Surface.FillAsync();
                }
                else
                {
                    await Surface.StrokeAsync();
                }
            }
            await Surface.ClosePathAsync();
        }

        public async void Text(RGBA color, Point topleft, string text, float fontsize = 16, string fontname = "Arial")
        {
            await Surface.SetFillStyleAsync(ColorCache(color));
            await Surface.SetFontAsync(string.Format($"{fontsize}px {fontname}"));
            await Surface.FillTextAsync(text, topleft.X, topleft.Y + fontsize);
        }

        public Task SuspendLayout()
        {
            return Surface.BeginBatchAsync();
        }

        public Task ResumeLayout()
        {
            return Surface.EndBatchAsync();
        }

        #region private
        private Canvas2DContext Surface;
        private Dictionary<int, string> Colors;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ColorCache(RGBA color)
        {
            var hash = color.GetHashCode();
            if (!Colors.TryGetValue(hash, out string hex))
            {
                hex = string.Format($"rgba({color.R},{color.G},{color.B},{(float)color.A / 256f:f1})");
                Colors.Add(hash, hex);
            }
            return hex;
        }
        #endregion
    }
}
