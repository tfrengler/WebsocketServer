using System;
using System.Net.Sockets;

namespace WebsocketServer
{
    class WebsocketClient
    {
        // PROPERTIES
        private TcpClient Client;
        private NetworkStream Stream;

        // METHODS
        public WebsocketClient(TcpClient ClientInstance)
        {
            Client = ClientInstance;
            Stream = Client.GetStream();
        }

        public void SendMessage(byte Optcode, byte[] Payload)
        {
            byte[] header = WebsocketProtocol.ComposeHeader(Optcode, (ulong)Payload.Length);
            Stream.Write(header, 0, header.Length);
            Stream.Write(Payload, 0, Payload.Length);
        }

        private void HandleMessage(byte[] MessageData)
        {
            int maskingKeyStartIndex = WebsocketProtocol.MaskingKeyStartIndex;
            int payloadStartIndex = WebsocketProtocol.MaskingKeyStartIndex + WebsocketProtocol.MaskingKeySize;
            byte[] decodedPayload = new byte[0];

            WebsocketProtocol.MessageHeader header = WebsocketProtocol.GetUnpackedHeader(MessageData);

            string optcodeLabel = "Unknown optcode: " + header.OPTCODE.ToString("X4");
            WebsocketProtocol.OPTCODE_LABELS.TryGetValue(header.OPTCODE, out optcodeLabel);

            Console.WriteLine("--------- MESSAGE HEADER ---------");
            Console.WriteLine("OPTCODE: " + optcodeLabel);
            Console.WriteLine("RSV1: " + header.RSV1);
            Console.WriteLine("RSV2: " + header.RSV2);
            Console.WriteLine("RSV3: " + header.RSV2);
            Console.WriteLine("FIN: " + header.FINAL);

            if (header.MASKED != 1)
            {
                Console.WriteLine("WARNING: Message is NOT masked, aborting");
                return;
            }

            int payloadSize = WebsocketProtocol.GetPayloadSize(new byte[2] { MessageData[0], MessageData[1] });

            if (payloadSize == 0)
            {
                Console.WriteLine("Message has no payload data (or payload size is not set correctly)");
            }

            if (payloadSize == 126)
            {
                maskingKeyStartIndex = maskingKeyStartIndex + 2;
                payloadStartIndex = maskingKeyStartIndex + WebsocketProtocol.MaskingKeySize;
            }

            if (payloadSize == 127)
            {
                maskingKeyStartIndex = maskingKeyStartIndex + 8;
                payloadStartIndex = maskingKeyStartIndex + WebsocketProtocol.MaskingKeySize;
            }

            Console.WriteLine($"PAYLOAD SIZE: {payloadSize} bytes");
            Console.WriteLine("--------- END MESSAGE HEADER ---------" + Environment.NewLine);

            /* 
                Only 32bit payloads are supported due to Buffer.BlockCopy not accepting 64bit values.
                However I think it is highly unlikely we will never receive more than 2GB of data...
            */
            if (payloadSize > 0 && payloadSize < Int32.MaxValue)
            {
                Console.WriteLine("Decoding payload");

                byte[] encodedPayload = new byte[payloadSize];
                byte[] maskingKey = new byte[WebsocketProtocol.MaskingKeySize];

                Buffer.BlockCopy(MessageData, maskingKeyStartIndex, maskingKey, 0, WebsocketProtocol.MaskingKeySize);
                Buffer.BlockCopy(MessageData, payloadStartIndex, encodedPayload, 0, payloadSize);

                decodedPayload = WebsocketProtocol.DecodePayload(maskingKey, encodedPayload);
            }

            /* Delegate to other handlers depending on optcode */

            /* TODO(thomas): Client will typically echo whatever payload you send them back to you. Maybe we need to verify that? */
            if (header.OPTCODE == WebsocketProtocol.OPT_CODES["Pong"])
                //onPongReceived();
                Console.WriteLine("NOT IMPLEMENTED");

            if (header.OPTCODE == WebsocketProtocol.OPT_CODES["TextFrame"] && decodedPayload.Length > 0)
                //onTextFrame(decodedPayload);
                Console.WriteLine("NOT IMPLEMENTED");

            if (header.OPTCODE == WebsocketProtocol.OPT_CODES["BinaryFrame"] && decodedPayload.Length > 0)
                //onBinaryFrame(decodedPayload);
                Console.WriteLine("NOT IMPLEMENTED");

            if (header.OPTCODE == WebsocketProtocol.OPT_CODES["Ping"])
                //onPingReceived(decodedPayload);
                Console.WriteLine("NOT IMPLEMENTED");
        }
    }
}