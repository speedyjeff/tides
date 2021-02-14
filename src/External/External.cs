using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Acurite;

// todo subnet notation?

namespace External
{
	public struct Data
	{
		// date
		public DateTime Date;
		// variant style returning data
		public float Value;
		public string StrValue;
		public float[] Values;
		// type of the Data
		public string Type;
	}

	public class Predictions
	{
		public Predictions(string location, int noahStationId, float lat, float lng, string subnet)
		{
			// storage for all data
			AllData = new Dictionary<PredictionType, PredictionDetails>();

			// init
			Location = location;
			NoahStationId = noahStationId;
			Latitude = lat;
			Longitude = lng;
			WeatherStationAddress = Subnet = null;
			WeatherStationSearch = 0;

			// check that the subnet is in the following format "#.#.#.";
			var ipparts = subnet.Split('.');
			if (ipparts.Length >= 3)
            {
				if (byte.TryParse(ipparts[0], out byte n1))
                {
					if (byte.TryParse(ipparts[1], out byte n2))
                    {
						if (byte.TryParse(ipparts[2], out byte n3))
                        {
							Subnet = $"{n1}.{n2}.{n3}.";
                        }
                    }
                }
            }
		}

		public string Location { get; private set; }

		public async Task<List<Data>> CurrentTides()
		{
			return await LatestPredictions(
				PredictionType.Tides,
				deleteAfterMinutes: 24 * 60, 
				lookaheadMinutes: 3 * 24 * 60,
				cacheInvalidationMinutes: 2 * 60,
				pullMoreMinutes: 24 * 60, 
				retrieveAdditional: async (start, end) =>
			{
				var json = await GetWebJson(string.Format(PredictionUrl, start, end, NoahStationId, "mllw"));
				return new List<string>() { json };
			});
		}

		public async Task<List<Data>> CurrentExtremes()
		{
			return await LatestPredictions(
				PredictionType.Extremes,
				deleteAfterMinutes: 24 * 60, 
				lookaheadMinutes: 14 * 24 * 60,
				cacheInvalidationMinutes: 2 * 60,
				pullMoreMinutes: 24 * 60, 
				retrieveAdditional: async (start, end) =>
			{
				var json = await GetWebJson(string.Format(PredictionHiloUrl, start, end, NoahStationId, "mllw"));
				return new List<string>() { json };
			});
		}

		public async Task<List<Data>> CurrentSuns()
		{
			return await LatestPredictions(
				PredictionType.Suns,
				deleteAfterMinutes: 24 * 60, 
				lookaheadMinutes: 7 * 24 * 60,
				cacheInvalidationMinutes: 2 * 60,
				pullMoreMinutes: 24 * 60, 
				retrieveAdditional: async (start, end) =>
			{
				// query each day seperately
				var json = new List<string>();
				var date = start;
				while (date.Date <= end.Date)
				{
					json.Add(await GetWebJson(string.Format(SunriseSunsetUrl, Latitude, Longitude, date)));
					date = date.AddDays(1);
				}

				return json;
			});
		}

		public async Task<List<Data>> CurrentWeather()
		{
			return await LatestPredictions(
				PredictionType.Weather,
				deleteAfterMinutes: 60, 
				lookaheadMinutes: 0,
				cacheInvalidationMinutes: 15,
				pullMoreMinutes: 0, 
				retrieveAdditional: async (start, end) =>
			{
				// get the grid where to query for weather
				var grid = ParseJson(await GetWebJson(string.Format(WeatherInfoUrl, Latitude, Longitude)), PredictionType.WeatherInfo);
				if (grid == null || grid.Count != 1) throw new Exception("failed to retrieve a grid for this location");
				// this call returns the url to call to get the data
				return new List<string>()
				{
						await GetWebJson(grid[0].StrValue)
				};
			});
		}

