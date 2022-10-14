using Acurite;
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
		public WeatherList(ICanvas canvas, IPredictions external)
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

		private enum PressureName { Stormy, Fair, Clear};
		private enum PressureChange { Failing, Rising, Steady};

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
				point.Y = (rowheight * 0);
				Canvas.Text(RGBA.White, point, "Local weather", fontsize, fontname);

				foreach (var w in weather.OrderByDescending(wr => wr.Date))
                {
					if (avoiddups.Contains(w.Type)) continue;

					switch (w.Type)
                    {
						case "temperature":
							point.X = 0f;
							point.Y = (rowheight * 1);
							Canvas.Text(RGBA.White, point, $"current: {w.Value}°", fontsize, fontname);
							break;
						case "temperaturetrend":
							point.X = (rowheight * 6);
							point.Y = (rowheight * 1);
							Canvas.Text(RGBA.White, point, $"[{w.StrValue.ToLower()}]", fontsize, fontname);
							break;
						case "temperaturelow":
							point.X = 0f;
							point.Y = (rowheight * 2);
							Canvas.Text(RGBA.White, point, $"low:     {w.Value}°", fontsize, fontname);
							break;
						case "temperaturehigh":
							point.X = 0f;
							point.Y = (rowheight * 3);
							Canvas.Text(RGBA.White, point, $"high:    {w.Value}°", fontsize, fontname);
							break;
						case "winddirection":
							point.X = 0f;
							point.Y = (rowheight * 4);
							Canvas.Text(RGBA.White, point, $"wind:    {w.StrValue}", fontsize, fontname);
							break;
						case "windspeed":
							point.X = (rowheight * 6);
							point.Y = (rowheight * 4);
							Canvas.Text(RGBA.White, point, $"{w.StrValue.ToLower()}", fontsize, fontname);
							break;
						case "shortforecast":
							point.X = 0f;
							point.Y = (rowheight * 5);
							var description = SplitString(w.StrValue, maxlength: 23);
							Canvas.Text(RGBA.White, point, $"{description.Item1}", fontsize, fontname);
							point.Y = (rowheight * 6);
							Canvas.Text(RGBA.White, point, $"{description.Item2}", fontsize, fontname);
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
					point.Y = (rowheight * 0);
					point.X = padding;
					Canvas.Text(RGBA.White, point, $"Weather Station", fontsize, fontname);

					// capture all the values
					var data = new AcuriteData();
					foreach (var w in weatherStation.OrderByDescending(s => s.Date))
					{
						if (data.utcDate == default(DateTime)) data.utcDate = w.Date;

						switch (w.Type)
						{
							case "outtemperature": if (!data.outTemperature.HasValue) data.outTemperature = w.Value; break;
							case "outhumidity": if (!data.outHumidity.HasValue) data.outHumidity = w.Value; break;
							case "pressure": if (!data.pressure.HasValue) data.pressure = w.Value; break;
							case "pressuretrend": if (data.pressureTrend == null) data.pressureTrend = w.Values; break;
							case "winddirection": if (!data.windDirection.HasValue) data.windDirection = w.Value; break;
							case "windspeed": if (!data.windSpeed.HasValue) data.windSpeed = w.Value; break;
							case "raintotal": if (!data.rainTotal.HasValue) data.rainTotal = w.Value; break;
							case "raintotaltrend": if (data.rainTotalTrend == null) data.rainTotalTrend = w.Values; break;
						}
					}

					// display the data
					if (data.outTemperature.HasValue)
					{
						point.X = padding;
						point.Y = (rowheight * 1);
						Canvas.Text(RGBA.White, point, $"current:  {data.outTemperature:f0}°", fontsize, fontname);
					}
					if (data.outHumidity.HasValue)
					{
						point.X = padding;
						point.Y = (rowheight * 2);
						Canvas.Text(RGBA.White, point, $"humidity: {data.outHumidity:f0}%", fontsize, fontname);
					}
					if (data.pressure.HasValue)
					{
						point.X = padding;
						point.Y = (rowheight * 3);
						Canvas.Text(RGBA.White, point, $"pressure: {data.pressure:f2} inHg", fontsize, fontname);
						if (data.pressureTrend != null)
						{
							point.X = padding;
							point.Y = (rowheight * 4);
							var trend = ReadPressureTrend(data.pressure.Value, data.pressureTrend);
							var change = trend.Item2 == PressureChange.Failing ? "↓" : (trend.Item2 == PressureChange.Rising ? "↑" : "-");
							Canvas.Text(RGBA.White, point, $"          {trend.Item3} {change}", fontsize, fontname);
						}
					}
					if (data.windDirection.HasValue)
					{
						point.X = padding;
						point.Y = (rowheight * 5);
						Canvas.Text(RGBA.White, point, $"wind:     {DegreesToCompass(data.windDirection.Value)}", fontsize, fontname);
					}
					if (data.windSpeed.HasValue)
					{
						point.X = padding + (rowheight * 6);
						point.Y = (rowheight * 5);
						Canvas.Text(RGBA.White, point, $"{data.windSpeed:f0} mph", fontsize, fontname);
					}
					if (data.rainTotal.HasValue)
					{
						point.X = padding;
						point.Y = (rowheight * 6);
						var rainTotal = data.rainTotal;
						if (data.rainTotalTrend != null && data.rainTotalTrend.Length >= 1) rainTotal -= data.rainTotalTrend[data.rainTotalTrend.Length - 1];
						Canvas.Text(RGBA.White, point, $"rain:     {rainTotal:f2} in", fontsize, fontname);

						// debug
						if (true)
						{
							point.X = padding;
							point.Y = (rowheight * 7);
							Canvas.Text(RGBA.White, point, $"{data.utcDate.ToLocalTime():yyyy/MM/dd hh:mm}", fontsize * 0.5f, fontname);
						}
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

		private static Tuple<string,string> SplitString(string text, int maxlength)
        {
			// strings can only be maxlength chars long
			if (string.IsNullOrWhiteSpace(text) || text.Length <= maxlength) return new Tuple<string, string>(text, "");

			// truncate the text and spread across two lines
			var index = text.LastIndexOf(' ', maxlength);

			var line1 = text.Substring(0, index);
			var line2 = text.Substring(index + 1);

			if (line2.Length > (maxlength + 2)) line2 = line2.Substring(0, maxlength - 2) + "..";

			return new Tuple<string, string>(line1, line2);
        }

		private static Tuple<PressureName, PressureChange, string> ReadPressureTrend(float pressure, float[] allpressures)
        {
			// https://www.wikihow.com/Set-a-Barometer

			var name = PressureName.Fair;
			var change = PressureChange.Steady;
			var forcast = "";

			// name
			if (pressure > 30.2f) name = PressureName.Clear;
			else if (pressure < 29.8f) name = PressureName.Stormy;
			else name = PressureName.Fair;

			// change
			// consider the first 2 elements in the array
			var delta = 0f;
			if (allpressures != null)
            {
				if (allpressures.Length == 1) delta = pressure - allpressures[0];
				else if (allpressures.Length >= 2)
				{
					delta = pressure - allpressures[1];
				}
				else delta = 0f;

				if (delta < 0) change = PressureChange.Failing;
				else if (delta > 0) change = PressureChange.Rising;
				else change = PressureChange.Steady;
            }

			// forcast
			if (change == PressureChange.Failing)
            {
				if (name == PressureName.Clear) forcast = "cloudy";
				else if (name == PressureName.Fair) forcast = "percepitation";
				else forcast = "stormy";
            }
			else if (change == PressureChange.Rising)
            {
				if (name == PressureName.Clear) forcast = "holding";
				else if (name == PressureName.Fair) forcast = "holding";
				else forcast = "clearing";
            }
			else
            {
				forcast = "steady";
			}

			return new Tuple<PressureName, PressureChange, string>(name, change, forcast);
        }
		#endregion
	}
}
