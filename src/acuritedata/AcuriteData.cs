using System;
using System.Diagnostics;

namespace Acurite
{
    public enum ChannelName { A = 0xc, B=0x8, C=0x0}
    public enum SignalStrength { None=0, Low=1, Medium=2, High=3}

    public struct AcuriteData
    {
        public DateTime utcDate { get; set; }
        public ChannelName? channel { get; set; }
        public int? sensorId { get; set; }
        public SignalStrength? signal { get; set; }
        public bool? lowBattery { get; set; }
        public float? windSpeed { get; set; } // mph
        public float? windDirection { get; set; } // degrees
        public float? rainTotal { get; set; }  // inches
        public float? outTemperature { get; set; } // fahrenheit
        public float? outHumidity { get; set; } // percentage
        public float? pressure { get; set; } // inHg
        public float? inTemperature { get; set; } // fahrenheit

        public float[] pressureTrend { get; set; }
        public float[] rainTotalTrend { get; set; }

        public static AcuriteData operator +(AcuriteData a, AcuriteData b)
        {
            // sort a before b
            if (a.utcDate > b.utcDate)
            {
                // swap
                var t = a;
                a = b;
                b = t;
            }

            // combine with the most recent favored
            AcuriteData result = new AcuriteData() { utcDate = b.utcDate };
            result.channel = (b.channel.HasValue) ? b.channel : a.channel;
            result.sensorId = (b.sensorId.HasValue) ? b.sensorId : a.sensorId;
            result.signal = (b.signal.HasValue) ? b.signal : a.signal;
            result.lowBattery = (b.lowBattery.HasValue) ? b.lowBattery : a.lowBattery;
            result.windSpeed = (b.windSpeed.HasValue) ? b.windSpeed : a.windSpeed;
            result.windDirection = (b.windDirection.HasValue) ? b.windDirection : a.windDirection;
            result.rainTotal = (b.rainTotal.HasValue) ? b.rainTotal : a.rainTotal;
            result.outTemperature = (b.outTemperature.HasValue) ? b.outTemperature : a.outTemperature;
            result.outHumidity = (b.outHumidity.HasValue) ? b.outHumidity : a.outHumidity;
            result.pressure = (b.pressure.HasValue) ? b.pressure : a.pressure;
            result.inTemperature = (b.inTemperature.HasValue) ? b.inTemperature : a.inTemperature;

            // copy private data
            result.LastPressureTrend = (b.LastPressureTrend != null) ? b.LastPressureTrend : (a.LastPressureTrend != null ? a.LastPressureTrend : new Stopwatch());
            result.LastRainTotalTrend = (b.LastRainTotalTrend != null) ? b.LastRainTotalTrend : (a.LastRainTotalTrend != null ? a.LastRainTotalTrend : new Stopwatch());

            // combine trends
            result.pressureTrend = CombineTrends(result.pressure, a.pressureTrend, b.pressureTrend, result.LastPressureTrend, TrendTiming, TrendMax);
            result.rainTotalTrend = CombineTrends(result.rainTotal, a.rainTotalTrend, b.rainTotalTrend, result.LastRainTotalTrend, TrendTiming, TrendMax);

            return result;
        }

        public bool HasValue()
        {
            return channel.HasValue ||
                sensorId.HasValue ||
                signal.HasValue ||
                lowBattery.HasValue ||
                windSpeed.HasValue ||
                windDirection.HasValue ||
                rainTotal.HasValue ||
                outTemperature.HasValue ||
                outHumidity.HasValue ||
                pressure.HasValue ||
                inTemperature.HasValue;
        }

        #region private
        private const int TrendTiming = 1000 * 60 * 60; // 1 hour in milliseconds
        private const int TrendMax = 24;
        private Stopwatch LastPressureTrend;
        private Stopwatch LastRainTotalTrend;

        private static float[] CombineTrends(float? value, float[] trenda, float[] trendb, Stopwatch timer, int timeout, int max)
        {
            if (!value.HasValue) return null;

            // determine which trend to use
            float[] newtrend = null;
            float[] trend = null;
            var lengtha = (trenda != null) ? trenda.Length : -1;
            var lengthb = (trendb != null) ? trendb.Length : -1;
            if (lengtha > 0 && lengtha > lengthb) trend = trenda;
            else if (lengthb > 0) trend = trendb;

            // add a trend every x milliseonds
            if (trend == null || timer.ElapsedMilliseconds > timeout)
            {
                var size = 1;
                // adjust the space for the additional readings
                if (trend != null)
                {
                    size += trend.Length;
                    if (size > max) size = max;
                }
                // allocate the array
                newtrend = new float[size];
                // add the latest reading
                newtrend[0] = value.Value;
                // include the other readings
                if (trend != null)
                {
                    for (int i = 0; i < trend.Length && (i + 1) < newtrend.Length; i++)
                    {
                        newtrend[i + 1] = trend[i];
                    }
                }

                // restart the timer
                timer.Restart();
            }
            else
            {
                // keep the current trend
                newtrend = trend;
            }

            return newtrend;
        }
        #endregion
    }
}
