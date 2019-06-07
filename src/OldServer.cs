using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WebsocketServer;

namespace WhoCares
{
    class Redundant
    {
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _clientDataStream;
        private TimeSpan _handshakeTimeout = new TimeSpan(0, 0, 10); /* Timeout from when client connects to them requesting a handshake */
        private TimeSpan _clientTimeout = new TimeSpan(0, 0, 10); /* Time for a client to respond to a Ping-request before they are dropped */
        private Stopwatch _clientHeartbeatTracker = new Stopwatch();
        private TimeSpan _checkClientInterval = new TimeSpan(0, 1, 0); /* Interval at which at Ping-request is sent to the client */
        private readonly string _handshakeKey = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private bool _expectingClientPong = false;
        private Stopwatch _clientPongCounter = new Stopwatch();

        public Redundant(string address, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(address), port);
            Console.WriteLine($"WebsocketListener has been initialized on {address}:{port}" + Environment.NewLine);
        }

        public void Start()
        {
            try
            {
                _listener.Start();
                Console.WriteLine("WebsocketListener has been started and is able to accept connections" + Environment.NewLine);
            }
            catch (SocketException error)
            {
                Console.WriteLine("WARNING: WebsocketListener was NOT able to start");
                Console.WriteLine(error.Message);
            }
        }

        private void handleClient()
        {
            Console.WriteLine("Ready to send and receive messages to/from client" + Environment.NewLine);
            _clientHeartbeatTracker.Start();

            while (true)
            {
                Thread.Sleep(100);
                if (_clientHeartbeatTracker.Elapsed > _checkClientInterval)
                    checkClientIsAlive();

                if (_expectingClientPong && _clientPongCounter.Elapsed > _clientTimeout)
                    break;

                if (!_clientDataStream.DataAvailable)
                    continue;

                Byte[] bytes = new Byte[_client.Available];
                _clientDataStream.Read(bytes, 0, bytes.Length);
                Console.WriteLine($"Message received from client ({bytes.Length} bytes)" + Environment.NewLine);
            }

            Console.WriteLine($"WARNING: Client hasn't responded to pings within the timeout {_checkClientInterval}");
            onDisconnectClient();
        }

        private void pingClient()
        {
            string heartbeatIdentifier = "<:HEARTBEAT:>";
            byte[] heartbeatPayload = Encoding.UTF8.GetBytes(heartbeatIdentifier);
            byte[] header = new byte[2] { (byte)(128 | WebsocketProtocol.OPT_CODES["Ping"]), (byte)heartbeatIdentifier.Length };

            _clientDataStream.Write(header, 0, header.Length);
            _clientDataStream.Write(heartbeatPayload, 0, heartbeatPayload.Length);
        }

        private void outputBits(byte[] bytes)
        {
            BitArray bits = new BitArray(bytes);
            string outputted = "";

            foreach (bool bit in bits)
            {
                outputted += (bit ? "1" : "0");
            }
            Console.WriteLine("------BITS--------");
            Console.WriteLine(outputted);
            Console.WriteLine("------------------");
        }

        private void checkClientIsAlive()
        {
            Console.WriteLine("Sending Ping request to client to see if they are still alive");

            _clientHeartbeatTracker.Reset();
            _expectingClientPong = true;
            _clientPongCounter.Start();
            pingClient();
        }

        private void onPongReceived()
        {
            Console.WriteLine("Received Pong-message from cliet");

            if (!_expectingClientPong)
                return;

            _clientPongCounter.Reset();
            _expectingClientPong = false;
            _clientHeartbeatTracker.Start();
        }

        private void onDisconnectClient()
        {
            Console.WriteLine("Client disconnected on our end, closing connection");
            _client.Close();
            _clientHeartbeatTracker.Reset();
            _clientPongCounter.Reset();
        }

        private void onPingReceived(byte[] pingPayload)
        {
            byte[] header = new byte[2] { (byte)(128 | WebsocketProtocol.OPT_CODES["Pong"]), (byte)pingPayload.Length };

            _clientDataStream.Write(header, 0, header.Length);
            _clientDataStream.Write(pingPayload, 0, pingPayload.Length);
        }

        private void onTextFrame(byte[] frameData)
        {
            Console.WriteLine("--------------------TEXT FRAME:");
            Console.WriteLine(Encoding.UTF8.GetString(frameData) + Environment.NewLine);
        }

        private void onBinaryFrame(byte[] frameData)
        {
            Console.WriteLine("BINARY FRAME: NOT IMPLEMENTED");
        }
    }
}