		public async Task<List<Data>> CurrentWeatherStation()
        {
			// need to first discover the service
			if (string.IsNullOrWhiteSpace(Subnet)) return new List<Data>();

			if (WeatherStationSearch > 0)
            {
				// a search is in progress;
				WeatherStationSearch--;
				return new List<Data>();
            }

			// discover the service
			if (string.IsNullOrWhiteSpace(WeatherStationAddress))
            {
				// indicate that we are searching
				WeatherStationSearch = 10;

				Console.WriteLine($"{DateTime.UtcNow:o}: scaning the weather station service");
				// broadcast a request and record who responded
				for (int i = 1; i <= 64; i++)
				{
					var ip = Subnet + i;
					var url = string.Format(WeatherStationUrl, ip);
					if (await QuickCheck(url, millisecondsDelay: 600))
					{
						WeatherStationAddress = ip;
						break;
					}
				}

				// clear
				WeatherStationSearch = 0;
			}

			// wait until we get a valid address
			if (string.IsNullOrWhiteSpace(WeatherStationAddress)) return new List<Data>();

			// return results
			return await LatestPredictions(
				PredictionType.Station,
				deleteAfterMinutes: 1, 
				lookaheadMinutes: 0,
				cacheInvalidationMinutes: 1,
				pullMoreMinutes: -1, 
				retrieveAdditional: async (start, end) =>
			{
				try
				{
					var json = await GetWebJson(string.Format(WeatherStationUrl, WeatherStationAddress));
					return new List<string>() { json };
				}
				catch(Exception)
                {
					// need to rescan for this service
					Console.WriteLine($"{DateTime.Now:o}: resetting weather station service address");
					WeatherStationAddress = null;
					return new List<string>();
                }
			});
		}

		public event Action OnQuery;

		#region private
		private Dictionary<PredictionType, PredictionDetails> AllData;
		private int NoahStationId;
		private float Latitude;
		private float Longitude;
		private string Subnet;
		private string WeatherStationAddress;
		private int WeatherStationSearch;

		private enum PredictionType { Tides, Extremes, Suns, WeatherInfo, Weather, Station };
		private class PredictionDetails
        {
			public Dictionary<string, Data> Data;
			public List<Data> Cache;
			public Stopwatch Timer;

			public PredictionDetails()
            {
				Data = new Dictionary<string, Data>();
				Cache = new List<Data>();
				Timer = new Stopwatch();
			}
        }

		// https://www.tidesandcurrents.noaa.gov/tide_predictions.html
		// https://api.tidesandcurrents.noaa.gov/api/prod/
		private const string PredictionUrl = "https://api.tidesandcurrents.noaa.gov/api/prod/datagetter?begin_date={0:yyyyMMdd}&end_date={1:yyyyMMdd}&station={2}&time_zone=gmt&units=english&format=json&datum={3}&product=predictions";
		private const string PredictionHiloUrl = "https://api.tidesandcurrents.noaa.gov/api/prod/datagetter?begin_date={0:yyyyMMdd}&end_date={1:yyyyMMdd}&station={2}&time_zone=gmt&units=english&format=json&datum={3}&product=predictions&interval=hilo";

		// https://sunrise-sunset.org/api
		private const string SunriseSunsetUrl = "https://api.sunrise-sunset.org/json?lat={0}&lng={1}&date={2:yyyy-MM-dd}&formatted=0";

		// https://www.weather.gov/documentation/services-web-api
		private const string WeatherInfoUrl = "https://api.weather.gov/points/{0},{1}"; // lat,lng

		// locally built Acurite Station hub
		private const string WeatherStationUrl = "http://{0}:11000/weather";

		private static readonly byte[] s_T = System.Text.Encoding.UTF8.GetBytes("t");
		private static readonly byte[] s_V = System.Text.Encoding.UTF8.GetBytes("v");
		private static readonly byte[] s_Type = System.Text.Encoding.UTF8.GetBytes("type");
		private static readonly byte[] s_Sunrise = System.Text.Encoding.UTF8.GetBytes("sunrise");
		private static readonly byte[] s_Sunset = System.Text.Encoding.UTF8.GetBytes("sunset");
		private static readonly byte[] s_ForecastHourly = System.Text.Encoding.UTF8.GetBytes("forecastHourly"); 
		private static readonly byte[] s_EndTime = System.Text.Encoding.UTF8.GetBytes("endTime");
		private static readonly byte[] s_Temperature = System.Text.Encoding.UTF8.GetBytes("temperature");
		private static readonly byte[] s_WindSpeed = System.Text.Encoding.UTF8.GetBytes("windSpeed");
		private static readonly byte[] s_WindDirection = System.Text.Encoding.UTF8.GetBytes("windDirection");
		private static readonly byte[] s_ShortForecast = System.Text.Encoding.UTF8.GetBytes("shortForecast");
		private static readonly byte[] s_TemperatureTrend = System.Text.Encoding.UTF8.GetBytes("temperatureTrend");
		private static readonly byte[] s_UtcDate = System.Text.Encoding.UTF8.GetBytes("utcDate");
		private static readonly byte[] s_Signal = System.Text.Encoding.UTF8.GetBytes("signal");
		private static readonly byte[] s_Lowbattery = System.Text.Encoding.UTF8.GetBytes("lowBattery");
		private static readonly byte[] s_RaintTotal = System.Text.Encoding.UTF8.GetBytes("rainTotal");
		private static readonly byte[] s_OutTemperature = System.Text.Encoding.UTF8.GetBytes("outTemperature");
		private static readonly byte[] s_InTemperature = System.Text.Encoding.UTF8.GetBytes("inTemperature");
		private static readonly byte[] s_OutHumidity = System.Text.Encoding.UTF8.GetBytes("outHumidity");
		private static readonly byte[] s_Pressure = System.Text.Encoding.UTF8.GetBytes("pressure");
		private static readonly byte[] s_PressureTrend = System.Text.Encoding.UTF8.GetBytes("pressureTrend");
		private static readonly byte[] s_RainTotalTrend = System.Text.Encoding.UTF8.GetBytes("rainTotalTrend");

