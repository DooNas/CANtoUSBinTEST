using FinalTest.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalTest.Services
{
    public class CanDataService : ICanDataService
    {
        private SerialPort serialPort;
        private ConcurrentQueue<byte[]> rawQueue = new ConcurrentQueue<byte[]>();
        private ConcurrentQueue<CanData> processedQueue = new ConcurrentQueue<CanData>();
        private Dictionary<uint, byte[]> lastProcessedData = new Dictionary<uint, byte[]>();
        private CancellationTokenSource cancellationTokenSource;
        private Task processingTask;

        public event EventHandler<CanData> DataReceived;

        public bool Connect(string portName)
        {
            try
            {
                serialPort = new SerialPort(portName, 2000000, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Disconnect()
        {
            if (serialPort?.IsOpen == true)
            {
                serialPort.Close();
            }
        }

        public async Task StartAsync()
        {
            cancellationTokenSource = new CancellationTokenSource();
            processingTask = ProcessRawDataQueueAsync(cancellationTokenSource.Token);
        }

        public async Task StopAsync()
        {
            cancellationTokenSource?.Cancel();
            if (processingTask != null)
            {
                await processingTask;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = serialPort.BytesToRead;
            byte[] tempBuffer = new byte[bytesToRead];
            serialPort.Read(tempBuffer, 0, bytesToRead);
            rawQueue.Enqueue(tempBuffer);
        }

        private async Task ProcessRawDataQueueAsync(CancellationToken token)
        {
            List<byte> accumulatedBuffer = new List<byte>();
            while (!token.IsCancellationRequested)
            {
                while (rawQueue.TryDequeue(out var rawData))
                {
                    accumulatedBuffer.AddRange(rawData);
                }

                while (accumulatedBuffer.Count >= 5)
                {
                    int i = 0;
                    bool processedPacket = false;

                    while (i < accumulatedBuffer.Count - 5)
                    {
                        if (accumulatedBuffer[i] != 0xAA)
                        {
                            i++;
                            continue;
                        }

                        byte control = accumulatedBuffer[i + 1];
                        int dlc = control & 0x0F;
                        bool isExtended = ((control >> 5) & 0x01) == 1;
                        int idLength = isExtended ? 4 : 2;
                        int packetLength = 1 + 1 + idLength + dlc + 1;

                        if (i + packetLength > accumulatedBuffer.Count)
                            break;

                        if (accumulatedBuffer[i + packetLength - 1] != 0x55)
                        {
                            i++;
                            continue;
                        }

                        uint canId = isExtended
                            ? (uint)accumulatedBuffer[i + 2] | ((uint)accumulatedBuffer[i + 3] << 8) |
                              ((uint)accumulatedBuffer[i + 4] << 16) | ((uint)accumulatedBuffer[i + 5] << 24)
                            : (uint)accumulatedBuffer[i + 2] | ((uint)accumulatedBuffer[i + 3] << 8);

                        byte[] payload = new byte[dlc];
                        Array.Copy(accumulatedBuffer.ToArray(), i + 1 + 1 + idLength, payload, 0, dlc);

                        if (!ShouldProcessData(canId, payload))
                        {
                            accumulatedBuffer.RemoveRange(0, i + packetLength);
                            processedPacket = true;
                            continue;
                        }

                        DataReceived?.Invoke(this, new CanData { CanId = canId, Payload = payload });
                        accumulatedBuffer.RemoveRange(0, i + packetLength);
                        processedPacket = true;
                        break;
                    }

                    if (!processedPacket)
                        break;
                }

                await Task.Delay(10, token);
            }
        }

        private bool ShouldProcessData(uint canId, byte[] payload)
        {
            if (lastProcessedData.TryGetValue(canId, out var lastPayload) &&
                payload.SequenceEqual(lastPayload))
            {
                return false;
            }

            lastProcessedData[canId] = payload.ToArray();
            return true;
        }
    }
}
