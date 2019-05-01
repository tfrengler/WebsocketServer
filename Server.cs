using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WebsocketServer
{
    class WebsocketListener
    {
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _clientDataStream;
        private TimeSpan _handshakeTimeout = new TimeSpan(0, 0, 10);
        private TimeSpan _clientKeepAlive = new TimeSpan(0, 3, 0);
        private Stopwatch _clientHeartbeatCounter = new Stopwatch();
        private byte _sendMessageHeaderByte = 129;

        private Dictionary<uint, string> _optcodeLabels = new Dictionary<uint, string>
        {
            { 0x0, "0x0: Continuation frame" },
            { 0x1, "0x1: Text frame" },
            { 0x2, "0x2: Binary frame" },
            { 0x3, "0x3: Non-control frame" },
            { 0x4, "0x4: Non-control frame" },
            { 0x5, "0x5: Non-control frame" },
            { 0x6, "0x6: Non-control frame" },
            { 0x7, "0x7: Non-control frame" },
            { 0x8, "0x8: Connection closed" },
            { 0x9, "0x9: Ping" },
            { 0xA, "0xA: Pong" },
            { 0xB, "0xB: Control frame" },
            { 0xC, "0xC: Control frame" },
            { 0xD, "0xD: Control frame" },
            { 0xE, "0xE: Control frame" },
            { 0xF, "0xF: Control frame" }
        };

        private struct MessageHeader
        {
            public byte OPTCODE;
            public byte RSV1;
            public byte RSV2;
            public byte RSV3;
            public byte FINAL;
            public ulong PAYLOAD_SIZE;
        }

        /* CONSTRUCTOR */
        public WebsocketListener(string address, int port)
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
                Listen();
            }
            catch (SocketException error)
            {
                Console.WriteLine("WARNING: WebsocketListener was NOT able to start");
                Console.WriteLine(error.Message);
            }
        }

        private void Listen()
        {
            Console.WriteLine("Listening for connections" + Environment.NewLine);
            while (true)
            {
                TcpClient client = _listener.AcceptTcpClient();
                Console.WriteLine($"Client connected from {((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()}:{((IPEndPoint)client.Client.RemoteEndPoint).Port.ToString()}");
                _client = client;
                _clientDataStream = client.GetStream();
                doHandShake();
            }
        }

        private void handleClient()
        {
            Console.WriteLine("Ready to send and receive messages to/from client" + Environment.NewLine);
            _clientHeartbeatCounter.Start();

            while (clientStillAlive())
            {
                Thread.Sleep(100);
                if (!_clientDataStream.DataAvailable)
                    continue;

                Byte[] bytes = new Byte[_client.Available];
                _clientDataStream.Read(bytes, 0, bytes.Length);
                Console.WriteLine($"Message received from client ({bytes.Length} bytes)" + Environment.NewLine);

                string decodedMessage = unpackAndHandleMessage(bytes);
                Console.WriteLine("--------- MESSAGE ---------");
                Console.WriteLine(decodedMessage);
                Console.WriteLine("--------- END MESSAGE ---------" + Environment.NewLine);

                
            }

            Console.WriteLine($"WARNING: Client hasn't responded within the keep-alive timeout {_clientKeepAlive}");
            onDisconnectClient();
        }

        private void sendHeartbeat()
        {
            string heartbeatIdentifier = "<:HEARTBEAT:>";
            byte[] heartbeatPayload = Encoding.UTF8.GetBytes(heartbeatIdentifier);
            byte[] message = new byte[2] { _sendMessageHeaderByte, (byte)heartbeatIdentifier.Length };

            _clientDataStream.Write(message, 0, message.Length);
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

        private bool clientStillAlive()
        {
            if (_clientHeartbeatCounter.Elapsed > _clientKeepAlive)
                return false;

            return true;
        }

        private MessageHeader unpackHeader(byte headerByte)
        {
            return new MessageHeader()
            {
                OPTCODE = (byte)(headerByte & 0xF),
                RSV1 = (byte)(headerByte >> 4 & 1),
                RSV2 = (byte)(headerByte >> 5 & 1),
                RSV3 = (byte)(headerByte >> 6 & 1),
                FINAL = (byte)(headerByte >> 7 & 1)
            };
        }

        private string unpackAndHandleMessage(byte[] data)
        {
            sendHeartbeat();

            Console.WriteLine("Handling message");
            int maskingKeySize = 4;
            int maskingKeyStartIndex = 2;
            int payloadStartIndex = maskingKeyStartIndex + maskingKeySize;

            byte optcode = (byte)(data[0] & 0xF);
            byte rsv1 = (byte)(data[0] >> 4 & 1);
            byte rsv2 = (byte)(data[0] >> 5 & 1);
            byte rsv3 = (byte)(data[0] >> 6 & 1);
            byte fin = (byte)(data[0] >> 7 & 1);

            string optcodeLabel = "Unknown optcode: " + optcode.ToString("X4");
            _optcodeLabels.TryGetValue(optcode, out optcodeLabel);

            Console.WriteLine("--------- MESSAGE HEADER ---------");
            Console.WriteLine("OPTCODE: " + optcodeLabel);
            Console.WriteLine("RSV1: " + rsv1);
            Console.WriteLine("RSV2: " + rsv2);
            Console.WriteLine("RSV3: " + rsv3);
            Console.WriteLine("FIN: " + fin);

            if ( (byte)(data[1] & 128) == 1 )
            {
                Console.WriteLine("WARNING: Message is NOT masked, aborting");
                return "<:NOT MASKED:>";
            }

            UInt64 payloadSize = (UInt64)(data[1] - 128 > 0 ? data[1] - 128 : 0);

            if (payloadSize == 126)
            {
                if (BitConverter.IsLittleEndian)
                    payloadSize = BitConverter.ToUInt16(new byte[] { data[3], data[2] }, 0);
                else
                    payloadSize = BitConverter.ToUInt16(new byte[] { data[2], data[3] }, 0);

                maskingKeyStartIndex = maskingKeyStartIndex + 2;
                payloadStartIndex = maskingKeyStartIndex + maskingKeySize;
            }

            if (payloadSize == 127)
            {
                if (BitConverter.IsLittleEndian)
                {
                    payloadSize = BitConverter.ToUInt64(new byte[] {
                        data[9], data[8], data[7], data[6],
                        data[5], data[4], data[3], data[2]
                    }, 0);
                }
                else
                {
                    payloadSize = BitConverter.ToUInt64(new byte[] {
                        data[2], data[3], data[4], data[5],
                        data[6], data[7], data[8], data[9]
                    }, 0);
                } 

                maskingKeyStartIndex = maskingKeyStartIndex + 8;
                payloadStartIndex = maskingKeyStartIndex + maskingKeySize;
            }

            Console.WriteLine($"PAYLOAD SIZE: {payloadSize} bytes");
            Console.WriteLine("--------- END MESSAGE HEADER ---------" + Environment.NewLine);

            byte[] encodedPayload = new byte[payloadSize];
            byte[] maskingKey = new byte[maskingKeySize];

            Buffer.BlockCopy(data, maskingKeyStartIndex, maskingKey, 0, maskingKeySize);
            Buffer.BlockCopy(data, payloadStartIndex, encodedPayload, 0, (int)payloadSize);

            byte[] decodedPayload = decodePayload(maskingKey, encodedPayload);

            return Encoding.UTF8.GetString(decodedPayload);
        }

        private byte[] decodePayload(byte[] maskingKey, byte[] encodedPayload)
        {
            Byte[] decoded = new Byte[encodedPayload.Length];
            for (int i = 0; i < encodedPayload.Length; i++)
            {
                decoded[i] = (Byte)(encodedPayload[i] ^ maskingKey[i % 4]);
            }
            return decoded;
        }

        private void doHandShake()
        {
            Console.WriteLine("Waiting to perform handshake");
            var stopwatch = new Stopwatch();

            while(stopwatch.Elapsed < _handshakeTimeout)
            {
                while(!_clientDataStream.DataAvailable && _client.Available < 3) ;

                Byte[] bytes = new Byte[_client.Available];
                _clientDataStream.Read(bytes, 0, bytes.Length);
                String data = Encoding.UTF8.GetString(bytes);

                Console.WriteLine("POSSIBLE HANDSHAKE REQUEST RECEIVED ----------------------------:");
                Console.WriteLine(data);

                if (!new Regex("^GET").IsMatch(data))
                {
                    Console.WriteLine("WARNING: Request is not of type GET, closing");
                    onDisconnectClient();
                    return;
                }

                if(!new Regex("Connection: Upgrade").IsMatch(data) && !new Regex("Upgrade: websocket").IsMatch(data))
                {
                    Console.WriteLine("WARNING: Request is not a websocket request, closing");
                    onDisconnectClient();
                    return;
                }

                Match WebsocketKeySearch = new Regex("Sec-WebSocket-Key: (.*)").Match(data);
                if (!WebsocketKeySearch.Success)
                {
                    Console.WriteLine("WARNING: No websocket key is present in the request");
                    _client.Close();
                    Listen();
                    return;
                }

                Byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + Environment.NewLine
                    + "Connection: Upgrade" + Environment.NewLine
                    + "Upgrade: websocket" + Environment.NewLine
                    + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                        SHA1.Create().ComputeHash(
                            Encoding.UTF8.GetBytes(
                                WebsocketKeySearch.Groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                            )
                        )
                    ) + Environment.NewLine
                    + Environment.NewLine);

                Console.WriteLine("Client authenticated for websocket connection, sending handshake response");
                _clientDataStream.Write(response, 0, response.Length);
                handleClient();

                return;
            }

            Console.WriteLine("WARNING: Timed out waiting for handshake request");
            onDisconnectClient();
        }

        private void onDisconnectClient()
        {
            Console.WriteLine("Client disconnected on our end, closing connection");
            _client.Close();
            _clientHeartbeatCounter.Reset();
            Listen();
        }
    }
}
