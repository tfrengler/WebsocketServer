using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WebsocketServer
{
    static class WebsocketProtocol
    {
        // PROPERTIES
        private static Encoding TextEncoding { get; } = Encoding.UTF8;

        public static string HandshakeKey { get; } = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static int MaskingKeySize { get; } = 4;

        public static int MaskingKeyStartIndex { get; } = 2;

        public static Dictionary<uint, string> OPTCODE_LABELS { get; } = new Dictionary<uint, string>
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

        public static Dictionary<string, byte> OPT_CODES { get; } = new Dictionary<string, byte>
        {
            { "ContinuationFrame", 0 },
            { "TextFrame", 1 },
            { "BinaryFrame", 2 },
            { "NonControlFrame1", 3 },
            { "NonControlFrame2", 4 },
            { "NonControlFrame3", 5 },
            { "NonControlFrame4", 6 },
            { "NonControlFrame5", 7 },
            { "ConnectionClosed", 8 },
            { "Ping", 9 },
            { "Pong", 10 },
            { "ControlFrame1", 11 },
            { "ControlFrame2", 12 },
            { "ControlFrame3", 13 },
            { "ControlFrame4", 14 },
            { "ControlFrame5", 15 }
        };

        public struct MessageHeader
        {
            public byte OPTCODE;
            public byte MASKED;
            public byte RSV1;
            public byte RSV2;
            public byte RSV3;
            public byte FINAL;
        };

        // METHODS
        public static MessageHeader GetUnpackedHeader(byte[] HeaderBytes)
        {
            return new MessageHeader()
            {
                OPTCODE = (byte)(HeaderBytes[0] & 0xF),
                MASKED = (byte)(HeaderBytes[1] & 128),
                RSV1 = (byte)(HeaderBytes[0] >> 4 & 1),
                RSV2 = (byte)(HeaderBytes[0] >> 5 & 1),
                RSV3 = (byte)(HeaderBytes[0] >> 6 & 1),
                FINAL = (byte)(HeaderBytes[0] >> 7 & 1)
            };
        }

        public static int GetPayloadSize(byte[] HeaderBytes)
        {
            int PayloadSize = (HeaderBytes[1] - 128 > 0 ? HeaderBytes[1] - 128 : 0);
            if (PayloadSize < 126) return PayloadSize;

            if (PayloadSize == 126)
            {
                if (BitConverter.IsLittleEndian)
                    PayloadSize = BitConverter.ToUInt16(new byte[] { HeaderBytes[3], HeaderBytes[2] }, 0);
                else
                    PayloadSize = BitConverter.ToUInt16(new byte[] { HeaderBytes[2], HeaderBytes[3] }, 0);

                return PayloadSize;
            }

            if (PayloadSize == 127)
            {
                if (BitConverter.IsLittleEndian)
                {
                    PayloadSize = BitConverter.ToInt32(new byte[] {
                        HeaderBytes[9], HeaderBytes[8], HeaderBytes[7], HeaderBytes[6],
                        HeaderBytes[5], HeaderBytes[4], HeaderBytes[3], HeaderBytes[2]
                    }, 0);
                }
                else
                {
                    PayloadSize = BitConverter.ToInt32(new byte[] {
                        HeaderBytes[2], HeaderBytes[3], HeaderBytes[4], HeaderBytes[5],
                        HeaderBytes[6], HeaderBytes[7], HeaderBytes[8], HeaderBytes[9]
                    }, 0);
                }

                return PayloadSize;
            }

            return -1;
        }

        public static string PerformHandshake(byte[] HttpRequest)
        {
            if (HttpRequest.Length != 3)
                throw new ArgumentException($"Argument 'Request' should be 3 bytes ({HttpRequest.Length})");

            string requestDataAsString = TextEncoding.GetString(HttpRequest);

            if (!new Regex("^GET").IsMatch(requestDataAsString))
            {
                Console.WriteLine("WARNING: Request is not of type GET, closing");
                return "";
            }

            if (!new Regex("Connection: Upgrade").IsMatch(requestDataAsString) && !new Regex("Upgrade: websocket").IsMatch(requestDataAsString))
            {
                Console.WriteLine("WARNING: Request is not a websocket request, closing");
                return "";
            }

            Match WebsocketKeySearch = new Regex("Sec-WebSocket-Key: (.*)").Match(requestDataAsString);
            if (!WebsocketKeySearch.Success)
            {
                Console.WriteLine("WARNING: No websocket key is present in the request");
                return "";
            }

            Console.WriteLine("Client authenticated for websocket connection");
            return WebsocketKeySearch.Groups[1].Value.Trim();
        }

        public static byte[] GetProtocolUpgradeResponse(string ClientWebsocketKey)
        {
            return TextEncoding.GetBytes("HTTP/1.1 101 Switching Protocols" + Environment.NewLine
                + "Connection: Upgrade" + Environment.NewLine
                + "Upgrade: websocket" + Environment.NewLine
                + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                    SHA1.Create().ComputeHash(
                        TextEncoding.GetBytes(
                            ClientWebsocketKey + HandshakeKey
                        )
                    )
                ) + Environment.NewLine
                + Environment.NewLine);
        }

        public static byte[] DecodePayload(byte[] MaskingKey, byte[] EncodedPayload)
        {
            byte[] decoded = new byte[EncodedPayload.Length];
            for (int i = 0; i < EncodedPayload.Length; i++)
            {
                decoded[i] = (byte)(EncodedPayload[i] ^ MaskingKey[i % 4]);
            }
            return decoded;
        }

        public static byte[] ComposeHeader(byte Optcode, ulong PayloadSize)
        {
            byte[] returnData;

            if (PayloadSize > 125 && PayloadSize <= UInt16.MaxValue)
            {
                returnData = new byte[4];
                returnData[1] = 126;
                byte[] payloadSizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)PayloadSize));
                Buffer.BlockCopy(payloadSizeBytes, 0, returnData, 2, payloadSizeBytes.Length);
            }
            else if (PayloadSize > UInt16.MaxValue)
            {
                returnData = new byte[8];
                returnData[1] = 127;
                byte[] payloadSizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)PayloadSize));
                Buffer.BlockCopy(payloadSizeBytes, 0, returnData, 2, payloadSizeBytes.Length);
            }
            else
            {
                returnData = new byte[2];
                returnData[1] = (byte)PayloadSize;
            }

            returnData[0] = (byte)(128 | Optcode);
            return returnData;
        }
    }
}
