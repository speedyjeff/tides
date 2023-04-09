using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

// todo OPTIONS request should be handled this way
// https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods/OPTIONS

namespace Acurite
{
    public class HttpAcuriteData : IRemoteAcuriteData
    {
        public HttpAcuriteData(int port, string protocol, bool listenLocal)
        {
            Port = port;
            ListenLocal = listenLocal;
            Listening = false;
            Protocol = protocol;
            Http = null;
        }

        public event Func<string> OnSend;

        public async void SendAsync()
        {
            if (Listening) return;

            // initialize
            var ip = GetIpAddress(ListenLocal);
            if (ip == null) throw new Exception("failed to get an appropriate local ip address");
            Listening = true;

            // start http listener
            var endpoint = new IPEndPoint(ip, Port);
            var url = $"{Protocol}://{endpoint}/{ServiceName}/";
            Http = new HttpListener();
            Http.Prefixes.Add(url);

            // start
            Http.Start();
            Console.WriteLine($"Servering {url} ...");

            while (Http != null && Http.IsListening)
            {
                // block to get request
                var context = await Http.GetContextAsync();
                var contenttype = context.Request.AcceptTypes != null && context.Request.AcceptTypes.Length > 0 ? context.Request.AcceptTypes[0] : "";

                // log the incoming request
                Console.Write($"{System.Threading.Thread.CurrentThread.ManagedThreadId} {DateTime.Now:o} \"{context.Request.HttpMethod} {contenttype} {Protocol}/{context.Request.RawUrl} {context.Request.ProtocolVersion}\" ");

                // get context to send
                var responseString = "";
                if (OnSend != null)
                {
                    responseString = OnSend();
                    contenttype = "application/json";
                    context.Response.StatusCode = 200;
                }
                else
                {
                    contenttype = "text/text";
                    context.Response.StatusCode = 404;
                }

                // log the response
                Console.WriteLine($"{responseString.Length} {context.Response.StatusCode}");

                // write
                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = contenttype;
                // disabling this CORS header, as the browser has its own disable mechanism and they are not matching
                //context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                using (var output = context.Response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
            }
        }

        public void Close()
        {
            if (Http != null)
            {
                if (Http.IsListening)
                {
                    Http.Close();
                }
            }
            Http = null;
        }

        public async Task<string> ReceiveAsync(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname)) throw new Exception("must provide a valid hostname");
            return await GetWebJson($"{Protocol}://{hostname}:{Port}/{ServiceName}");
        }

        #region private
        private int Port;
        private bool ListenLocal;
        private HttpListener Http;
        private string Protocol;
        private volatile bool Listening = false;
        private const string ServiceName = "weather";

        private IPAddress GetIpAddress(bool local)
        {
            var host = local ? Dns.GetHostEntry("localhost") : Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ip = null;

            // choose appropriate ip address
            foreach (var lip in host.AddressList)
            {
                // ipv4
                if (lip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ip = lip;
                    break;
                }
            }
            if (ip == null && host.AddressList.Length >= 1) ip = host.AddressList[0];
            if (ip == null) return null;

            // check if loopback
            var straddr = ip.ToString();
            var isloopback = IPAddress.IsLoopback(ip) || String.IsNullOrWhiteSpace(straddr) || straddr.StartsWith("127.");

            // check to see if wanting an externally visible ip address but only got loopback
            if (!local && isloopback) return null;

            // success
            return ip;
        }

        private async Task<string> GetWebJson(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new Exception("Must pass in valid url");

            Console.WriteLine($"{DateTime.UtcNow:O} Querying {url}...");

            var tries = 10;
            while (--tries > 0)
            {
                try
                {
                    // create request object
                    using (var httpClient = new HttpClient())
                    {
                        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                        {
                            // send request
                            using (var httpResponse = await httpClient.SendAsync(request))
                            {
                                return await httpResponse.Content.ReadAsStringAsync();
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Console.WriteLine(e);
                }

                // pause and retry
                System.Threading.Thread.Sleep(100);
            }

            throw new Exception("Failed to retrieve text");
        }
        #endregion
    }
}
