using System;

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
    }
}
