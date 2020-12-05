using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace Clockface
{
    public class DigitalClock
    {
		public DigitalClock(ICanvas canvas, bool digitalclockface = true)
		{
			// init
			Canvas = canvas;
			UseDigitalClockFace = digitalclockface;
			Ratio = Canvas.Width / 200f; // 150 was the reference px

			// create timer
			FrameTimer = new Timer(FrameUpdate, null, 0, 100);
		}

		public event Action OnRendered;

		#region private
		private ICanvas Canvas;
		private Timer FrameTimer;
		private int FrameLock = 0;
		private bool UseDigitalClockFace;
		private float Ratio;

		private void FrameUpdate(object state)
        {
			if (Canvas == null) throw new Exception("must have a valid canvas to draw too");

			// the timer is reentrant, so only allow one instance to run
			if (System.Threading.Interlocked.CompareExchange(ref FrameLock, 1, 0) != 0) return;

			try
            {
				Canvas.SuspendLayout();
				Canvas.Clear(RGBA.Black);

				var now = DateTime.Now;

				if (UseDigitalClockFace)
				{
					var width = Canvas.Width / 9f;
					var height = (Canvas.Height * 3f) / 4f;
					var center = new Point() { X = width, Y = Canvas.Height / 2f };
					var halfwidth = (width / 2f);
					var halfheight = (height / 2f);

					DrawCharacter(Canvas, center, halfwidth, halfheight, now.Hour > 12 ? now.Hour - 12 : now.Hour);
					center.X += (2 * width);
					DrawCharacter(Canvas, center, halfwidth, halfheight, ':');
					center.X += width;
					DrawCharacter(Canvas, center, halfwidth, halfheight, now.Minute);
					center.X += (2 * width);
					DrawCharacter(Canvas, center, halfwidth, halfheight, ':');
					center.X += width;
					DrawCharacter(Canvas, center, halfwidth, halfheight, now.Second);
				}
				else
                {
					var fontsize = 18f * Ratio;
					var fontname = "Courier New";
					var center = new Point() { X = 0f, Y = Canvas.Height / 2f };

					Canvas.Text(RGBA.White, center, $"{now:hh:mm:ss}", fontsize, fontname);
                }

				// todo pm indicator
				var quad = new Point[]
				{
					new Point() {X = 0, Y = 0},
					new Point() {X = Canvas.Width, Y = 0},
					new Point() {X = Canvas.Width, Y = Canvas.Height},
					new Point() {X = 0, Y = Canvas.Height}
				};
				Canvas.Polygon(RGBA.White, quad, fill: false, border: true, thickness: 2f * Ratio);
			}
			finally
            {
				Canvas.ResumeLayout();
            }

			if (OnRendered != null) OnRendered();

			// set state back to not running
			System.Threading.Volatile.Write(ref FrameLock, 0);
		}

		private void DrawCharacter(ICanvas canvas, Point center, float halfwidth, float halfheight, int num)
		{
			if (num < 0 || num > 60) throw new Exception("number must be between 0 and 60");

			DrawCharacter(canvas, center, halfwidth, halfheight, Convert.ToChar((num / 10) + (int)'0'));
			num = num % 10;

			center.X += (2 * halfwidth);
			DrawCharacter(canvas, center, halfwidth, halfheight, Convert.ToChar(num + (int)'0'));
		}

		private void DrawCharacter(ICanvas canvas, Point center, float halfwidth, float halfheight, char chr)
        {
			var quad = new Point[4];
			var elementdepth = (halfwidth / 3f);
			var thickness = 1f * Ratio;

			// 0 ---- 1
			// -      -
			// -      -
			// 3 ---- 2
			if (chr == ':')
			{
				// narrow
				quad[0].X = quad[3].X = center.X - (elementdepth/2f);
				quad[1].X = quad[2].X = center.X + (elementdepth/2f);

				// top
				quad[0].Y = quad[1].Y = center.Y - (halfheight / 4f);
				quad[2].Y = quad[3].Y = center.Y - (halfheight / 4f) + elementdepth;

				canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);

				// bottom
				quad[0].Y = quad[1].Y = center.Y + (halfheight / 4f) - elementdepth;
				quad[2].Y = quad[3].Y = center.Y + (halfheight / 4f);

				canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);
			}
			else if (Char.IsDigit(chr))
			{
				// right top
				if (chr != '5' && chr != '6')
				{
					quad[0].X = quad[3].X = center.X + (halfwidth-elementdepth);
					quad[1].X = quad[2].X = center.X + (halfwidth);

					quad[0].Y = center.Y - (halfheight - elementdepth);
					quad[1].Y = center.Y - halfheight;
					quad[2].Y = center.Y;
					quad[3].Y = center.Y - elementdepth;

					canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);
				}

				// right bottom
				if (chr != '2')
				{
					quad[0].X = quad[3].X = center.X + (halfwidth - elementdepth);
					quad[1].X = quad[2].X = center.X + (halfwidth);

					quad[0].Y = center.Y + elementdepth;
					quad[1].Y = center.Y;
					quad[2].Y = center.Y + halfheight;
					quad[3].Y = center.Y + (halfheight - elementdepth);

					canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);
				}

				// left top
				if (chr != '1' && chr != '2' && chr != '3' && chr != '7')
				{
					quad[0].X = quad[3].X = center.X - (halfwidth);
					quad[1].X = quad[2].X = center.X - (halfwidth - elementdepth);


					quad[0].Y = center.Y - halfheight;
					quad[1].Y = center.Y - (halfheight - elementdepth);
					quad[2].Y = center.Y - elementdepth;
					quad[3].Y = center.Y;

					canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);
				}

				// left bottom
				if (chr != '1' && chr != '3' && chr != '4' && chr != '5' && chr != '7' && chr != '9')
				{
					quad[0].X = quad[3].X = center.X - (halfwidth);
					quad[1].X = quad[2].X = center.X - (halfwidth - elementdepth);

					quad[0].Y = center.Y;
					quad[1].Y = center.Y + elementdepth;
					quad[2].Y = center.Y + (halfheight - elementdepth);
					quad[3].Y = center.Y + halfheight;

					canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);
				}

				// top
				if (chr != '1' && chr != '4')
				{
					quad[0].X = center.X - halfwidth;
					quad[1].X = center.X + halfwidth;
					quad[2].X = center.X + (halfwidth - elementdepth);
					quad[3].X = center.X - (halfwidth - elementdepth);

					quad[0].Y = quad[1].Y = center.Y - (halfheight);
					quad[2].Y = quad[3].Y = center.Y - (halfheight-elementdepth);

					canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);
				}

				// middle
				if (chr != '1' && chr != '7' && chr != '0')
				{
					var six = new Point[6];

					six[0].X = center.X + halfwidth;
					six[1].X = center.X + (halfwidth - elementdepth);
					six[2].X = center.X - (halfwidth - elementdepth);
					six[3].X = center.X - halfwidth;
					six[4].X = center.X - (halfwidth - elementdepth);
					six[5].X = center.X + (halfwidth - elementdepth);

					six[0].Y = center.Y;
					six[1].Y = center.Y - elementdepth;
					six[2].Y = center.Y - elementdepth;
					six[3].Y = center.Y;
					six[4].Y = center.Y + elementdepth;
					six[5].Y = center.Y + elementdepth;

					canvas.Polygon(RGBA.White, six, fill: true, border: true, thickness);
				}

				// bottom
				if (chr != '1' && chr != '4' && chr != '7' && chr != '9')
				{
					quad[0].X = center.X + (halfwidth - elementdepth);
					quad[1].X = center.X - (halfwidth - elementdepth);
					quad[2].X = center.X - halfwidth;
					quad[3].X = center.X + halfwidth;

					quad[2].Y = quad[3].Y = center.Y + (halfheight);
					quad[1].Y = quad[0].Y = center.Y + (halfheight - elementdepth);

					canvas.Polygon(RGBA.White, quad, fill: true, border: true, thickness);
				}
			}
			else throw new Exception("invalid character to draw");
		}
        #endregion
    }
}