		private List<Data> ParseJson(string json, PredictionType type)
		{
			if (string.IsNullOrWhiteSpace(json)) throw new Exception("Failed to get json");

			// parse json
			var bytes = System.Text.Encoding.UTF8.GetBytes(json);
			var reader = new Utf8JsonReader(bytes, true, default(JsonReaderState));

			var sunriseseen = false;
			var sunsetseen = false;
			var tseen = false;
			var vseen = false;
			var typeseen = false;
			var forecasthourlyseen = false;
			var endtimeseen = false;
			var temperatureseen = false;
			var windspeedseen = false;
			var winddirectionseen = false;
			var shortforecastseen = false;
			var temperaturetrendseen = false;
			var utcdateseen = false;
			var signalseen = false;
			var lowbatteryseen = false;
			var raintotalseen = false;
			var outtemperatureseen = false;
			var outhumidityseen = false;
			var pressureseen = false;
			var intemperatureseen = false;
			var collectweather = false;
			var pressuretrendseen = false;
			var raintotaltrendseen = false;
			var results = new List<Data>();
			var p = default(Data);
			var min = default(Data);
			var max = default(Data);
			var pressuretrend = new List<float>();
			var raintotaltrend = new List<float>();
			var count = 0;
			while(reader.Read())
			{
				//
				// noah data
				//
				if (type == PredictionType.Extremes || type == PredictionType.Tides)
				{
					tseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_T));
					if (tseen && reader.TokenType == JsonTokenType.String)
					{
						// add it if there is already a prediction in flight
						if (count > 0) results.Add(p);
						count++;

						var t = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);

						// incoming datetimes are GMT/UTC
						var date = DateTime.Parse(t, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal);
						p = new Data() { Date = date, Type = "" };

						tseen = false;
					}

					vseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_V));
					if (vseen && reader.TokenType == JsonTokenType.String)
					{
						p.Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan));
						vseen = false;
					}

					typeseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_Type));
					if (typeseen && reader.TokenType == JsonTokenType.String)
					{
						p.Type = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
						typeseen = false;
					}
				}

				//
				// sunrise/sunset data
				//
				if (type == PredictionType.Suns)
				{
					sunriseseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_Sunrise));
					if (sunriseseen && reader.TokenType == JsonTokenType.String)
					{
						count++;
						var t = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);

						// incoming datetimes are GMT/UTC
						var date = DateTime.Parse(t, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal);
						results.Add(new Data() { Date = date, Type = "sunrise" });

						sunriseseen = false;
					}
					sunsetseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_Sunset));
					if (sunsetseen && reader.TokenType == JsonTokenType.String)
					{
						count++;
						var t = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);

						// incoming datetimes are GMT/UTC
						var date = DateTime.Parse(t, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal);
						results.Add(new Data() { Date = date, Type = "sunset" });

						sunsetseen = false;
					}
				}

				//
				// weather
				//
				if (type == PredictionType.WeatherInfo)
                {
					forecasthourlyseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_ForecastHourly));
					if (forecasthourlyseen && reader.TokenType == JsonTokenType.String)
					{
						results.Add(new Data() { StrValue = System.Text.Encoding.UTF8.GetString(reader.ValueSpan) });
						forecasthourlyseen = false;
						break;
					}
				}

				if (type == PredictionType.Weather)
                {
					endtimeseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_EndTime));
					if (endtimeseen && reader.TokenType == JsonTokenType.String)
					{
						// only capture the current weather
						if (p.Date != default(DateTime) || min.Date != default(DateTime) || max.Date != default(DateTime))
						{
							collectweather = false;
							continue;
						}

						var t = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);

						// incoming datetimes are local
						var date = DateTime.Parse(t, CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal);

						// check if the end date is in the future
						if (DateTime.UtcNow < date)
                        {
							p = new Data() { Date = date, Type = "weather" };
							min = new Data() { Date = date.AddSeconds(6), Type = "temperaturelow", Value = Single.MaxValue };
							max = new Data() { Date = date.AddSeconds(7), Type = "temperaturehigh", Value = Single.MinValue };
							collectweather = true;
						}

						endtimeseen = false;
					}

					temperatureseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_Temperature));
					if (temperatureseen && reader.TokenType == JsonTokenType.Number)
					{
						var temp = 0f;
						reader.TryGetSingle(out temp);
						if (collectweather && p.Date != default(DateTime))
						{
							results.Add( new Data() { Date = p.Date.AddSeconds(1), Value = temp, Type = "temperature" } );
						}
						if (min.Date != default(DateTime) && min.Date.AddHours(24) >= p.Date) min.Value = Math.Min(min.Value, temp);
						if (max.Date != default(DateTime) && max.Date.AddHours(24) >= p.Date) max.Value = Math.Max(max.Value, temp);
						temperatureseen = false;
					}

					temperaturetrendseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_TemperatureTrend));
					if (temperaturetrendseen && (reader.TokenType == JsonTokenType.String || reader.TokenType == JsonTokenType.Null))
					{
						if (collectweather && p.Date != default(DateTime) && reader.TokenType != JsonTokenType.Null)
						{
							results.Add(new Data() { Date = p.Date.AddSeconds(2), StrValue = System.Text.Encoding.UTF8.GetString(reader.ValueSpan), Type = "temperaturetrend" });
						}
						temperaturetrendseen = false;
					}

					windspeedseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_WindSpeed));
					if (windspeedseen && (reader.TokenType == JsonTokenType.String || reader.TokenType == JsonTokenType.Null))
					{
						if (collectweather && p.Date != default(DateTime) && reader.TokenType != JsonTokenType.Null)
						{
							results.Add(new Data() { Date = p.Date.AddSeconds(3), StrValue = System.Text.Encoding.UTF8.GetString(reader.ValueSpan), Type = "windspeed" });
						}
						windspeedseen = false;
					}

					winddirectionseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_WindDirection));
					if (winddirectionseen && (reader.TokenType == JsonTokenType.String || reader.TokenType == JsonTokenType.Null))
					{
						if (collectweather && p.Date != default(DateTime) && reader.TokenType != JsonTokenType.Null)
						{
							results.Add(new Data() { Date = p.Date.AddSeconds(4), StrValue = System.Text.Encoding.UTF8.GetString(reader.ValueSpan), Type = "winddirection" });
						}
						winddirectionseen = false;
					}

					shortforecastseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_ShortForecast));
					if (shortforecastseen && (reader.TokenType == JsonTokenType.String || reader.TokenType == JsonTokenType.Null))
					{
						if (collectweather && p.Date != default(DateTime) && reader.TokenType != JsonTokenType.Null)
						{
							results.Add(new Data() { Date = p.Date.AddSeconds(5), StrValue = System.Text.Encoding.UTF8.GetString(reader.ValueSpan), Type = "shortforecast" });
						}
						shortforecastseen = false;
					}
				}

				//
				// station
				//
				if (type == PredictionType.Station)
				{
					utcdateseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_UtcDate));
					if (utcdateseen && reader.TokenType == JsonTokenType.String)
					{
						var t = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);

						// incoming datetimes are GMT/UTC
						var date = DateTime.Parse(t, CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
						p = new Data() { Date = date, Type = "station" };

						utcdateseen = false;
					}

					signalseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_Signal));
					if (signalseen && reader.TokenType == JsonTokenType.Null) signalseen = false;
					if (signalseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "signal" });
						signalseen = false;
					}

					lowbatteryseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_Lowbattery));
					if (lowbatteryseen && reader.TokenType == JsonTokenType.Null) lowbatteryseen = false;
					if (lowbatteryseen && reader.TokenType != JsonTokenType.PropertyName)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = reader.TokenType == JsonTokenType.True ? 1f : 0f, Type = "lowbattery" });
						lowbatteryseen = false;
					}

					windspeedseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_WindSpeed));
					if (windspeedseen && reader.TokenType == JsonTokenType.Null) windspeedseen = false;
					if (windspeedseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "windspeed" });
						windspeedseen = false;
					}

					winddirectionseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_WindDirection));
					if (winddirectionseen && reader.TokenType == JsonTokenType.Null) winddirectionseen = false;
					if (winddirectionseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "winddirection" });
						winddirectionseen = false;
					}

					raintotalseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_RaintTotal));
					if (raintotalseen && reader.TokenType == JsonTokenType.Null) raintotalseen = false;
					if (raintotalseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "raintotal" });
						raintotalseen = false;
					}

					outtemperatureseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_OutTemperature));
					if (outtemperatureseen && reader.TokenType == JsonTokenType.Null) outtemperatureseen = false;
					if (outtemperatureseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "outtemperature" });
						outtemperatureseen = false;
					}

					intemperatureseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_InTemperature));
					if (intemperatureseen && reader.TokenType == JsonTokenType.Null) intemperatureseen = false;
					if (intemperatureseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "intemperature" });
						intemperatureseen = false;
					}

					outhumidityseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_OutHumidity));
					if (outhumidityseen && reader.TokenType == JsonTokenType.Null) outhumidityseen = false;
					if (outhumidityseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "outhumidity" });
						outhumidityseen = false;
					}

					pressureseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_Pressure));
					if (pressureseen && reader.TokenType == JsonTokenType.Null) pressureseen = false;
					if (pressureseen && reader.TokenType == JsonTokenType.Number)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Value = Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)), Type = "pressure" });
						pressureseen = false;
					}

					pressuretrendseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_PressureTrend));
					if (pressuretrendseen && reader.TokenType == JsonTokenType.Null) pressuretrendseen = false;
					if (pressuretrendseen && reader.TokenType == JsonTokenType.StartArray)
                    {
						pressuretrend.Clear();
                    }
					if (pressuretrendseen && reader.TokenType == JsonTokenType.Number)
					{
						pressuretrend.Add( Convert.ToSingle( System.Text.Encoding.UTF8.GetString(reader.ValueSpan) ));
					}
					if (pressuretrendseen && reader.TokenType == JsonTokenType.EndArray)
                    {
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Values = pressuretrend.ToArray(), Type = "pressuretrend" });
						pressuretrendseen = false;
						pressuretrend.Clear();
					}

					raintotaltrendseen |= (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual(s_RainTotalTrend));
					if (raintotaltrendseen && reader.TokenType == JsonTokenType.Null) raintotaltrendseen = false;
					if (raintotaltrendseen && reader.TokenType == JsonTokenType.StartArray)
					{
						raintotaltrend.Clear();
					}
					if (raintotaltrendseen && reader.TokenType == JsonTokenType.Number)
					{
						raintotaltrend.Add(Convert.ToSingle(System.Text.Encoding.UTF8.GetString(reader.ValueSpan)));
					}
					if (raintotaltrendseen && reader.TokenType == JsonTokenType.EndArray)
					{
						p.Date = p.Date.AddMilliseconds(10);
						results.Add(new Data() { Date = p.Date, Values = raintotaltrend.ToArray(), Type = "raintotaltrend" });
						raintotaltrendseen = false;
						raintotaltrend.Clear();
					}
				} // (type == PredictionType.Station)
			} // while(reader.Read())

			// add the last prediction
			if (count > 0) results.Add(p);
			if (min.Date != default(DateTime) && min.Value > Single.MinValue) results.Add(min);
			if (max.Date != default(DateTime) && max.Value < Single.MaxValue) results.Add(max);

			return results;
		}

		private async Task<List<Data>> LatestPredictions(
			PredictionType type,
			int deleteAfterMinutes,       // if data is older than X minutes delete
			int lookaheadMinutes,         // when querying request data X minutes in the future
			int cacheInvalidationMinutes, // invalidate the cache after X minutes
			int pullMoreMinutes,          // request more data if the newest data is older X minutes old
			Func<DateTime, DateTime, Task<List<string>>> retrieveAdditional
			)
		{
			PredictionDetails details = null;
			lock (AllData)
			{
				// get prediction
				if (!AllData.TryGetValue(type, out details))
				{
					details = new PredictionDetails();
					AllData.Add(type, details);
				}
			}

			// init
			var now = DateTime.UtcNow;
			var past = now.AddMinutes(-1 * Math.Abs(deleteAfterMinutes));
			var future = now.AddMinutes(lookaheadMinutes);
			var datetimewindow = now.AddMinutes(pullMoreMinutes);

			// check if we have a cahce for this prediction
			lock (details)
			{
				if (details.Cache != null && details.Cache.Count > 0 && details.Timer.IsRunning && details.Timer.Elapsed.TotalMinutes < cacheInvalidationMinutes)
				{
					// return this early
					return details.Cache;
				}
			}

			// ensure we have x hours worth of data
			var tries = 3;
			var results = new List<Data>();
			do
			{
				List<string> remove = null;
				var latest = default(DateTime);

				// iterate through results
				//  1. identify results that can be removed
				//  2. get the latest datetime stamp
				//  3. capture results that will ve returned
				lock (details)
				{
					foreach (var kvp in details.Data)
					{
						// 1. too old
						if (kvp.Value.Date < past)
						{
							if (remove == null) remove = new List<string>();
							remove.Add(kvp.Key);
						}
						// 2. capture latest datetimestamp
						if (kvp.Value.Date > latest) latest = kvp.Value.Date;
						// 3. results
						if (kvp.Value.Date >= past) results.Add(kvp.Value);
					}
				}

				// query for results
				if (latest == default(DateTime) || results.Count == 0 || datetimewindow > latest)
				{
					// this call likely involves a network call, which may fail
					// in order to be resilent to network failures, in that case 
					// no data is returned
					List<string> latestjson = null;
					try
					{
						latestjson = await retrieveAdditional(past, future);
					}
					catch(Exception e)
                    {
						Console.WriteLine($"Catastrophic failure during get - {e}");
						return new List<Data>();
                    }

					// query for more results
					foreach (var json in latestjson)
					{
						try
						{
							var data = ParseJson(json, type);

							lock (details)
							{
								foreach (var p in data)
								{
									// add it to the set of predictions
									var key = $"{p.Date:yyyyMMdd HH mm ss fff}";
									if (!details.Data.ContainsKey(key))
									{
										details.Data.Add(key, p);
									}
								}
							}
						}
						catch(Exception e)
                        {
							Console.WriteLine($"Failure during parse - {e}");
						}
					}

					// clear so that we capture again
					results.Clear();
				}

				// remove the items that are too old
				if (remove != null && remove.Count > 0)
				{
					lock (details)
					{
						foreach (var key in remove) details.Data.Remove(key);
					}
				}

				// we failed to successfully get data, return none
				if (--tries == 0) return new List<Data>();
			}
			while (results.Count == 0);

			// update the details with the cache
			lock (details)
			{
				details.Cache = results;
				details.Timer.Restart();

				return details.Cache;
			}
		}

		private async Task<string> GetWebJson(string url)
		{
			if (string.IsNullOrWhiteSpace(url)) throw new Exception("Must pass in valid url");

			Console.WriteLine($"{DateTime.UtcNow:O} Querying {url}...");

			if (OnQuery != null) OnQuery();

			var tries = 10;
			while(--tries > 0)
			{
				try
				{
					// create request object
					using (var httpClient = new HttpClient())
					{
						using (var request = new HttpRequestMessage(HttpMethod.Get, url))
						{
							// add custom http header
							request.Headers.Add("User-Agent", $"tides {Latitude + Longitude}");

							// send request
							using (var httpResponse = await httpClient.SendAsync(request))
							{
								return await httpResponse.Content.ReadAsStringAsync();
							}
						}
					}
				}
				catch(System.Exception e)
				{
					Console.WriteLine(e);
				}

				// pause and retry
				System.Threading.Thread.Sleep(100);
			}

			throw new Exception("Failed to retrieve text");
		}

		private async Task<bool> QuickCheck(string url, int millisecondsDelay)
		{
			if (string.IsNullOrWhiteSpace(url)) throw new Exception("Must pass in valid url");

			try
			{
				// create a cancellation token
				var cts = new CancellationTokenSource(millisecondsDelay);

				// create request object
				using (var httpClient = new HttpClient())
				{
					using (var request = new HttpRequestMessage(HttpMethod.Get, url))
					{
						// send request
						using (var httpResponse = await httpClient.SendAsync(request, cts.Token))
						{
							return httpResponse.IsSuccessStatusCode;
						}
					}
				}
			}
			catch (System.Exception)
			{
				return false;
			}
		}
		#endregion
	}
}
