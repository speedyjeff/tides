using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using LibUsbDotNet.Info;

using Acurite;
using System.Diagnostics;

namespace acuritehub
{
    class Program
    {
        static int Main(string[] args)
        {
            var options = Options.Parse(args);

            if (options.ShowHelp) { return Options.DisplayHelp(); }

            if (options.Mode == ModeName.List) { List(); return 0; }

            // execute the operation
            var program = new Program();
            Console.WriteLine("<ctrl-c> to exit");
            while (true)
            {
                IRemoteAcuriteData remote = null;
                try
                {
                    switch (options.Mode)
                    {
                        // client
                        case ModeName.Client:
                            if (options.Transport == TransportName.Udp) remote = program.UdpReceiveAsync(options);
                            else if (options.Transport == TransportName.Http) program.HttpReceiveAsync(options);
                            break;

                        // server
                        case ModeName.Server:
                            // start the last net polling stopwatch (polling the device times out after inuse)
                            program.LastNetQuery = new Stopwatch();
                            program.LastNetQuery.Start();

                            // start server
                            if (options.Transport == TransportName.Udp) remote = program.UdpSendAsync(options);
                            else if (options.Transport == TransportName.Http) remote = program.HttpSendAsync(options);

                            // poll
                            program.PollStation(options);
                            break;

                        default: throw new Exception($"unknown mode : {options.Mode}");
                    }

                    // wait indefinetly
                    while (true) Console.ReadLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{DateTime.Now:o}: catastrophic failure {e.Message}");
                }

                // wait and retry
                System.Threading.Thread.Sleep(options.Interval);

                // close out before the next restart
                if (remote != null) remote.Close();
            }
        }

        #region private
        private event Action<AcuriteData> OnPolled;
        private Stopwatch LastNetQuery;

        private static void List()
        {
            // display all the vendor and product ids
            foreach (var id in AcuriteStation.All())
            {
                Console.WriteLine($"VendorId: {id.VendorId} (0x{id.VendorId:x}), ProductId: {id.ProductId} (0x{id.ProductId:x})");
            }
        }

        //
        // Udp
        //

        // client
        private IRemoteAcuriteData UdpReceiveAsync(Options options)
        {
            // wait to receive data from the server
            var remote = new UdtAcuriteData(options.Port);
            remote.OnReceived += (payload) =>
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<AcuriteData>(payload);
                Console.WriteLine($"{DateTime.Now:o}: Channel: {data.channel} SensorId: {data.sensorId} Signal: {data.signal} Battery: {data.lowBattery} WindSpeed: {data.windSpeed} WindDirection: {data.windDirection} RainTotal: {data.rainTotal} OutTemperature: {data.outTemperature} OutHumitiy: {data.outHumidity} Pressure: {data.pressure} InTemperature: {data.inTemperature}");
                Console.WriteLine($"{payload}");
            };
            remote.ReceiveAsync();
            return remote;
        }

        // server
        private IRemoteAcuriteData UdpSendAsync(Options options)
        {
            // setup to send data via udp
            var remote = new UdtAcuriteData(options.Port);

            // subscribe to notifications when data is polled
            OnPolled += async (data) =>
            {
                // reset the stopwatch and restart
                LastNetQuery.Reset();
                LastNetQuery.Start();

                // serialize the most current data
                var json = System.Text.Json.JsonSerializer.Serialize<AcuriteData>(data);
                await remote.SendAsync(json);
            };

            return remote;
        }

        //
        // Http
        //

        // client
        private async void HttpReceiveAsync(Options options)
        {
            var http = new HttpAcuriteData(options.Port, options.Protocol, listenLocal: true);

            try
            {
                while (true)
                {
                    var payload = await http.ReceiveAsync(options.Hostname);
                    var data = System.Text.Json.JsonSerializer.Deserialize<AcuriteData>(payload);
                    Console.WriteLine($"{DateTime.Now:o}: Channel: {data.channel} SensorId: {data.sensorId} Signal: {data.signal} Battery: {data.lowBattery} WindSpeed: {data.windSpeed} WindDirection: {data.windDirection} RainTotal: {data.rainTotal} OutTemperature: {data.outTemperature} OutHumitiy: {data.outHumidity} Pressure: {data.pressure} InTemperature: {data.inTemperature}");
                    Console.WriteLine($"{payload}");

                    System.Threading.Thread.Sleep(options.Interval);
                }
            }
            finally
            {
                if (http != null) http.Close();
            }
        }

        // server
        private IRemoteAcuriteData HttpSendAsync(Options options)
        {
            var http = new HttpAcuriteData(options.Port, options.Protocol, listenLocal: false);
            AcuriteData current = new AcuriteData();

            // get the latest data
            OnPolled += (data) => 
            { 
                current = data; 
            };

            // serialize upon request
            http.OnSend += () =>
            {
                // reset the stopwatch and restart
                LastNetQuery.Reset();
                LastNetQuery.Start();

                // serialize the most current data
                var json = System.Text.Json.JsonSerializer.Serialize<AcuriteData>(current);
                return json;
            };

            // start listening
            http.SendAsync();

            return http;
        }

        //
        // Common
        //
        private void PollStation(Options options)
        {
            // setup
            var station = new AcuriteStation();
            var combined = new AcuriteData();

            // poll the station until asked to stop
            var failCount = 0;
            while (true)
            {
                // check if the poll timeout has occured (in which case, skip this station read)
                if (LastNetQuery.ElapsedMilliseconds < options.SleepPolling)
                {
                    // read data
                    AcuriteData current;
                    if (options.VendorId.HasValue && options.ProductId.HasValue) current = station.Read(options.VendorId.Value, options.ProductId.Value);
                    else current = station.Read();

                    // send if there is data
                    if (!current.HasValue())
                    {
                        failCount++;
                        Console.WriteLine($"{DateTime.Now:o}: failed to get a station reading");
                        if (failCount >= options.MaxPollFailures) throw new Exception($"Failed to get station data {failCount} in a row");
                    }
                    else
                    {
                        // reset fail count (if set)
                        failCount = 0;

                        // combine the data into a view of the latest
                        combined = combined + current;

                        // display
                        var data = combined;
                        if (options.RawData) data = current;
                        Console.WriteLine($"{DateTime.Now:o}:{(options.RawData ? " [raw]" : "")} {data.channel},{data.sensorId},{data.signal},{data.lowBattery},{data.windSpeed},{data.windDirection},{data.rainTotal},{data.outTemperature},{data.outHumidity},{data.pressure},{data.inTemperature},{(data.pressureTrend != null ? data.pressureTrend.Length : 0)},{(data.rainTotalTrend != null ? data.rainTotalTrend.Length : 0)}");

                        // notifiy of data
                        if (OnPolled != null) OnPolled(data);
                    }
                }

                System.Threading.Thread.Sleep(options.Interval);
            }
        }
        #endregion
    }
}
