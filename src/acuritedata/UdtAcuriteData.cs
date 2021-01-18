using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Acurite
{
    public class UdtAcuriteData
    {
        public UdtAcuriteData(int port)
        {
            Listening = false;
            Port = port;
            UdpEndpoint = null;
        }

        public event Action<string> OnReceived;

        public async void ListenAsync()
        {
            // exit early if already listening
            if (Listening) return;

            // start listening and setup client
            Listening = true;
            var remoteEndpoint = new IPEndPoint(IPAddress.Any, Port);
            UdpListener = new UdpClient(remoteEndpoint);

            while (Listening)
            {
                // listen
                var data = await UdpListener.ReceiveAsync();

                // decode and fire event
                var payload = System.Text.Encoding.ASCII.GetString(data.Buffer);
                if (OnReceived != null) OnReceived(payload);
            }
        }

        public void StopListening()
        {
            Listening = false;
            if (UdpListener != null) UdpListener.Close();
            UdpListener = null;
        }

        public async Task<bool> SendAsync(string payload)
        {
            // setup endpoint
            if (UdpEndpoint == null)
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
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
                if (ip == null) throw new Exception("Failed to get an ip address to listen too");

                // store end point
                UdpEndpoint = new IPEndPoint(ip, Port);
            }

            var bytes = System.Text.Encoding.ASCII.GetBytes(payload);

            using (var udpclient = new UdpClient())
            {
                udpclient.Connect(UdpEndpoint);
                await udpclient.SendAsync(bytes, bytes.Length);
            }

            return true;
        }

        #region private
        private int Port;
        private IPEndPoint UdpEndpoint;
        private volatile bool Listening;
        private UdpClient UdpListener;
        #endregion
    }
}
