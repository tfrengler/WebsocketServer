using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebsocketServer
{
    static class WebsocketProtocol
    {
        public static readonly Dictionary<uint, string> _OPTCODE_LABELS = new Dictionary<uint, string>
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

        private static readonly Dictionary<string, byte> _OPT_CODES = new Dictionary<string, byte>
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

        private struct MessageHeader
        {
            public byte OPTCODE;
            public byte RSV1;
            public byte RSV2;
            public byte RSV3;
            public byte FINAL;
            public int PAYLOAD_SIZE;
        };

        private static MessageHeader UnpackHeader(byte[] HeaderBytes)
        {
            return new MessageHeader()
            {
                OPTCODE = (byte)(HeaderBytes[0] & 0xF),
                RSV1 = (byte)(HeaderBytes[0] >> 4 & 1),
                RSV2 = (byte)(HeaderBytes[0] >> 5 & 1),
                RSV3 = (byte)(HeaderBytes[0] >> 6 & 1),
                FINAL = (byte)(HeaderBytes[0] >> 7 & 1)
            };
        }

        private static int CalculatePayloadSize(byte[] HeaderBytes)
        {
            int PayloadSize = (HeaderBytes[1] - 128 > 0 ? HeaderBytes[1] - 128 : 0);

            if (PayloadSize == 126)
            {
                if (BitConverter.IsLittleEndian)
                    PayloadSize = BitConverter.ToUInt16(new byte[] { HeaderBytes[3], HeaderBytes[2] }, 0);
                else
                    PayloadSize = BitConverter.ToUInt16(new byte[] { HeaderBytes[2], HeaderBytes[3] }, 0);
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
            }

            return PayloadSize;
        }

    }
}
