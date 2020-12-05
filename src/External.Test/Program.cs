using System;
using External;

namespace Tides.Test
{
	class Program
	{
		static void Main(string[] args)
		{
			var predictions = new Predictions("test", noahStationId: 9442396, lat: 47.9133f, lng: -124.6369f, lookaheadhours: 24);

			//Tides(predictions);
			//Suns(predictions);
			Weather(predictions);
		}

		private static void Tides(Predictions predictions)
		{ 
			var querycount = 0;

			predictions.OnQuery += () =>
			{
				querycount++;
			};

			// this should cause a query
			var tides = predictions.CurrentTides;
			foreach(var tide in tides)
			{
				Console.WriteLine($"{tide.Date} {tide.Value}");
			}

			if (tides.Count == 0 || querycount != 1) throw new Exception("Invalid query");

			// this round should not cause a query
			tides = predictions.CurrentTides;

			if (tides.Count == 0 || querycount != 1) throw new Exception("Invalid query");

			Console.WriteLine();

			var extremes = predictions.CurrentExtremes;

			foreach(var e in extremes)
			{
				Console.WriteLine($"{e.Date} {e.Value} {e.Type}");
			}

			if (extremes.Count == 0 || querycount != 2) throw new Exception($"Invalid query : {extremes.Count} {querycount}");

			// this round should not cause a query
			extremes = predictions.CurrentExtremes;

			if (extremes.Count == 0 || querycount != 2) throw new Exception("Invalid query");
		}

		private static void Suns(Predictions predictions)
		{
			var querycount = 0;

			predictions.OnQuery += () =>
			{
				querycount++;
			};

			// this should cause a query
			var suns = predictions.CurrentSuns;
			foreach (var s in suns)
			{
				Console.WriteLine($"{s.Date} {s.Type}");
			}

			if (suns.Count == 0 || querycount <= 0) throw new Exception("Invalid query");

			var previousquerycount = querycount;

			// this round should not cause a query
			suns = predictions.CurrentSuns;

			if (suns.Count == 0 || querycount != previousquerycount) throw new Exception("Invalid query");
		}

		private static void Weather(Predictions predictions)
		{
			var querycount = 0;

			predictions.OnQuery += () =>
			{
				querycount++;
			};

			// this should cause a query
			var weather = predictions.CurrentWeather;
			foreach (var w in weather)
			{
				Console.WriteLine($"{w.Date} {w.Type} {w.StrValue} {w.Value}");
			}

			if (weather.Count == 0 || querycount != 2) throw new Exception($"Invalid query : {weather.Count} {querycount}");

			var previousquerycount = querycount;

			// this round should not cause a query
			weather = predictions.CurrentWeather;

			if (weather.Count == 0 || querycount != previousquerycount) throw new Exception($"Invalid query : {weather.Count} {querycount}");
		}
	}
}
