using System;
using System.Threading.Tasks;
using Acurite;
using External;

namespace Tides.Test
{
	class Program
	{
		static void Main(string[] args)
		{
			var predictions = new Predictions("test", noahStationId: 9442396, lat: 47.9133f, lng: -124.6369f, subnet: "");

			var task = Task.Run(async () =>
		    {
				int sum = 0;
			    sum += await Tides(predictions);
			    sum += await Suns(predictions);
			    sum += await Weather(predictions);
				sum += await WeatherStation(predictions);

				AcuriteDataTest();

			   Console.WriteLine($"sum = {sum}");
		    });
			task.Wait();
		}

		private async static Task<int> Tides(Predictions predictions)
		{ 
			var querycount = 0;

			predictions.OnQuery += () =>
			{
				querycount++;
			};

			// this should cause a query
			var tides = await predictions.CurrentTides();
			foreach(var tide in tides)
			{
				Console.WriteLine($"{tide.Date} {tide.Value}");
			}

			if (tides.Count == 0 || querycount != 1) throw new Exception("Invalid query");

			// this round should not cause a query
			tides = await predictions.CurrentTides();

			if (tides.Count == 0 || querycount != 1) throw new Exception("Invalid query");

			Console.WriteLine();

			var extremes = await predictions.CurrentExtremes();

			foreach(var e in extremes)
			{
				Console.WriteLine($"{e.Date} {e.Value} {e.Type}");
			}

			if (extremes.Count == 0 || querycount != 2) throw new Exception($"Invalid query : {extremes.Count} {querycount}");

			// this round should not cause a query
			extremes = await predictions.CurrentExtremes();

			if (extremes.Count == 0 || querycount != 2) throw new Exception("Invalid query");

			return 0;
		}

		private async static Task<int> Suns(Predictions predictions)
		{
			var querycount = 0;

			predictions.OnQuery += () =>
			{
				querycount++;
			};

			// this should cause a query
			var suns = await predictions.CurrentSuns();
			foreach (var s in suns)
			{
				Console.WriteLine($"{s.Date} {s.Type}");
			}

			if (suns.Count == 0 || querycount <= 0) throw new Exception("Invalid query");

			var previousquerycount = querycount;

			// this round should not cause a query
			suns = await predictions.CurrentSuns();

			if (suns.Count == 0 || querycount != previousquerycount) throw new Exception("Invalid query");

			return 0;
		}

		private async static Task<int> Weather(Predictions predictions)
		{
			var querycount = 0;

			predictions.OnQuery += () =>
			{
				querycount++;
			};

			// this should cause a query
			var weather = await predictions.CurrentWeather();
			foreach (var w in weather)
			{
				Console.WriteLine($"{w.Date} {w.Type} {w.StrValue} {w.Value}");
			}

			if (weather.Count == 0 || querycount != 2) throw new Exception($"Invalid query : {weather.Count} {querycount}");

			var previousquerycount = querycount;

			// this round should not cause a query
			weather = await predictions.CurrentWeather();

			if (weather.Count == 0 || querycount != previousquerycount) throw new Exception($"Invalid query : {weather.Count} {querycount}");

			return 0;
		}

		private async static Task<int> WeatherStation(Predictions predictions)
		{
			var querycount = 0;

			predictions.OnQuery += () =>
			{
				querycount++;
			};

			// this should cause a query
			var weather = await predictions.CurrentWeatherStation();
			foreach (var w in weather)
			{
				Console.WriteLine($"{w.Date} {w.Type} {w.StrValue} {w.Value}");
			}

			if (weather.Count == 0 || querycount != 1) throw new Exception($"Invalid query : {weather.Count} {querycount}");

			var previousquerycount = querycount;

			// this round should not cause a query
			weather = await predictions.CurrentWeatherStation();

			if (weather.Count == 0 || querycount != previousquerycount) throw new Exception($"Invalid query : {weather.Count} {querycount}");

			return 0;
		}

		private static void AcuriteDataTest()
        {
			var a = new AcuriteData();
			a.utcDate = new DateTime(2020, 1, 1);
			a.pressure = 30;
			var b = new AcuriteData();
			b.utcDate = new DateTime(2021, 1, 1);
			b.pressure = 31;

			var c = a + b;

			Console.WriteLine($"{c.pressure} {(c.pressureTrend == null ? "null" : c.pressureTrend[0]+"")}");

			var d = new AcuriteData();
			d.utcDate = new DateTime(2021, 2, 1);
			d.pressure = 32;

			var e = c + d;

			Console.WriteLine($"{e.pressure} {(e.pressureTrend == null ? "null" : e.pressureTrend[0] + "")}");
		}
	}
}
