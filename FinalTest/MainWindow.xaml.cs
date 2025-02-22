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
        // 원시 데이터와 처리된 데이터를 위한 큐
        private ConcurrentQueue<byte[]> rawQueue = new ConcurrentQueue<byte[]>(); // 시리얼 포트에서 읽은 원시 데이터 저장
        private ConcurrentQueue<(uint canId, byte[] payload)> processedQueue = new ConcurrentQueue<(uint, byte[])>(); // 파싱된 데이터 저장
        private Dictionary<uint, byte[]> lastProcessedData = new Dictionary<uint, byte[]>(); // 중복 데이터 체크용 (마지막 처리된 데이터 저장)
        private Stopwatch stopwatch = new Stopwatch();

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private DispatcherTimer uiUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadDbcFile();
            InitializeSerialPorts();
            InitializeGauges();
            stopwatch.Start();

            // 백그라운드에서 원시 데이터 처리 Task 실행 (CancellationToken을 통해 안전하게 종료 가능)
            Task.Run(() => ProcessRawDataQueueAsync(cancellationTokenSource.Token));

            // UI 업데이트를 위한 DispatcherTimer 설정 (100ms 간격)
            uiUpdateTimer = new DispatcherTimer();
            uiUpdateTimer.Interval = TimeSpan.FromMilliseconds(20);
            uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            uiUpdateTimer.Start();
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

        // 시리얼 포트에서 데이터가 수신되면 원시 데이터를 rawQueue에 저장합니다.
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = serialPort.BytesToRead;
            byte[] tempBuffer = new byte[bytesToRead];
            serialPort.Read(tempBuffer, 0, bytesToRead);

            rawQueue.Enqueue(tempBuffer);
        }

        // 백그라운드 Task: rawQueue에 저장된 원시 데이터를 누적하여 패킷 단위로 파싱한 후, processedQueue에 저장합니다.
        private async Task ProcessRawDataQueueAsync(CancellationToken token)
        {
            List<byte> accumulatedBuffer = new List<byte>();
            while (!token.IsCancellationRequested)
            {
                // rawQueue에 쌓인 모든 원시 데이터를 누적 버퍼에 추가
                while (rawQueue.TryDequeue(out var rawData))
                {
                    accumulatedBuffer.AddRange(rawData);
                }

                // 누적된 데이터가 충분하다면 패킷 단위로 처리
                while (accumulatedBuffer.Count >= 5)
                {
                    int i = 0;
                    bool processedPacket = false;
                    // 시작 바이트 0xAA를 찾습니다.
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
                        int packetLength = 1 + 1 + idLength + dlc + 1; // 시작 바이트, 컨트롤, ID, 데이터, 종료 바이트

                        if (i + packetLength > accumulatedBuffer.Count)
                            break; // 아직 전체 패킷 수신 전

                        if (accumulatedBuffer[i + packetLength - 1] != 0x55)
                        {
                            i++;
                            continue;
                        }

                        uint canId = isExtended
                            ? (uint)accumulatedBuffer[i + 2] | ((uint)accumulatedBuffer[i + 3] << 8) | ((uint)accumulatedBuffer[i + 4] << 16) | ((uint)accumulatedBuffer[i + 5] << 24)
                            : (uint)accumulatedBuffer[i + 2] | ((uint)accumulatedBuffer[i + 3] << 8);

                        byte[] payload = new byte[dlc];
                        accumulatedBuffer.CopyTo(i + 1 + 1 + idLength, payload, 0, dlc);

                        // 중복 데이터 검사 (같은 CAN ID와 페이로드라면 건너뜁니다)
                        if (lastProcessedData.TryGetValue(canId, out var lastPayload) && lastPayload.SequenceEqual(payload))
                        {
                            accumulatedBuffer.RemoveRange(0, i + packetLength);
                            processedPacket = true;
                            continue;
                        }
                        lastProcessedData[canId] = payload;

                        // 파싱된 데이터를 processedQueue에 저장합니다.
                        processedQueue.Enqueue((canId, payload));

                        // 처리한 패킷의 바이트를 누적 버퍼에서 제거합니다.
                        accumulatedBuffer.RemoveRange(0, i + packetLength);
                        processedPacket = true;
                        break;
                    }
                    if (!processedPacket)
                    {
                        // 처리 가능한 패킷이 없으면 루프 종료
                        break;
                    }
                }
                await Task.Delay(10, token);
            }
        }

        // UI 업데이트 타이머 이벤트: 주기적으로 processedQueue의 데이터를 UI에 반영합니다.
        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            ProcessUIUpdate();
        }

        // processedQueue에 있는 데이터를 순차적으로 UI에 업데이트합니다.
        private void ProcessUIUpdate()
        {
            while (processedQueue.TryDequeue(out var data))
            {
                DecodeAndUpdateUI(data.canId, data.payload);
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
            cancellationTokenSource.Cancel();
            uiUpdateTimer.Stop();
            base.OnClosing(e);
        }
    }
}