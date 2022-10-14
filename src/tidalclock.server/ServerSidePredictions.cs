using External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tidalclock.server.Data;

namespace tidalclock.server
{
    // leverages SingalR to communicate with the server to retrieve the data gathered from network endpoints
    // previously the wasm version was on the border of CORS isues due to cross site calls, this eliminates that issue
    internal class ServerSidePredictions : IPredictions
    {
        public ServerSidePredictions(PredictionsService service)
        {
            Service = service;
            Location = "";
        }

        public async void InitializeAsync(string location, int noahStationId, float lat, float lng, string subnet)
        { 
            Location = location;
            IsInitialized = false;

            // initialize
            var success = await Service.InitializeAsync(location, noahStationId, lat, lng, subnet);

            if (!success) throw new Exception("failed to initialize");
            else IsInitialized = true;
        }

        public string Location { get; set; }

        public async Task<List<External.Data>> CurrentExtremes()
        {
            if (!IsInitialized) return await GetEmpty();
            return await Service.CurrentExtremes();
        }

        public async Task<List<External.Data>> CurrentSuns()
        {
            if (!IsInitialized) return await GetEmpty();
            return await Service.CurrentSuns();
        }

        public async Task<List<External.Data>> CurrentTides()
        {
            if (!IsInitialized) return await GetEmpty();
            return await Service.CurrentTides();
        }

        public async Task<List<External.Data>> CurrentWeather()
        {
            if (!IsInitialized) return await GetEmpty();
            return await Service.CurrentWeather();
        }

        public async Task<List<External.Data>> CurrentWeatherStation()
        {
            if (!IsInitialized) return await GetEmpty();
            return await Service.CurrentWeatherStation();
        }

        #region private
        private PredictionsService Service;
        private bool IsInitialized;

        private Task<List<External.Data>> GetEmpty()
        {
            return Task.Run(() => { return new List<External.Data>(); });
        }
        #endregion
    }
}
