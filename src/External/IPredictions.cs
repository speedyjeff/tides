using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace External
{
    public interface IPredictions
    {
        public string Location { get; set; }

        public Task<List<Data>> CurrentTides();

        public Task<List<Data>> CurrentExtremes();

        public Task<List<Data>> CurrentSuns();

        public Task<List<Data>> CurrentWeather();

        public Task<List<Data>> CurrentWeatherStation();
    }
}
