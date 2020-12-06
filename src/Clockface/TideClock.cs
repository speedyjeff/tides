using System;
using System.Threading;

using Utilities;
using External;
using System.Text;

// todo flare out the radar legs

namespace Clockface
{
	public class TideClock
	{
		public TideClock(ICanvas canvas, Predictions external)
		{
			if (canvas == null || external == null) throw new Exception("must pass in valid canvas and predictions");

			// init
			Canvas = canvas;
			Prediction = external;
			Ratio = Math.Min(Canvas.Width, Canvas.Height) / 524f; // 524 was the reference px

			// create timer
			FrameTimer = new Timer(FrameUpdate, null, 0, 50);
		}

		public event Action OnRendered;

		#region private
		private ICanvas Canvas;
		private Predictions Prediction;
		private Timer FrameTimer;
		private int FrameLock = 0;
		private float Angle;
		private float Ratio;

		private static RGBA CurrentTimeColor = new RGBA() { A = 255, R = 255, G = 242, B = 0 };
		private static RGBA HighlightHghColor = new RGBA() { A = 255, R = 220, G = 220, B = 220 };
		private static RGBA HighlightMidColor = new RGBA() { A = 255, R = 166, G = 166, B = 166 };
		private static RGBA HighlightLowColor = new RGBA() { A = 255, R = 133, G = 133, B = 133 };
		private static RGBA SpokeColor = new RGBA() { A = 255, R = 100, G = 100, B = 100 };

		private async void FrameUpdate(object state)
		{
			if (Canvas == null) throw new Exception("must have a valid canvas to draw too");

			// the timer is reentrant, so only allow one instance to run
			if (System.Threading.Interlocked.CompareExchange(ref FrameLock, 1, 0) != 0) return;

			// grab predictions
			var tides = await Prediction.CurrentTides();
			if (tides == null || tides.Count == 0) throw new Exception("failed to get tide information");
			var suns = await Prediction.CurrentSuns();
			if (suns == null || suns.Count == 0) throw new Exception("failed to get sunrise/sunset information");

			try
			{
				Canvas.SuspendLayout();

				// locations
				var dimension = Math.Min(Canvas.Width, Canvas.Height);
				var center = new Point((dimension / 2f), (dimension / 2f), z: 0f);
				var points = new Point[2];
				var fontsize = 16f * Ratio;
				var fontname = "Courier New";

				// clear
				Canvas.Clear(RGBA.Black);

				// outer ring
				var ringthickness = 1f * Ratio;
				var innerradius = dimension / 20f;
				var outerradius = ((1f * dimension) / 3f) + innerradius + ringthickness;
				Canvas.Ellipse(RGBA.White, center, width: 2 * outerradius, height: 2 * outerradius, fill: false, border: false, ringthickness);

				// clock numbers
				for (int i = 1; i <= 24; i++)
				{
					var angle = i * (360f / 24f);

					// draw line
					CalculateLineByAngle(center.X, center.Y, angle, outerradius, out points[0].X, out points[0].Y, out points[1].X, out points[1].Y);
					Canvas.Polygon(RGBA.White, points, fill: false, border: false, thickness: 1f * Ratio);

					// draw number
					CalculateLineByAngle(center.X, center.Y, angle, outerradius + (25f * Ratio), out points[0].X, out points[0].Y, out points[1].X, out points[1].Y);

					// adjust for the font size
					points[1].X -= (15f * Ratio);
					points[1].Y -= (8f * Ratio);

					Canvas.Text(RGBA.White, points[1], Clocknumber(i), fontsize, fontname);
				}

				var now = DateTime.Now;
				var later = now.AddHours(24);

				// calculate the local min/max
				var min = Single.MaxValue;
				var max = Single.MinValue;
				foreach (var tide in tides)
				{
					if (tide.Date >= now && tide.Date < later)
					{
						min = Math.Min(min, tide.Value);
						max = Math.Max(max, tide.Value);
					}
				}

				// adjust so that the min/max do not rest on the line
				min -= (0.1f * Ratio);
				max += (0.1f * Ratio);

				// flip the sign of min to bring to 0
				min *= -1;

				// draw the tides as a sun burst
				foreach (var tide in tides)
				{
					if (tide.Date >= now && tide.Date < later)
					{
						// calculate angles
						CalculateAngleByClockface(tide.Date.Hour, tide.Date.Minute, tide.Date.Second, out float hour, out float minute, out float seccond);

						// calculate the magnitude of the line
						var distance = ((tide.Value + min) / (min + max)) * (outerradius - innerradius);

						// calculate the line
						CalculateLineByAngle(center.X, center.Y, hour, innerradius, out points[1].X, out points[1].Y, out points[0].X, out points[0].Y);
						CalculateLineByAngle(points[0].X, points[0].Y, hour, distance, out points[0].X, out points[0].Y, out points[1].X, out points[1].Y);

						// draw the line
						var angledelta = Math.Min(Math.Min(Math.Abs(Angle - hour), Math.Abs((Angle + 360) - hour)), Math.Abs(Angle - (hour + 360)));
						var color = RGBA.Black;
						if (tide.Date.Subtract(now).TotalMinutes < 6) color = CurrentTimeColor;
						else if (angledelta < 10) color = HighlightHghColor;
						else if (angledelta < 20) color = HighlightMidColor;
						else if (angledelta < 30) color = HighlightLowColor;
						else color = SpokeColor;
						Canvas.Polygon(color, points, fill: false, border: false, thickness: 1f * Ratio);
					}
				}

				// insert sunrise/sunset information
				foreach (var sun in suns)
				{
					if (sun.Date.Date == now.Date.Date)
					{
						// calculate angles
						CalculateAngleByClockface(sun.Date.Hour, sun.Date.Minute, sun.Date.Second, out float hour, out float minute, out float seccond);
						CalculateLineByAngle(center.X, center.Y, hour, outerradius, out points[1].X, out points[1].Y, out points[0].X, out points[0].Y);
						var diameter = (20f * Ratio);

						if (sun.Type.Equals("sunrise", StringComparison.OrdinalIgnoreCase))
						{
							Canvas.Ellipse(RGBA.White, points[0], diameter, diameter, fill: true, border: false, thickness: 2f * Ratio);
						}
						else if (sun.Type.Equals("sunset", StringComparison.OrdinalIgnoreCase))
						{
							Canvas.Ellipse(RGBA.White, points[0], diameter, diameter, fill: false, border: true, thickness: 2f * Ratio);
						}
						else throw new Exception($"unknown sun type : {sun.Type}");
					}
				}
			}
			finally
            {
				Canvas.ResumeLayout();
            }

			// fire that the frame is done
			if (OnRendered != null) OnRendered();

			// set state back to not running
			System.Threading.Volatile.Write(ref FrameLock, 0);

			// increase the angle
			Angle = (Angle + 0.25f) % 360f;
		}

