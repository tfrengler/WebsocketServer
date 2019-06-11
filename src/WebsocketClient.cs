using System;
using System.Threading;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Net;

namespace WebsocketServer
{
    class WebsocketClient
    {
        // PROPERTIES
        public string ID { get; }
        private readonly HttpListenerWebSocketContext Client;
        private readonly WebSocket Stream;
        private readonly ArraySegment<byte> MessageBuffer = WebSocket.CreateServerBuffer(1024);
        public IPEndPoint Address { get; }
        public DateTime TimeConnected { get; }
        public DateTime TimeDisconnected { get; }
        public bool Alive { get; private set; } = true;

        // METHODS
        public WebsocketClient(HttpListenerWebSocketContext WebsocketConnection, string ID, IPEndPoint Address)
        {
            this.ID = ID;
            this.Address = Address;
            this.Client = WebsocketConnection;
            this.Stream = this.Client.WebSocket;

            Console.WriteLine($"Websocket client initialized ({this.ID})");
        }

        public void SendMessage(WebSocketMessageType Type, byte[] Payload)
        {
            CancellationTokenSource SendCancelSignal = new CancellationTokenSource(new TimeSpan(0, 0, 30));
            this.Stream.SendAsync(new ArraySegment<byte>(Payload), Type, true, SendCancelSignal.Token);
        }

        private void HandleMessage(byte[] Data, WebSocketMessageType Type)
        {
            if (Type == WebSocketMessageType.Text)
            {
                Console.WriteLine($"---------------------TEXT MESSAGE RECEIVED ({Data.Length} bytes):");

                Console.WriteLine(System.Text.Encoding.UTF8.GetString(Data));
                Console.WriteLine($"---------------------END TEXT MESSAGE" + Environment.NewLine);

                this.SendMessage(WebSocketMessageType.Text, Data);
            }
            else if (Type == WebSocketMessageType.Binary)
            {
                Console.WriteLine($"BINARY MESSAGE RECEIVED ({Data.Length} bytes): NOT IMPLEMENTED");
            }
        }

        public async void Listen()
        {
            Console.WriteLine($"Websocket client listening for messages");

            while (true)
            {
                CancellationTokenSource ReadCancelSignal = new CancellationTokenSource(new TimeSpan(0, 10, 0));
                WebSocketReceiveResult MessageHeader;

                try
                {
                    MessageHeader = await this.Stream.ReceiveAsync(this.MessageBuffer, ReadCancelSignal.Token);
                }
                catch(TaskCanceledException)
                {
                    this.OnMessageReceiveTimeout();
                    return;
                }

                if (MessageHeader.CloseStatus > 0)
                {
                    this.OnConnectionClosed();
                    return;
                }

                byte[] ExtractedMessage = new byte[MessageHeader.Count];
                Buffer.BlockCopy(this.MessageBuffer.Array, this.MessageBuffer.Offset, ExtractedMessage, 0, MessageHeader.Count);

                this.HandleMessage(ExtractedMessage, MessageHeader.MessageType);
            }
        }

        private async void OnDisconnect(bool SendCloseConnectionSignal)
        {
            if (SendCloseConnectionSignal)
            {
                CancellationTokenSource CloseCancelSignal = new CancellationTokenSource(new TimeSpan(0, 0, 5));
                try
                {
                    Console.WriteLine($"Sending close signal to client");
                    await this.Stream.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye bye", CloseCancelSignal.Token);
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("Client did not respond to a connection close message in time");
                }
            }
            
            Console.WriteLine($"Client disconnected ({this.ID})");
            this.Alive = false;
        }

        private void OnConnectionClosed()
        {
            Console.WriteLine($"Client closed connection on their end");
            this.OnDisconnect(false);
        }

        private void OnMessageReceiveTimeout()
        {
            Console.WriteLine($"Client timed out after inactivity");
            this.OnDisconnect(true);
        }
    }
}