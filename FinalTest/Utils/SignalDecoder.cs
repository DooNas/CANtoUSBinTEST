using DbcParserLib.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalTest.Utils
{
    public static class SignalDecoder
    {
        public static double DecodeSignal(Signal signal, byte[] data)
        {
            bool isLittleEndian = signal.ByteOrder == 1;
            int rawValue = ExtractBits(data, signal.StartBit, signal.Length, isLittleEndian);
            return (rawValue * signal.Factor) + signal.Offset;
        }

        private static int ExtractBits(byte[] data, int startBit, int length, bool isLittleEndian)
        {
            int value = 0;
            int byteIndex = startBit / 8;
            int bitIndex = startBit % 8;

            for (int i = 0; i < length; i++)
            {
                if (byteIndex >= data.Length) break;

                int bitValue = (data[byteIndex] >> bitIndex) & 1;
                value |= (bitValue << i);
                bitIndex++;
                if (bitIndex == 8)
                {
                    byteIndex++;
                    bitIndex = 0;
                }
            }

            return value;
        }
    }
}
