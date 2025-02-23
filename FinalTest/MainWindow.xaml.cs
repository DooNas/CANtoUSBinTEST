using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;
using DbcParserLib;
using DbcParserLib.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace FinalTest
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private Dbc dbc;
        private ConcurrentQueue<byte[]> rawQueue = new ConcurrentQueue<byte[]>();
        private Dictionary<uint, byte[]> lastProcessedData = new Dictionary<uint, byte[]>();
        private CancellationTokenSource cancellationTokenSource;
        private DispatcherTimer uiUpdateTimer;
        private bool isClosing = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            InitializeSerialPorts();
            InitializeGauges();

            cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ProcessDataAsync(cancellationTokenSource.Token));
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            isClosing = true;
            cancellationTokenSource?.Cancel();

            if (serialPort?.IsOpen == true)
            {
                serialPort.Close();
                serialPort.Dispose();
            }

            base.OnClosing(e);
        }

        private void LoadDbcButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".dbc",
                Filter = "DBC Files (*.dbc)|*.dbc|All Files (*.*)|*.*",
                Title = "DBC 파일 탐색기"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    dbc = Parser.ParseFromPath(openFileDialog.FileName);
                    StatusText.Text = "DBC 파일 로드 성공";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load DBC file: {ex.Message}");
                    StatusText.Text = "DBC 파일 로드 실패";
                }
            }
        }

        private void InitializeSerialPorts()
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                PortComboBox.Items.Add(port);
            }
            if (ports.Length > 0)
            {
                PortComboBox.SelectedIndex = 0;
            }
        }

        private void InitializeGauges()
        {
            // Engine Temperature Gauges
            Engine1TempGauge.SetValueRange(0, 4095);
            Engine1TempGauge.SetUnit("°C");
            Engine2TempGauge.SetValueRange(0, 4095);
            Engine2TempGauge.SetUnit("°C");

            // Fuel Quantity Gauges
            FuelLeftGauge.SetValueRange(0, 6553.5);
            FuelLeftGauge.SetUnit("kg");
            FuelRightGauge.SetValueRange(0, 6553.5);
            FuelRightGauge.SetUnit("kg");
        }


        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort?.IsOpen == true)
            {
                await DisconnectSerialPort();
            }
            else
            {
                await ConnectSerialPort();
            }
        }

        private async Task ConnectSerialPort()
        {
            try
            {
                string selectedPort = PortComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedPort))
                {
                    MessageBox.Show("Please select a COM port");
                    return;
                }

                serialPort = new SerialPort(selectedPort, 2000000, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                StatusText.Text = "Connected";
                ConnectButton.Content = "Disconnect";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}");
                StatusText.Text = "Connection failed";
            }
        }

        private async Task DisconnectSerialPort()
        {
            try
            {
                if (serialPort?.IsOpen == true)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                }

                StatusText.Text = "Disconnected";
                ConnectButton.Content = "Connect";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Disconnection error: {ex.Message}");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serialPort?.IsOpen != true || isClosing) return;

            try
            {
                int bytesToRead = serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                serialPort.Read(buffer, 0, bytesToRead);
                rawQueue.Enqueue(buffer);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Read Error: {ex.Message}";
                });
            }
        }

        private async Task ProcessDataAsync(CancellationToken token)
        {
            List<byte> accumulatedBuffer = new List<byte>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    while (rawQueue.TryDequeue(out byte[] data))
                    {
                        accumulatedBuffer.AddRange(data);
                    }

                    ProcessAccumulatedData(accumulatedBuffer);
                    await Task.Delay(10, token);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"Processing Error: {ex.Message}";
                    });
                    await Task.Delay(1000, token);
                }
            }
        }

        private void ProcessAccumulatedData(List<byte> buffer)
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

                    if (ProcessPacket(buffer, i, out int packetLength))
                    {
                        buffer.RemoveRange(0, i + packetLength);
                        processedPacket = true;
                        break;
                    }
                    i++;
                }

                if (!processedPacket) break;
            }
        }

        private bool ProcessPacket(List<byte> buffer, int startIndex, out int packetLength)
        {
            packetLength = 0;
            byte control = buffer[startIndex + 1];
            int dlc = control & 0x0F;
            bool isExtended = ((control >> 5) & 0x01) == 1;
            int idLength = isExtended ? 4 : 2;
            packetLength = 1 + 1 + idLength + dlc + 1;

            if (startIndex + packetLength > buffer.Count) return false;
            if (buffer[startIndex + packetLength - 1] != 0x55) return false;

            uint canId = isExtended
                ? (uint)buffer[startIndex + 2] | ((uint)buffer[startIndex + 3] << 8) |
                  ((uint)buffer[startIndex + 4] << 16) | ((uint)buffer[startIndex + 5] << 24)
                : (uint)buffer[startIndex + 2] | ((uint)buffer[startIndex + 3] << 8);

            byte[] payload = new byte[dlc];
            buffer.CopyTo(startIndex + 1 + 1 + idLength, payload, 0, dlc);

            if (!lastProcessedData.TryGetValue(canId, out byte[] lastPayload) ||
                !payload.SequenceEqual(lastPayload))
            {
                lastProcessedData[canId] = payload.ToArray();
                ProcessDecodedData(canId, payload);
            }

            return true;
        }

        private void ProcessDecodedData(uint canId, byte[] payload)
        {
            var message = dbc.Messages.FirstOrDefault(m => m.ID == canId);
            if (message == null) return;

            Dispatcher.Invoke(() =>
            {
                foreach (var signal in message.Signals)
                {
                    double value = DecodeSignal(signal, payload);
                    UpdateUIElement(message.Name, signal.Name, value);
                }
            });
        }

        private double DecodeSignal(Signal signal, byte[] data)
        {
            bool isLittleEndian = signal.ByteOrder == 1;
            int rawValue = ExtractBits(data, signal.StartBit, signal.Length, isLittleEndian);
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

        private void UpdateUIElement(string messageName, string signalName, double value)
        {
            switch (messageName)
            {
                case "FLIGHT_STATUS":
                    UpdateFlightStatusUI(signalName, value);
                    break;
                case "ENGINE_DATA":
                    UpdateEngineDataUI(signalName, value);
                    break;
                case "FUEL_SYSTEM":
                    UpdateFuelSystemUI(signalName, value);
                    break;
                case "NAV_DATA": 
                    UpdateNavigationUI(signalName, value);
                    break;
                case "ENV_DATA":
                    UpdateEnvironmentDataUI(signalName, value);
                    break;
            }
        }

        private void UpdateFlightStatusUI(string signalName, double value)
        {
            switch (signalName)
            {
                case "flight_mode":
                    FlightModeText.Text = $"Flight Mode: {GetFlightModeName((int)value)}";
                    break;
                case "autopilot_engaged":
                    AutopilotText.Text = $"Autopilot: {(value == 1 ? "Engaged" : "Disengaged")}";
                    break;
                case "landing_gear_status":
                    LandingGearText.Text = $"Landing Gear: {GetLandingGearStatus((int)value)}";
                    break;
                case "flaps_position":
                    FlapsText.Text = $"Flaps: {value:F1}°";
                    break;
                case "aircraft_altitude":
                    AltitudeText.Text = $"Altitude: {value:F0} ft";
                    break;
                case "vertical_speed":
                    VerticalSpeedText.Text = $"Vertical Speed: {value:F0} ft/min";
                    break;
            }
        }

        private void UpdateEngineDataUI(string signalName, double value)
        {
            switch (signalName)
            {
                case "engine1_thrust":
                    Engine1ThrustText.Text = $"Engine 1 Thrust: {value:F1} kN";
                    break;
                case "engine2_thrust":
                    Engine2ThrustText.Text = $"Engine 2 Thrust: {value:F1} kN";
                    break;
                case "engine1_temp":
                    Engine1TempGauge.SetValue(value);
                    break;
                case "engine2_temp":
                    Engine2TempGauge.SetValue(value);
                    break;
                case "engine1_status":
                    Engine1StatusText.Text = $"Engine 1 Status: {GetEngineStatus((int)value)}";
                    break;
                case "engine2_status":
                    Engine2StatusText.Text = $"Engine 2 Status: {GetEngineStatus((int)value)}";
                    break;
            }
        }

        private void UpdateFuelSystemUI(string signalName, double value)
        {
            switch (signalName)
            {
                case "fuel_qty_left":
                    FuelLeftGauge.SetValue(value);
                    break;
                case "fuel_qty_right":
                    FuelRightGauge.SetValue(value);
                    break;
            }
        }

        private void UpdateNavigationUI(string signalName, double value)
        {
            switch (signalName)
            {
                case "latitude":
                    LatitudeText.Text = $"Latitude: {value:F6}°";
                    break;
                case "longitude":
                    LongitudeText.Text = $"Longitude: {value:F6}°";
                    break;
            }
        }

        private void UpdateEnvironmentDataUI(string signalName, double value)
        {
            switch (signalName)
            {
                case "outside_air_temp":
                    OutsideAirTempText.Text = $"Outside Air Temperature: {value:F1}°C";
                    break;
                case "air_pressure":
                    AirPressureText.Text = $"Air Pressure: {value:F1} kPa";
                    break;
                case "wind_speed":
                    WindSpeedText.Text = $"Wind Speed: {value:F1} kt";
                    break;
                case "wind_direction":
                    WindDirectionText.Text = $"Wind Direction: {value:F0}°";
                    break;
                case "turbulence_level":
                    TurbulenceLevelText.Text = $"Turbulence Level: {GetTurbulenceLevel((int)value)}";
                    break;
            }
        }

        private string GetFlightModeName(int mode) => mode switch
        {
            0 => "GROUND",
            1 => "TAKEOFF",
            2 => "CLIMB",
            3 => "CRUISE",
            4 => "DESCENT",
            5 => "APPROACH",
            6 => "LANDING",
            _ => "UNKNOWN"
        };

        private string GetLandingGearStatus(int status) => status switch
        {
            0 => "UP",
            1 => "DOWN",
            2 => "MOVING",
            3 => "FAULT",
            _ => "UNKNOWN"
        };

        private string GetEngineStatus(int status) => status switch
        {
            0 => "OFF",
            1 => "STARTING",
            2 => "RUNNING",
            3 => "SHUTTING_DOWN",
            4 => "FAULT",
            _ => "UNKNOWN"
        };

        private string GetTurbulenceLevel(int level) => level switch
        {
            0 => "NONE",
            1 => "LIGHT",
            2 => "MODERATE",
            3 => "SEVERE",
            4 => "EXTREME",
            _ => "UNKNOWN"
        };
    }
}