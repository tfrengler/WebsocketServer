using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace WebsocketServer
{
    class Server
    {
        private const int MAX_CLIENTS = 8;
        private readonly TimeSpan CLIENT_KEEP_ALIVE = new TimeSpan(0,0,30);
        private readonly WebsocketClient[] Clients;
        public HttpListener Gateway { get; }

        /// <summary>CONSTRUCTOR</summary>
        /// <param name="Address">The IP address you want the server to run on</param>
        /// <param name="Port">The port you want the server to run on</param>
        public Server(string Address, int Port)
        {
            this.Clients = new WebsocketClient[MAX_CLIENTS];
            this.Gateway = new HttpListener();
            this.Gateway.Prefixes.Add("http://" + Address + ":" + Port + "/");

            Console.WriteLine($"Websocket server has been initialized on {Address}:{Port}" + Environment.NewLine);
        }

        private int GetFreeClientSlot()
        {
            for (int i = 0; i < Clients.Length; i++)
            {
                if (Clients[i] == null)
                    return i;
            }
            return -1;
        }

        public async Task ListenForAndAccecptClients()
        {
            Console.WriteLine("Server is listening for web socket requests");

            while (true)
            {
                HttpListenerContext HttpConnection = this.Gateway.GetContext();
                HttpListenerRequest HttpRequest = HttpConnection.Request;
                HttpListenerResponse HttpResponse = HttpConnection.Response;

                string RemoteAddress = $"{ HttpRequest.RemoteEndPoint.Address }:{ HttpRequest.RemoteEndPoint.Port}";

                if (!HttpRequest.IsWebSocketRequest)
                {
                    Console.WriteLine($"Not a WebSocket request, ignore ({RemoteAddress})");
                    HttpResponse.StatusCode = 404;
                    HttpResponse.Close();
                    continue;
                }

                Console.WriteLine($"Websocket request received ({RemoteAddress})");

                int FreeClientSlot = this.GetFreeClientSlot();
                if (FreeClientSlot == -1)
                {
                    Console.WriteLine($"Too many clients connected, dropping connection ({RemoteAddress})");
                    HttpResponse.StatusCode = 418;
                    HttpResponse.Close();
                    continue;
                }

                HttpListenerWebSocketContext WebsocketConnection = await HttpConnection.AcceptWebSocketAsync(null, this.CLIENT_KEEP_ALIVE);
                WebsocketClient NewClient = new WebsocketClient(WebsocketConnection, "UNIQUE", HttpRequest.RemoteEndPoint);
                this.Clients[FreeClientSlot] = NewClient;

                Console.WriteLine($"Client connected and assigned to slot {FreeClientSlot}");
                var _ = Task.Run(() => NewClient.Listen());
            }
        }

        public async Task MonitorAndManageClients()
        {
            Console.WriteLine("Server is monitoring connected clients");

            while (true) {
                await Task.Delay(5000);
                Console.WriteLine(this.Clients.Length);
                //if (this.Clients.Length == 0) continue;
                /*
                for (int i = 0; i < Clients.Length; i++)
                {
                    if (!Clients[i].Alive)
                    {
                        Clients[i] = null;
                        Console.WriteLine($"Released client in slot {i}");
                    };
                }
                */
            }
        }

        public bool Start()
        {
            try
            {
                this.Gateway.Start();
                return true;
            }
            catch (SocketException Error)
            {
                Console.WriteLine("WARNING: Http listener was NOT able to start");
                Console.WriteLine(Error.Message);
                return false;
            }
        }
    }
}
