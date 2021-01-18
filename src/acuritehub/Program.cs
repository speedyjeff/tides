using System;
using System.Collections.Generic;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using LibUsbDotNet.Info;

using Acurite;

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
            switch (options.Mode)
            {
                // client
                case ModeName.Client:
                    if (options.Transport == TransportName.Udp) program.UdpReceiveAsync(options);
                    else if (options.Transport == TransportName.Http) program.HttpReceiveAsync(options);
                    break;

                // server
                case ModeName.Server:
                    if (options.Transport == TransportName.Udp) program.UdpSendAsync(options);
                    else if (options.Transport == TransportName.Http) program.HttpSendAsync(options);
                    break;

                default: throw new Exception($"unknown mode : {options.Mode}");
            }
            Console.ReadLine();

            return 0;
        }

        #region private
        private event Action<AcuriteData> OnPolled;

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
        private void UdpReceiveAsync(Options options)
        {
            // wait to receive data from the server
            var remote = new UdtAcuriteData(options.Port);
            remote.OnReceived += (payload) =>
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<AcuriteData>(payload);
                Console.WriteLine($"{DateTime.Now:o}: Channel: {data.channel} SensorId: {data.sensorId} Signal: {data.signal} Battery: {data.lowBattery} WindSpeed: {data.windSpeed} WindDirection: {data.windDirection} RainTotal: {data.rainTotal} OutTemperature: {data.outTemperature} OutHumitiy: {data.outHumidity} Pressure: {data.pressure} InTemperature: {data.inTemperature}");
                Console.WriteLine($"{payload}");
            };
            remote.ListenAsync();
        }

        private void UdpSendAsync(Options options)
        {
            // setup to send data via udp
            var remote = new UdtAcuriteData(options.Port);

            // subscribe to notifications when data is polled
            OnPolled += async (data) =>
            {
                var json = System.Text.Json.JsonSerializer.Serialize<AcuriteData>(data);
                await remote.SendAsync(json);
            };

            // poll
            PollStation(options);
        }

        //
        // Http
        //
        private async void HttpReceiveAsync(Options options)
        {
            var http = new HttpAcuriteData(options.Port, listenLocal: true);
            
            while(true)
            {
                var payload = await http.ReceiveAsync("acuritehub");
                var data = System.Text.Json.JsonSerializer.Deserialize<AcuriteData>(payload);
                Console.WriteLine($"{DateTime.Now:o}: Channel: {data.channel} SensorId: {data.sensorId} Signal: {data.signal} Battery: {data.lowBattery} WindSpeed: {data.windSpeed} WindDirection: {data.windDirection} RainTotal: {data.rainTotal} OutTemperature: {data.outTemperature} OutHumitiy: {data.outHumidity} Pressure: {data.pressure} InTemperature: {data.inTemperature}");
                Console.WriteLine($"{payload}");

                System.Threading.Thread.Sleep(5000);
            }
        }

        private void HttpSendAsync(Options options)
        {
            var http = new HttpAcuriteData(options.Port, listenLocal: false);
            AcuriteData current = new AcuriteData();

            // get the latest data
            OnPolled += (data) => 
            { 
                current = data; 
            };

            // serialize upon request
            http.OnSend += () =>
            {
                var json = System.Text.Json.JsonSerializer.Serialize<AcuriteData>(current);
                return json;
            };

            // start listening
            http.SendAsync();

            // poll
            PollStation(options);
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
            while (true)
            {
                // read data
                AcuriteData current;
                if (options.VendorId.HasValue && options.ProductId.HasValue) current = station.Read(options.VendorId.Value, options.ProductId.Value);
                else current = station.Read();

                // send if there is data
                if (current.HasValue())
                {
                    // combine the data into a view of the latest
                    combined = combined + current;

                    // display
                    var data = combined;
                    if (options.RawData) data = current;
                    Console.WriteLine($"{DateTime.Now:o}: {data.channel},{data.sensorId},{data.signal},{data.lowBattery},{data.windSpeed},{data.windDirection},{data.rainTotal},{data.outTemperature},{data.outHumidity},{data.pressure},{data.inTemperature}");

                    // notifiy of data
                    if (OnPolled != null) OnPolled(data);
                }

                System.Threading.Thread.Sleep(5000);
            }
        }
        #endregion
    }
}
