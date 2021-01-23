using External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

// todo add an indicator when the weather is out of date

namespace Clockface
{
    public class WeatherList
    {
		public WeatherList(ICanvas canvas, Predictions external)
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
		private Predictions Prediction;
		private Timer FrameTimer;
		private int FrameLock = 0;
		private float Ratio;

		private static readonly string[] CompassNames = new string[]
			{"N", "NNE", "NE", "ENE",
			"E", "ESE", "SE", "SSE",
			"S", "SSW", "SW", "WSW",
			"W", "WNW", "NW", "NNW"};

		private static string DegreesToCompass(float deg)
        {
			var index = (int)(deg / 22.5f);
			if (index >= 0 && index < CompassNames.Length) return CompassNames[index];
			throw new Exception($"Unknown degree {deg}, {index}");
        }

		private async void FrameUpdate(object state)
		{
			if (Canvas == null) throw new Exception("must have a valid canvas to draw too");

			// the timer is reentrant, so only allow one instance to run
			if (System.Threading.Interlocked.CompareExchange(ref FrameLock, 1, 0) != 0) return;

			// grab predictions
			var weather = await Prediction.CurrentWeather();
			var weatherStation = await Prediction.CurrentWeatherStation();

			var rowheight = 26f * Ratio;
			var fontsize = 18f * Ratio;
			var fontname = "Courier New";
			var point = new Point() { X = 0f, Y = 0f };
			var avoiddups = new HashSet<string>();

			try
			{
				await Canvas.SuspendLayout();

				// clear
				Canvas.Clear(RGBA.Black);

				// current weather
				point.Y = (rowheight * 1);
				Canvas.Text(RGBA.White, point, "Local weather", fontsize, fontname);

				foreach (var w in weather.OrderByDescending(wr => wr.Date))
                {
					if (avoiddups.Contains(w.Type)) continue;

					switch (w.Type)
                    {
						case "temperature":
							point.X = 0f;
							point.Y = (rowheight * 2);
							Canvas.Text(RGBA.White, point, $"current: {w.Value}°", fontsize, fontname);
							break;
						case "temperaturetrend":
							point.X = (rowheight * 6);
							point.Y = (rowheight * 2);
							Canvas.Text(RGBA.White, point, $"[{w.StrValue.ToLower()}]", fontsize, fontname);
							break;
						case "temperaturelow":
							point.X = 0f;
							point.Y = (rowheight * 3);
							Canvas.Text(RGBA.White, point, $"low:     {w.Value}°", fontsize, fontname);
							break;
						case "temperaturehigh":
							point.X = 0f;
							point.Y = (rowheight * 4);
							Canvas.Text(RGBA.White, point, $"high:    {w.Value}°", fontsize, fontname);
							break;
						case "winddirection":
							point.X = 0f;
							point.Y = (rowheight * 5);
							Canvas.Text(RGBA.White, point, $"wind:    {w.StrValue}", fontsize, fontname);
							break;
						case "windspeed":
							point.X = (rowheight * 6);
							point.Y = (rowheight * 5);
							Canvas.Text(RGBA.White, point, $"{w.StrValue.ToLower()}", fontsize, fontname);
							break;
						case "shortforecast":
							point.X = 0f;
							point.Y = (rowheight * 6);
							var description = w.StrValue;
							if (description.Length > 22) description = description.Substring(0, 22) + "..";
							Canvas.Text(RGBA.White, point, $"{description}", fontsize, fontname);
							// debug
							if (true)
							{
								point.X = 0f;
								point.Y = (rowheight * 7);
								Canvas.Text(RGBA.White, point, $"{w.Date.ToLocalTime():yyyy/MM/dd hh:mm}", fontsize * 0.5f, fontname);
							}
							break;
                    }

					avoiddups.Add(w.Type);
				}

				// weather station
				if (weatherStation.Count > 0)
				{
					var padding = (rowheight * 10);
					avoiddups.Clear();
					point.Y = (rowheight * 1);
					point.X = padding;
					Canvas.Text(RGBA.White, point, $"Weather Station", fontsize, fontname);

					foreach (var w in weatherStation.OrderByDescending(s => s.Date))
					{
						if (avoiddups.Contains(w.Type)) continue;

						switch (w.Type)
						{
							case "outtemperature":
								point.X = padding;
								point.Y = (rowheight * 2);
								Canvas.Text(RGBA.White, point, $"current:  {w.Value:f0}°", fontsize, fontname);
								break;
							case "outhumidity":
								point.X = padding;
								point.Y = (rowheight * 3);
								Canvas.Text(RGBA.White, point, $"humidity: {w.Value:f0}%", fontsize, fontname);
								break;
							case "pressure":
								point.X = padding;
								point.Y = (rowheight * 4);
								Canvas.Text(RGBA.White, point, $"pressure: {w.Value:f2} inHg", fontsize, fontname);
								break;
							case "winddirection":
								point.X = padding;
								point.Y = (rowheight * 5);
								Canvas.Text(RGBA.White, point, $"wind:     {DegreesToCompass(w.Value)}", fontsize, fontname);
								break;
							case "windspeed":
								point.X = padding + (rowheight * 6);
								point.Y = (rowheight * 5);
								Canvas.Text(RGBA.White, point, $"{w.Value:f0} mph", fontsize, fontname);
								break;
							case "raintotal":
								point.X = padding;
								point.Y = (rowheight * 6);
								Canvas.Text(RGBA.White, point, $"rain:     {w.Value:f2} in", fontsize, fontname);
								// debug
								if (true)
								{
									point.X = padding;
									point.Y = (rowheight * 7);
									Canvas.Text(RGBA.White, point, $"{w.Date.ToLocalTime():yyyy/MM/dd hh:mm}", fontsize * 0.5f, fontname);
								}
								break;
						}

						avoiddups.Add(w.Type);
					}
				}
			}
			finally
			{
				await Canvas.ResumeLayout();
			}

			// fire that the frame is done
			if (OnRendered != null) OnRendered();

			// set state back to not running
			System.Threading.Volatile.Write(ref FrameLock, 0);
		}
		#endregion
	}
}
