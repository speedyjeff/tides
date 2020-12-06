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

// todo make resilent to network failures

namespace External
{
	public struct Data
	{
		public DateTime Date;
		public float Value;
		public string StrValue;
		public string Type;
	}

	public class Predictions
	{
		public Predictions(string location, int noahStationId, float lat, float lng, int lookaheadhours)
		{
			// storage for all data
			AllData = new Dictionary<PredictionType, PredictionDetails>();

			// init
			Location = location;
			LookaheadHours = lookaheadhours;
			NoahStationId = noahStationId;
			Latitude = lat;
			Longitude = lng;
		}

		public string Location { get; private set; }

		public async Task<List<Data>> CurrentTides()
		{
			return await LatestPredictions(PredictionType.Tides, LookaheadHours, lookaheadmultiplier: 3, retrieveAdditional: async (start, end) =>
			{
				var json = await GetWebJson(string.Format(PredictionUrl, start, end, NoahStationId, "mllw"));
				return new List<string>() { json };
			});
		}

		public async Task<List<Data>> CurrentExtremes()
		{
			return await LatestPredictions(PredictionType.Extremes, LookaheadHours, lookaheadmultiplier: 14, retrieveAdditional: async (start, end) =>
			{
				var json = await GetWebJson(string.Format(PredictionHiloUrl, start, end, NoahStationId, "mllw"));
				return new List<string>() { json };
			});
		}

		public async Task<List<Data>> CurrentSuns()
		{
			return await LatestPredictions(PredictionType.Suns, LookaheadHours, lookaheadmultiplier: 7, retrieveAdditional: async (start, end) =>
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
			return await LatestPredictions(PredictionType.Weather, lookaheadhours: 0, lookaheadmultiplier: 0, retrieveAdditional: async (start, end) =>
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

		public event Action OnQuery;

		#region private
		private Dictionary<PredictionType, PredictionDetails> AllData;
		private int LookaheadHours;
		private int NoahStationId;
		private float Latitude;
		private float Longitude;

		private enum PredictionType { Tides, Extremes, Suns, WeatherInfo, Weather };
		private class PredictionDetails
        {
			public Dictionary<string, Data> Data;
			public List<Data> Cache;
			public Stopwatch Timer;
			public float CacheInvalidationMinutes;

			public PredictionDetails()
            {
				Data = new Dictionary<string, Data>();
				Cache = new List<Data>();
				Timer = new Stopwatch();
				CacheInvalidationMinutes = 0f;
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
			var collectweather = false;
			var results = new List<Data>();
			var p = default(Data);
			var min = default(Data);
			var max = default(Data);
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
			}

			// add the last prediction
			if (count > 0) results.Add(p);
			if (min.Date != default(DateTime) && min.Value > Single.MinValue) results.Add(min);
			if (max.Date != default(DateTime) && max.Value < Single.MaxValue) results.Add(max);

			return results;
		}

		private async Task<List<Data>> LatestPredictions(PredictionType type, int lookaheadhours, int lookaheadmultiplier, Func<DateTime, DateTime, Task<List<string>>> retrieveAdditional)
		{
			PredictionDetails details = null;
			lock (AllData)
			{
				// get prediction
				if (!AllData.TryGetValue(type, out details))
				{
					details = new PredictionDetails()
					{
						CacheInvalidationMinutes = lookaheadhours > 0 ? ((lookaheadhours * 60f) / 12f) : 15f
					};
					AllData.Add(type, details);
				}
			}

			// init
			var now = DateTime.UtcNow;
			var past = now.AddHours(-1 * lookaheadhours);
			var future = now.AddHours(lookaheadhours * lookaheadmultiplier);
			var datetimewindow = now.AddHours(lookaheadhours);

			// check if we have a cahce for this prediction
			lock (details)
			{
				if (details.Cache != null && details.Cache.Count > 0 && details.Timer.IsRunning && details.Timer.Elapsed.TotalMinutes < details.CacheInvalidationMinutes)
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
				if (latest == default(DateTime) || datetimewindow > latest)
				{
					// query for more results
					foreach (var json in await retrieveAdditional(past, future))
					{
						var data = ParseJson(json, type);

						lock (details)
						{
							foreach (var p in data)
							{
								// add it to the set of predictions
								var key = $"{p.Date:yyyyMMdd HH mm ss}";
								if (!details.Data.ContainsKey(key))
								{
									details.Data.Add(key, p);
								}
							}
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

				if (--tries == 0) throw new Exception("Failed to get sufficient tide results");
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

			Console.WriteLine($"Querying {url}...");

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
							request.Headers.Add("User-Agent", $"tids {Latitude + Longitude}");

							// send request
							using (var httpResponse = await httpClient.SendAsync(request))
							{
								return await httpResponse.Content.ReadAsStringAsync();
							}
						}
					}
				}
				catch(System.Net.WebException e)
				{
					Console.WriteLine(e);
				}

				// pause and retry
				System.Threading.Thread.Sleep(5000);
			}

			throw new Exception("Failed to retrieve text");
		}
		#endregion
	}
}
