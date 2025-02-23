using System;
using System.IO.Ports;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace FinalTest.Services
{
    public class SerialService : IDisposable
    {
        private SerialPort _serialPort;
        private readonly ConcurrentQueue<byte[]> _rawQueue = new ConcurrentQueue<byte[]>();
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _processTask;
        private readonly Dictionary<uint, byte[]> _lastProcessedData = new Dictionary<uint, byte[]>();

        public event EventHandler<(uint canId, byte[] payload)> DataReceived;
        public event EventHandler<string> ConnectionStatusChanged;

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public SerialService()
        {
            _serialPort = new SerialPort
            {
                BaudRate = 2000000,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One
            };
        }

        public void Connect(string portName)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }

                _serialPort.PortName = portName;
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                _cts = new CancellationTokenSource();
                _processTask = ProcessDataAsync(_cts.Token);

                ConnectionStatusChanged?.Invoke(this, "Connected");
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }

        public void Disconnect()
        {
            _cts.Cancel();
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
            ConnectionStatusChanged?.Invoke(this, "Disconnected");
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_serialPort.IsOpen) return;

            int bytesToRead = _serialPort.BytesToRead;
            byte[] buffer = new byte[bytesToRead];
            _serialPort.Read(buffer, 0, bytesToRead);
            _rawQueue.Enqueue(buffer);
        }

        private async Task ProcessDataAsync(CancellationToken token)
        {
            List<byte> buffer = new List<byte>();

            while (!token.IsCancellationRequested)
            {
                while (_rawQueue.TryDequeue(out byte[] data))
                {
                    buffer.AddRange(data);
                }

                ProcessBuffer(buffer);
                await Task.Delay(10, token);
            }
        }

        private void ProcessBuffer(List<byte> buffer)
        {
            while (buffer.Count >= 5)
            {
                int i = 0;
                bool processedPacket = false;

                while (i < buffer.Count - 5)
                {
                    if (buffer[i] != 0xAA)
                    {
                        i++;
                        continue;
                    }

                    byte control = buffer[i + 1];
                    int dlc = control & 0x0F;
                    bool isExtended = ((control >> 5) & 0x01) == 1;
                    int idLength = isExtended ? 4 : 2;
                    int packetLength = 1 + 1 + idLength + dlc + 1;

                    if (i + packetLength > buffer.Count)
                        break;

                    if (buffer[i + packetLength - 1] != 0x55)
                    {
                        i++;
                        continue;
                    }

                    uint canId = isExtended
                        ? (uint)buffer[i + 2] | ((uint)buffer[i + 3] << 8) | ((uint)buffer[i + 4] << 16) | ((uint)buffer[i + 5] << 24)
                        : (uint)buffer[i + 2] | ((uint)buffer[i + 3] << 8);

                    byte[] payload = new byte[dlc];
                    buffer.CopyTo(i + 1 + 1 + idLength, payload, 0, dlc);

                    if (!_lastProcessedData.TryGetValue(canId, out byte[] lastPayload) ||
                        !payload.SequenceEqual(lastPayload))
                    {
                        _lastProcessedData[canId] = payload.ToArray();
                        DataReceived?.Invoke(this, (canId, payload));
                    }

                    buffer.RemoveRange(0, i + packetLength);
                    processedPacket = true;
                    break;
                }

                if (!processedPacket)
                    break;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _serialPort?.Dispose();
        }

        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }
    }
}