using DbcParserLib.Model;
using DbcParserLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalTest.Services
{
    public class DbcService
    {
        private readonly Dbc _dbc;

        public DbcService(string dbcPath)
        {
            _dbc = Parser.ParseFromPath(dbcPath);
        }

        public Message GetMessage(uint canId)
        {
            return _dbc.Messages.FirstOrDefault(m => m.ID == canId);
        }

        public double DecodeSignal(Signal signal, byte[] data)
        {
            int rawValue = ExtractBits(data, signal.StartBit, signal.Length, signal.ByteOrder == 1);
            return (rawValue * signal.Factor) + signal.Offset;
        }

        private int ExtractBits(byte[] data, int startBit, int length, bool isLittleEndian)
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
