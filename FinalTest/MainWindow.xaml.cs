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

namespace FinalTest
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private Dbc dbc;
        private ConcurrentQueue<(uint canId, byte[] payload)> receivedQueue = new ConcurrentQueue<(uint, byte[])>();
        private Dictionary<uint, byte[]> lastProcessedData = new Dictionary<uint, byte[]>(); // 마지막 처리된 데이터 저장
        private Stopwatch stopwatch = new Stopwatch();

        public MainWindow()
        {
            InitializeComponent();
            LoadDbcFile();
            InitializeSerialPorts();
            InitializeGauges();
            stopwatch.Start();
        }
        private void ProcessUIUpdate()
        {
            while (receivedQueue.TryDequeue(out var data))
            {
                DecodeAndUpdateUI(data.canId, data.payload);
            }
        }
        private void InitializeGauges()
        {
            // 엔진 온도 게이지 초기화
            Engine1TempGauge.SetValueRange(0, 4095);
            Engine1TempGauge.SetUnit("°C");

            Engine2TempGauge.SetValueRange(0, 4095);
            Engine2TempGauge.SetUnit("°C");

            // 연료량 게이지 초기화
            FuelLeftGauge.SetValueRange(0, 6553.5);
            FuelLeftGauge.SetUnit("kg");

            FuelRightGauge.SetValueRange(0, 6553.5);
            FuelRightGauge.SetUnit("kg");
        }

        private void LoadDbcFile()
        {
            try
            {
                dbc = Parser.ParseFromPath("E:/GitHub/Remote/CANtoUSBinTEST/FinalTest/vcan.dbc");
                StatusText.Text = "DBC 파일 로드 완료";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DBC 파일 로드 실패: {ex.Message}");
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
                PortComboBox.SelectedIndex = 0;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
                StatusText.Text = "연결 해제됨";
                return;
            }

            try
            {
                serialPort = new SerialPort(PortComboBox.SelectedItem.ToString(), 2000000, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();
                StatusText.Text = "연결됨";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"연결 실패: {ex.Message}");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = serialPort.BytesToRead;
            byte[] tempBuffer = new byte[bytesToRead];
            serialPort.Read(tempBuffer, 0, bytesToRead);

            Task.Run(() => ProcessReceivedData(tempBuffer)); // 백그라운드 처리
        }

        private void ProcessReceivedData(byte[] tempBuffer)
        {
            List<byte> receivedData = new List<byte>(tempBuffer);

            while (receivedData.Count >= 5)
            {
                int i = 0;
                while (i < receivedData.Count - 5)
                {
                    if (receivedData[i] != 0xAA)
                    {
                        i++;
                        continue;
                    }

                    byte control = receivedData[i + 1];
                    int dlc = control & 0x0F;
                    bool isExtended = ((control >> 5) & 0x01) == 1;
                    int idLength = isExtended ? 4 : 2;
                    int packetLength = 1 + 1 + idLength + dlc + 1;

                    if (i + packetLength > receivedData.Count)
                        break;

                    if (receivedData[i + packetLength - 1] != 0x55)
                    {
                        i++;
                        continue;
                    }

                    uint canId = isExtended
                        ? (uint)receivedData[i + 2] | ((uint)receivedData[i + 3] << 8) | ((uint)receivedData[i + 4] << 16) | ((uint)receivedData[i + 5] << 24)
                        : (uint)receivedData[i + 2] | ((uint)receivedData[i + 3] << 8);

                    byte[] payload = new byte[dlc];
                    Array.Copy(receivedData.ToArray(), i + 1 + 1 + idLength, payload, 0, dlc);

                    // 같은 데이터는 무시하여 불필요한 UI 업데이트 방지
                    if (lastProcessedData.TryGetValue(canId, out var lastPayload) && lastPayload.SequenceEqual(payload))
                    {
                        receivedData.RemoveRange(0, i + packetLength);
                        continue;
                    }

                    lastProcessedData[canId] = payload;
                    receivedQueue.Enqueue((canId, payload));

                    receivedData.RemoveRange(0, i + packetLength);

                    // UI 업데이트 실행 (즉시 반영)
                    Dispatcher.BeginInvoke(new Action(() => ProcessUIUpdate()));
                    break;
                }
            }
        }

        private void DecodeAndUpdateUI(uint canId, byte[] payload)
        {
            var message = dbc.Messages.FirstOrDefault(m => m.ID == canId);
            if (message == null) return;

            switch (message.Name)
            {
                case "FLIGHT_STATUS":
                    UpdateFlightStatus(message, payload);
                    break;
                case "ENGINE_DATA":
                    UpdateEngineData(message, payload);
                    break;
                case "NAV_DATA":
                    UpdateNavData(message, payload);
                    break;
                case "FUEL_SYSTEM":
                    UpdateFuelSystem(message, payload);
                    break;
                case "ENV_DATA":
                    UpdateEnvironmentData(message, payload);
                    break;
            }
        }

        private double DecodeSignal(Signal signal, byte[] data)
        {
            bool isLittleEndian = signal.ByteOrder == 1;
            int rawValue = ExtractBits(data, signal.StartBit, signal.Length, isLittleEndian);
            double physicalValue = (rawValue * signal.Factor) + signal.Offset;
            return physicalValue;
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

        private void UpdateFlightStatus(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = DecodeSignal(signal, payload);
                switch (signal.Name)
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
                        FlapsText.Text = $"Flaps: {value}°";
                        break;
                    case "aircraft_altitude":
                        AltitudeText.Text = $"Altitude: {value:F0} ft";
                        break;
                    case "vertical_speed":
                        VerticalSpeedText.Text = $"Vertical Speed: {value:F0} ft/min";
                        break;
                }
            }
        }

        private void UpdateEngineData(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = DecodeSignal(signal, payload);
                switch (signal.Name)
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
        }

        private void UpdateNavData(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = DecodeSignal(signal, payload);
                switch (signal.Name)
                {
                    case "latitude":
                        LatitudeText.Text = $"Latitude: {value:F6}°";
                        break;
                    case "longitude":
                        LongitudeText.Text = $"Longitude: {value:F6}°";
                        break;
                }
            }
        }

        private void UpdateFuelSystem(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = DecodeSignal(signal, payload);
                switch (signal.Name)
                {
                    case "fuel_qty_left":
                        FuelLeftGauge.SetValue(value);
                        break;
                    case "fuel_qty_right":
                        FuelRightGauge.SetValue(value);
                        break;
                }
            }
        }

        private void UpdateEnvironmentData(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = DecodeSignal(signal, payload);
                switch (signal.Name)
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
        }

        private string GetFlightModeName(int mode)
        {
            switch (mode)
            {
                case 0: return "GROUND";
                case 1: return "TAKEOFF";
                case 2: return "CLIMB";
                case 3: return "CRUISE";
                case 4: return "DESCENT";
                case 5: return "APPROACH";
                case 6: return "LANDING";
                default: return "UNKNOWN";
            }
        }

        private string GetLandingGearStatus(int status)
        {
            switch (status)
            {
                case 0: return "UP";
                case 1: return "DOWN";
                case 2: return "MOVING";
                case 3: return "FAULT";
                default: return "UNKNOWN";
            }
        }

        private string GetEngineStatus(int status)
        {
            switch (status)
            {
                case 0: return "OFF";
                case 1: return "STARTING";
                case 2: return "RUNNING";
                case 3: return "SHUTTING_DOWN";
                case 4: return "FAULT";
                default: return "UNKNOWN";
            }
        }

        private string GetTurbulenceLevel(int level)
        {
            switch (level)
            {
                case 0: return "NONE";
                case 1: return "LIGHT";
                case 2: return "MODERATE";
                case 3: return "SEVERE";
                case 4: return "EXTREME";
                default: return "UNKNOWN";
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
            }
            base.OnClosing(e);
        }
    }
}