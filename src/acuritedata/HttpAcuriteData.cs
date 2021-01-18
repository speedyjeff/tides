﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Acurite
{
    public class HttpAcuriteData
    {
        public HttpAcuriteData(int port, bool listenLocal)
        {
            Port = port;
            ListenLocal = listenLocal;
            Listening = false;
        }

        public event Func<string> OnSend;

        public void SendAsync()
        {
            if (Listening) return;

            // initialize
            var host = ListenLocal ? Dns.GetHostEntry("localhost") : Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ip = null;
            Listening = true;

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
            if (ip == null) throw new Exception("Failed to get an ip address to listen too");

            // start http lisenter
            var endpoint = new IPEndPoint(ip, Port);
            var http = new HttpListener();
            http.Prefixes.Add($"{Protocol}://{endpoint}/{ServiceName}/");

            // start
            http.Start();
            Console.WriteLine($"Servering {Protocol}://{endpoint}/{ServiceName}/ ...");

            // async handle the incoming requests
            HandleIncoming(http);
        }

        public async Task<string> ReceiveAsync(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname)) throw new Exception("must provide a valid hostname");
            return await GetWebJson($"{Protocol}://{hostname}:{Port}/{ServiceName}");
        }

        #region private
        private int Port;
        private bool ListenLocal;
        private string Protocol = "http";
        private volatile bool Listening = false;
        private const string ServiceName = "weather";

        private async void HandleIncoming(HttpListener http)
        {
            while (http.IsListening)
            {
                try
                {
                    // block to get request
                    var context = await http.GetContextAsync();
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
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    using (var output = context.Response.OutputStream)
                    {
                        await output.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"exception : {e}");
                }
            }
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