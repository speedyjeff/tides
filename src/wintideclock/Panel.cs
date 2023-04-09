using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Utilities;
using External;
using Clockface;

namespace wintideclock
{
	public partial class Panel : Form
	{
		public Panel()
		{
			InitializeComponent();

			// set the base properties
			Text = "Tidal Clock";
			DoubleBuffered = true;
			Height = 800;
			Width = 1280;
			DoubleBuffer = BufferedGraphicsManager.Current.Allocate(CreateGraphics(), new Rectangle(0, 0, Width, Height));
			Canvas = DoubleBuffer.Graphics;
			Resize += ResizeWindow;
			Paint += PaintWindow;

			// create drawable surface
			TideClockImage = new WritableBitmap(width: 4096, height: 4096); // keep 1:1 ratio
			DigitalClockImage = new WritableBitmap(width: 4096, height: (int)(4096f*(1f/2f))); // keep 2:1 ratio
			DetailListImage = new WritableBitmap(width: 4096, height: (int)(4096f*(4f/5f))); // keep 5:4 ratio
			WeatherListImage = new WritableBitmap(width: 4096, height: (int)(4096f*(2f/5f))); // keep 5:2 ratio

			// https://www.tidesandcurrents.noaa.gov/tide_predictions.html
			External = new Predictions("La Push, WA", noahStationId: 9442396, lat: 47.9133f, lng: -124.6369f, subnet: "");

			// create clock face
			TideClock = new TideClock(TideClockImage.Canvas, External);
			TideClock.OnRendered += FrameRender;

			// create the digital clock
			DigitalClock = new DigitalClock(DigitalClockImage.Canvas, hasseconds: true, digitalclockface: true);
			DigitalClock.OnRendered += FrameRender;

			// create detail list
			DetailList = new DetailList(DetailListImage.Canvas, External);
			DetailList.OnRendered += FrameRender;

			// create weather list
			WeatherList = new WeatherList(WeatherListImage.Canvas, External);
			WeatherList.OnRendered += FrameRender;
		}

		#region private
		private TideClock TideClock;
		private WritableBitmap TideClockImage;
		private DigitalClock DigitalClock;
		private WritableBitmap DigitalClockImage;
		private DetailList DetailList;
		private WritableBitmap DetailListImage;
		private WeatherList WeatherList;
		private WritableBitmap WeatherListImage;
		private Graphics Canvas;
		private BufferedGraphics DoubleBuffer;
		private Predictions External;
		private bool IsShutdown;

		private delegate void PaintDelegate();
		private PaintDelegate RunPaintCallback;

		private void FrameRender()
		{
			if (IsShutdown) return;

			// draw
			DrawToDoubleBuffer();

			// refresh
			if (RunPaintCallback == null) RunPaintCallback = new PaintDelegate(() => { Refresh(); });

			// todo on shutdown stop painting

			// run on the UI thread
			try
			{
				Invoke(RunPaintCallback, null);
			}
			catch(InvalidOperationException)
            {
				IsShutdown = true;
			}
		}

		private void PaintWindow(object sender, PaintEventArgs e)
        {
			lock (this)
			{
				DoubleBuffer.Render(e.Graphics);
			}
        }

		private void DrawToDoubleBuffer()
		{
			lock (this)
			{
				int dimension = 0;
				int midpoint = Width - (int)(((float)Width / 1024f) * 500f);

				// draw the images on the screen
				// 524 x 524 | 500,400
				// 150,100   | 500,200

				var wratio = ((float)Width / (float)1024);
				var hratio = ((float)Height / (float)600);

                TideClockImage.Canvas.SuspendLayout();
                try
				{
					Canvas.DrawImage(TideClockImage.UnderlyingImage, new Rectangle()
					{
						X = 0,
						Y = 0,
						Width = (int)(524f * wratio),
						Height = (int)(524f * hratio)
					});
				}
				finally
				{
					TideClockImage.Canvas.ResumeLayout();
				}

                DigitalClockImage.Canvas.SuspendLayout();
                try
				{
					Canvas.DrawImage(DigitalClockImage.UnderlyingImage, new Rectangle()
					{
						X = (int)(((524f / 2f) - (200f/2f)) * wratio),
						Y = (int)((524f - 5f) * hratio),
						Width = (int)(200f * wratio),
						Height = (int)(50f * hratio)
					});
				}
				finally
				{
					DigitalClockImage.Canvas.ResumeLayout();
				}

                DetailListImage.Canvas.SuspendLayout();
                try
				{
					Canvas.DrawImage(DetailListImage.UnderlyingImage, new Rectangle()
					{
						X = (int)(524 * wratio),
						Y = 0,
						Width = (int)(500 * wratio),
						Height = (int)(400 * hratio)
					});
				}
				finally
				{
					DetailListImage.Canvas.ResumeLayout();
				}

                WeatherListImage.Canvas.SuspendLayout();
                try
				{
					dimension = Width - midpoint;
					Canvas.DrawImage(WeatherListImage.UnderlyingImage, new Rectangle()
					{
						X = (int)((524) * wratio),
						Y = (int)(400 * hratio),
						Width = (int)(500 * wratio),
						Height = (int)(200 * hratio)
					});
				}
				finally
				{
					WeatherListImage.Canvas.ResumeLayout();
				}
			}

		}

		private void ResizeWindow(object sender, EventArgs e)
        {
			lock (this)
			{
				if (DoubleBuffer != null)
				{
					Canvas.Dispose();
					DoubleBuffer.Dispose();
				}
				DoubleBuffer = BufferedGraphicsManager.Current.Allocate(CreateGraphics(), new Rectangle(0, 0, Width, Height));
				Canvas = DoubleBuffer.Graphics;
				Canvas.Clear(Color.FromArgb(0, 0, 0));
			}
		}
		#endregion
	}
}