		private string Clocknumber(int i)
        {
			switch(i)
            {
				case 13:
				case 1: return "1";
				case 14:
				case 2: return "2";
				case 15:
				case 3: return "3";
				case 16:
				case 4: return "4";
				case 17:
				case 5: return "5";
				case 18:
				case 6: return "6";
				case 19:
				case 7: return "7";
				case 20:
				case 8: return "8";
				case 21:
				case 9: return "9";
				case 22:
				case 10: return "10";
				case 23:
				case 11: return "11";
				case 24:
				case 12: return "12";
				default: throw new Exception($"Unknown clock number {i}");
			}
        }

		private static bool CalculateLineByAngle(float x, float y, float angle, float distance, out float x1, out float y1, out float x2, out float y2)
		{
			while (angle < 0) angle += 360;
			while (angle > 360) angle -= 360;

			x1 = x;
			y1 = y;
			float a = (float)Math.Cos(angle * (Math.PI / 180d)) * distance;
			float o = (float)Math.Sin(angle * (Math.PI / 180d)) * distance;
			x2 = x1 + o;
			y2 = y1 - a;

			return true;
		}

		private static bool CalculateAngleByClockface(int hour, int minute, int second, out float hangle, out float mangle, out float sangle)
        {
			// init
			hangle = (float)hour;
			mangle = (float)minute;
			sangle = (float)second;

			// validate
			if (hour < 0 || hour >= 24
				|| minute < 0 || minute >= 60
				|| second < 0 || second >= 60) throw new Exception($"Invalid time : {hour} {minute} {second}");

			// include partial times
			mangle += ((float)second / 60f);
			hangle += ((float)minute / 60f);

			// caculate angles (as a percentage of the whole)
			hangle *= (360f / 24f);
			mangle *= (360f / 60f);
			sangle *= (360f / 60f);

			return true;
		}

		#endregion
	}
}
