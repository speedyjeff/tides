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
            if (b.channel.HasValue) result.channel = b.channel; else result.channel = a.channel;
            if (b.sensorId.HasValue) result.sensorId = b.sensorId; else result.sensorId = a.sensorId;
            if (b.signal.HasValue) result.signal = b.signal; else result.signal = a.signal;
            if (b.lowBattery.HasValue) result.lowBattery = b.lowBattery; else result.lowBattery = a.lowBattery;
            if (b.windSpeed.HasValue) result.windSpeed = b.windSpeed; else result.windSpeed = a.windSpeed;
            if (b.windDirection.HasValue) result.windDirection = b.windDirection; else result.windDirection = a.windDirection;
            if (b.rainTotal.HasValue) result.rainTotal = b.rainTotal; else result.rainTotal = a.rainTotal;
            if (b.outTemperature.HasValue) result.outTemperature = b.outTemperature; else result.outTemperature = a.outTemperature;
            if (b.outHumidity.HasValue) result.outHumidity = b.outHumidity; else result.outHumidity = a.outHumidity;
            if (b.pressure.HasValue) result.pressure = b.pressure; else result.pressure = a.pressure;
            if (b.inTemperature.HasValue) result.inTemperature = b.inTemperature; else result.inTemperature = a.inTemperature;

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
