using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WebsocketServer
{
    class Server
    {
        private const int MAX_CLIENTS = 8;
        private readonly ConcurrentBag<Task> Clients;
        public TcpListener Listener { get; }

        /* CONSTRUCTOR */
        public Server(string Address, int Port)
        {
            Listener = new TcpListener(IPAddress.Parse(Address), Port);
            Clients = new ConcurrentBag<Task>();

            Console.WriteLine($"WebsocketListener has been initialized on {Address}:{Port}" + Environment.NewLine);
        }

        public void HandlePotentialClient(TcpClient Client)
        {
            Console.WriteLine($"Potential client connected from {((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString()}:{((IPEndPoint)Client.Client.RemoteEndPoint).Port.ToString()}");

        }

        public void Listen()
        {
            while (true)
            {
                TcpClient newClient = Listener.AcceptTcpClient();

                if (Clients.Count < MAX_CLIENTS)
                    Clients.Add(Task.Run(() => HandlePotentialClient(newClient)));
                else
                    Console.WriteLine("Max client connections reached");
            }
        }

        public void Start()
        {
            try
            {
                Listener.Start();
            }
            catch (SocketException error)
            {
                Console.WriteLine("WARNING: WebsocketListener was NOT able to start");
                Console.WriteLine(error.Message);
            }

            // Query status of clients loop?
            Listen();
        }
    }
}
