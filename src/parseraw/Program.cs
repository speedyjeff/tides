using System;
using System.Collections.Generic;
using System.Xml;

namespace parseraw
{
	class Program
	{
		static int Main(string[] args)
		{
			if (args.Length < 4)
			{
				Console.WriteLine("./program XmlFilename StateAbreviation Latitude Longitude");
				Console.WriteLine("  NOAH station information can be pulled from https://api.tidesandcurrents.noaa.gov/mdapi/prod/webapi/stations/");
				return -1;
			}

			var xmlfilename = args[0];
			var stateabreviation = args[1];
			var latitude = Convert.ToSingle(args[2]);
			var longitude = Convert.ToSingle(args[3]);

			var results = XmlReaderWrapper.Parse(xmlfilename,
					new List<XmlElement>()
					{
						new XmlElement() { Name = "station", IsResultContainer = true },
						new XmlElement() { Name = "id" },
						new XmlElement() { Name = "name" },
						new XmlElement() { Name = "lat" },
						new XmlElement() { Name = "lng" },
						new XmlElement() { Name = "tidetype" },
						new XmlElement() { Name = "tidal" },
						new XmlElement() { Name = "state" },
						new XmlElement() { Name = "timezone" }
					});

			Console.WriteLine($"Count : {results.Count}");
			var smallestDistance = Single.MaxValue;
			foreach(var row in results)
			{
				// only consider stations in a specific state
				if (!row.TryGetValue("state", out string state)) continue;
				if (!string.Equals(state, stateabreviation, StringComparison.OrdinalIgnoreCase)) continue;

				// get distance from lat/lng
				var distance = Single.MaxValue;
				if (row.TryGetValue("lat", out string lat))
				{
					if (row.TryGetValue("lng", out string lng))
					{
						distance = DistanceFromLatLng(latitude, longitude, Convert.ToSingle(lat), Convert.ToSingle(lng));
					}
				}

				foreach(var kvp in row) Console.Write($"{kvp.Key}:{kvp.Value} ");
				Console.WriteLine($"distance:{distance}");

				smallestDistance = Math.Min(distance, smallestDistance);
			}

			Console.WriteLine($"{smallestDistance}");

			return 0;
		}

		#region private
		private const float EarthRadius = 6371; // km

		// distance between two points on the globe
		// https://stackoverflow.com/questions/27928/calculate-distance-between-two-latitude-longitude-points-haversine-formula
		private static float DistanceFromLatLng(float lat1, float lng1, float lat2, float lng2)
		{
			// convert to radians
			var rlat1 = lat1 * (Math.PI/180f);
			var rlng1 = lng1 * (Math.PI/180f);
			var rlat2 = lat2 * (Math.PI/180f);
			var rlng2 = lng2 * (Math.PI/180f);
			// get diff and convert to radians
  			var rdlat = (lat2-lat1) * (Math.PI/180f);
  			var rdlng = (lng2-lng2) * (Math.PI/180f);

			var a = (Math.Sin(rdlat/2f) * Math.Sin(rdlat/2f)) +
				(Math.Cos(rlat1) * Math.Cos(rlat2) * Math.Sin(rdlng/2f) * Math.Sin(rdlng/2f));
			var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1f-a)); 
			var d = EarthRadius * c; // distance in km
			return Convert.ToSingle(d);
		}
		#endregion
	}
}
