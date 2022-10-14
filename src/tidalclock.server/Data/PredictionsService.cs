using External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tidalclock.server.Data
{
    public class PredictionsService
    {
        public async Task<bool> InitializeAsync(string location, int noahStationId, float lat, float lng, string subnet)
        {
            ExternalData = new Predictions(location, noahStationId, lat, lng, subnet);
            return await Task.Run(() => { return true; });
        }

        public async Task<List<External.Data>> CurrentExtremes()
        {
            if (ExternalData is null) throw new Exception("must initalize first");
            return await ExternalData.CurrentExtremes();
        }

        public async Task<List<External.Data>> CurrentSuns()
        {
            if (ExternalData is null) throw new Exception("must initalize first");
            return await ExternalData.CurrentSuns();
        }

        public async Task<List<External.Data>> CurrentTides()
        {
            if (ExternalData is null) throw new Exception("must initalize first");
            return await ExternalData.CurrentTides();
        }

        public async Task<List<External.Data>> CurrentWeather()
        {
            if (ExternalData is null) throw new Exception("must initalize first");
            return await ExternalData.CurrentWeather();
        }

        public async Task<List<External.Data>> CurrentWeatherStation()
        {
            if (ExternalData is null) throw new Exception("must initalize first");
            return await ExternalData.CurrentWeatherStation();
        }

        #region private
        private Predictions ExternalData;
        #endregion
    }
}
