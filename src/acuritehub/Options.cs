using System;
using System.IO;

namespace acuritehub
{
    enum ModeName { List, Client, Server};
    enum TransportName { Udp, Http};

    class Options
    {
        public int Port;
        public bool ShowHelp;
        public ModeName Mode;
        public int? VendorId;
        public int? ProductId;
        public bool RawData;
        public TransportName Transport;
        public string Protocol;
        public string Hostname;
        public int Interval;
        public long SleepPolling;
        public int MaxPollFailures;

        public Options()
        {
            Port = 11000;
            ShowHelp = false;
            Mode = ModeName.Server;
            RawData = false;
            Transport = TransportName.Http;
            Protocol = "http";
            Hostname = "";
            Interval = 500; // ms
            SleepPolling = 600000; // ms  (10 minutes)
            MaxPollFailures = 100; // times (wall clock is Interval * PollFailureMax ms)
        }

        public static int DisplayHelp()
        {
            Console.WriteLine("./acuritehub [-port ####] [-mode list|client|server] [-transport udp|http|https] [-vendorid ####] [-productid ####] [-raw] [-hostname ...] [-interval ####] [-sleeppoll ####] [-maxpollfailures ###]");
            return 1;
        }

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-help", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "-?", StringComparison.OrdinalIgnoreCase))
                {
                    options.ShowHelp = true;
                }
                else if (string.Equals(args[i], "-raw", StringComparison.OrdinalIgnoreCase))
                {
                    options.RawData = true;
                }
                else if (string.Equals(args[i], "-port", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (Int32.TryParse(args[i], out int port)) options.Port = port;
                    }
                }
                else if (string.Equals(args[i], "-hostname", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        options.Hostname = args[i];
                    }
                }
                else if (string.Equals(args[i], "-vendorid", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (Int32.TryParse(args[i], out int id)) options.VendorId = id;
                    }
                }
                else if (string.Equals(args[i], "-productid", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (Int32.TryParse(args[i], out int id)) options.ProductId = id;
                    }
                }
                else if (string.Equals(args[i], "-interval", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (Int32.TryParse(args[i], out int time)) options.Interval = time;
                    }
                }
                else if (string.Equals(args[i], "-mode", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (string.Equals(args[i], "list", StringComparison.OrdinalIgnoreCase)) options.Mode = ModeName.List;
                        else if (string.Equals(args[i], "client", StringComparison.OrdinalIgnoreCase)) options.Mode = ModeName.Client;
                        else if (string.Equals(args[i], "server", StringComparison.OrdinalIgnoreCase)) options.Mode = ModeName.Server;
                    }
                }
                else if (string.Equals(args[i], "-transport", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (string.Equals(args[i], "udp", StringComparison.OrdinalIgnoreCase)) options.Transport = TransportName.Udp;
                        else if (string.Equals(args[i], "http", StringComparison.OrdinalIgnoreCase)) options.Transport = TransportName.Http;
                        else if (string.Equals(args[i], "https", StringComparison.OrdinalIgnoreCase))
                        {
                            options.Transport = TransportName.Http;
                            options.Protocol = "https";
                        }
                    }
                }
                else if (string.Equals(args[i], "-sleeppoll", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (Int64.TryParse(args[i], out long sleep)) options.SleepPolling = sleep;
                    }
                }
                else if (string.Equals(args[i], "-maxpollfailures", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (Int32.TryParse(args[i], out int max)) options.MaxPollFailures = max;
                    }
                }
                else
                {
                    Console.WriteLine($"Unknown command line parameter : {args[i]}");
                    options.ShowHelp = true;
                }
            }

            // check options
            if (!options.ShowHelp && 
                options.Transport == TransportName.Http && 
                options.Mode == ModeName.Client &&
                string.IsNullOrWhiteSpace(options.Hostname))
            {
                Console.WriteLine("Must provide a valid hostname when listening to http/https");
                options.ShowHelp = true;
            }
            if (options.Interval <= 0)
            {
                Console.WriteLine("Must provide a non-zero and positive interval value");
                options.ShowHelp = true;
            }

            return options;
        }
    }
}
