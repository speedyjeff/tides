using External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace Clockface
{
    public class DetailList
    {
		public DetailList(ICanvas canvas, IPredictions external)
		{
			if (canvas == null || external == null) throw new Exception("must pass in valid canvas and predictions");

			// init
			Canvas = canvas;
			Prediction = external;
			Ratio = Canvas.Width / 500f; // 500 was the reference px

			// create timer
			FrameTimer = new Timer(FrameUpdate, null, 0, 30 * 1000);
		}

		public event Action OnRendered;

		#region private
		private ICanvas Canvas;
		private IPredictions Prediction;
		private Timer FrameTimer;
		private int FrameLock = 0;
		private float Ratio;

		class ExtremeDetails
        {
			public DateTime Date;
			public DateTime LowDate;
			public float LowValue;
			public DateTime HighDate;
			public float HighValue;
        }

        private async void FrameUpdate(object state)
		{
			if (Canvas == null) throw new Exception("must have a valid canvas to draw too");

			// the timer is reentrant, so only allow one instance to run
			if (System.Threading.Interlocked.CompareExchange(ref FrameLock, 1, 0) != 0) return;

			// grab predictions
			var extremes = await Prediction.CurrentExtremes();
			var suns = await Prediction.CurrentSuns();

			// gather the tide extreme data
			var extremedetails = new List<ExtremeDetails>();
			ExtremeDetails current = null;
			foreach (var ex in extremes.OrderBy(e => e.Date))
			{
				// check if the current is complete
				if (current != null && (current.Date.Date != ex.Date.Date || current.HighDate != default(DateTime) ) )
                {
					// add it
					extremedetails.Add(current);
					current = null;
                }
				// check if we need to create a new one
				if (current == null)
                {
					current = new ExtremeDetails() { Date = ex.Date.Date };
                }

				// add the tide info
				if (ex.Type.Equals("h", StringComparison.OrdinalIgnoreCase))
				{
					if (current.HighDate != default(DateTime)) throw new Exception("Invalid high tide");
					current.HighDate = ex.Date;
					current.HighValue = ex.Value;
				}
				else if (ex.Type.Equals("l", StringComparison.OrdinalIgnoreCase))
                {
					if (current.LowDate != default(DateTime)) throw new Exception("Invalid low tide");
					current.LowDate = ex.Date;
					current.LowValue = ex.Value;
				}
				else throw new Exception($"unknown extreme type {ex.Type}");
            }
			if (current != null) extremedetails.Add(current);

			var now = DateTime.Now;
			var rowheight = 24f * Ratio;
			var margin = 20f * Ratio;
			var headerfontsize = 20f * Ratio;
			var headerfontname = "Courier New"; // "Eras Light ITC";
			var datafontsize = 18f * Ratio;
			var datafontname = "Courier New";
			var point = new Point() { X = 0f, Y = 0f };

            Canvas.SuspendLayout();
            try
			{
				// clear
				Canvas.Clear(RGBA.Black);

				Canvas.Text(RGBA.White, point, $"{now:MMM dd, yyyy} {Prediction.Location}", headerfontsize, headerfontname);

				//
				// tide extremes
				//
				point.Y = (rowheight*1.5f);
				point.X = (datafontsize * 4f);
				Canvas.Text(RGBA.White, point, "low", datafontsize, datafontname);
				point.X = (datafontsize * 17f);
				Canvas.Text(RGBA.White, point, "high", datafontsize, datafontname);
				var prvdate = default(DateTime);
				foreach (var ex in extremedetails)
				{
					if (ex.Date.Date >= now.Date && point.Y < (Canvas.Height-(rowheight*2)))
					{
						point.Y += rowheight;
						if (!prvdate.Date.Equals(ex.Date.Date))
						{
							point.Y += (rowheight / 4f);
							point.X = 0f;
							Canvas.Text(RGBA.White, point, $"{ex.Date:ddd}", datafontsize, datafontname);
						}
						if (ex.LowDate != default(DateTime))
                        {
							point.X = (datafontsize * 4f);
							Canvas.Text(RGBA.White, point, $"{ex.LowDate:hh:mm tt} {(ex.LowValue > 0f ? " " : "")}{ex.LowValue:f2}", datafontsize, datafontname);
						}
						if (ex.HighDate != default(DateTime))
						{
							point.X = (datafontsize * 16f);
							Canvas.Text(RGBA.White, point, $"{ex.HighDate:hh:mm tt} {(ex.HighValue < 10f ? " " : "")}{ex.HighValue:f2}", datafontsize, datafontname);
						}
						prvdate = ex.Date;
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
		}
        #endregion
    }
}
