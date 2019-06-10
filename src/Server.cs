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
        private readonly ConcurrentBag<Task> Clients = new ConcurrentBag<Task>();
        public HttpListener Gateway { get; }
        public object TextEncoding { get; private set; }

        /* CONSTRUCTOR */
        public Server(string Address, int Port)
        {
            this.Gateway = new HttpListener();
            this.Gateway.Prefixes.Add("http://" + Address + ":" + Port + "/");

            Console.WriteLine($"Websocket server has been initialized on {Address}:{Port}" + Environment.NewLine);
        }

        public async void Listen()
        {
            while (true)
            {
                HttpListenerContext HttpConnection = this.Gateway.GetContext();
                HttpListenerRequest HttpRequest = HttpConnection.Request;

                if (HttpRequest.IsWebSocketRequest && Clients.Count < MAX_CLIENTS)
                {
                    HttpListenerWebSocketContext WebsocketConnection = await HttpConnection.AcceptWebSocketAsync("chat");
                    WebsocketClient NewClient = new WebsocketClient(WebsocketConnection);
                    Task.Run(()=> NewClient.Listen());
                }

                if (!HttpRequest.IsWebSocketRequest)
                    Console.WriteLine("Not a WebSocket request, ignore");

                if (Clients.Count == MAX_CLIENTS)
                    Console.WriteLine("Too many clients connected");

                Thread.Sleep(50);
            }
        }

        public void Start()
        {
            try
            {
                this.Gateway.Start();
            }
            catch (SocketException Error)
            {
                Console.WriteLine("WARNING: Listener was NOT able to start");
                Console.WriteLine(Error.Message);
            }

            // Query status of clients loop?
            this.Listen();
        }
    }
}
