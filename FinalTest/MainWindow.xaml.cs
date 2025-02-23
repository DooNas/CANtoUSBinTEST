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
        // CAN 데이터 수신을 위한 시리얼 포트
        private SerialPort serialPort;

        // DBC 파일에서 로드된 CAN 메시지 정의
        private Dbc dbc;

        // 원시 데이터 버퍼 큐 - 스레드 안전한 ConcurrentQueue 사용
        private ConcurrentQueue<byte[]> rawQueue = new ConcurrentQueue<byte[]>();

        // 중복 데이터 필터링을 위한 마지막 처리된 데이터 저장
        private Dictionary<uint, byte[]> lastProcessedData = new Dictionary<uint, byte[]>();

        // 비동기 작업 취소를 위한 토큰
        private CancellationTokenSource cancellationTokenSource;

        // 애플리케이션 종료 상태 플래그
        private bool isClosing = false;

        // 버퍼 모니터링 컴포넌트
        private BufferMonitor bufferMonitor;
        public BufferMonitor BufferMonitor => bufferMonitor;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        /// <summary>
        /// 시리얼 포트 초기화 메서드
        /// 시스템에서 사용 가능한 모든 COM 포트를 검색하여 콤보박스에 추가
        /// </summary>
        private void InitializeApplication()
        {
            InitializeSerialPorts();
            InitializeGauges();

            cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => ProcessDataAsync(cancellationTokenSource.Token));

            // 밀린 버퍼량 체크
            bufferMonitor = new BufferMonitor(rawQueue);
            DataContext = this;
            bufferMonitor.Start();
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            isClosing = true;
            bufferMonitor?.Stop();
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
            int initialSize = buffer.Count;

            while (buffer.Count >= 5)
            {
                int i = 0;
                bool processedPacket = false;

                while (i < buffer.Count - 5)
                {
                    if (buffer[i] != 0xAA)
                    {
                        bufferMonitor.IncrementDroppedCount();
                        i++;
                        continue;
                    }

                    if (ProcessPacket(buffer, i, out int packetLength))
                    {
                        buffer.RemoveRange(0, i + packetLength);
                        bufferMonitor.IncrementProcessedCount();
                        processedPacket = true;
                        break;
                    }
                    bufferMonitor.IncrementDroppedCount();
                    i++;
                }

                if (!processedPacket) break;
            }
        }
        /// <summary>
        /// [CAN 데이터 패킷 처리 메서드]
        /// </summary>
        /// <param name="buffer">원시 데이터 버퍼</param>
        /// <param name="startIndex">패킷 시작 인덱스</param>
        /// <param name="packetLength">처리된 패킷의 길이</param>
        /// <returns>패킷 처리 성공 여부</returns>
        private bool ProcessPacket(List<byte> buffer, int startIndex, out int packetLength)
        {
            packetLength = 0;
            byte control = buffer[startIndex + 1];
            int dlc = control & 0x0F;
            bool isExtended = ((control >> 5) & 0x01) == 1;
            int idLength = isExtended ? 4 : 2;
            packetLength = 1 + 1 + idLength + dlc + 1;

            // 패킷 길이 확인
            if (startIndex + packetLength > buffer.Count) return false;
            // 종료 바이트 확인
            if (buffer[startIndex + packetLength - 1] != 0x55) return false;

            // CAN ID 추출 (Standard 또는 Extended)
            uint canId = isExtended
                ? (uint)buffer[startIndex + 2] | ((uint)buffer[startIndex + 3] << 8) |
                  ((uint)buffer[startIndex + 4] << 16) | ((uint)buffer[startIndex + 5] << 24)
                : (uint)buffer[startIndex + 2] | ((uint)buffer[startIndex + 3] << 8);

            byte[] payload = new byte[dlc];
            buffer.CopyTo(startIndex + 1 + 1 + idLength, payload, 0, dlc);

            // 중복 데이터 필터링
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

        /// <summary>
        /// [CAN 신호 디코딩 메서드]
        /// DBC 파일의 정의에 따라 원시 바이트 데이터를 실제 값으로 변환
        /// </summary>
        /// <param name="signal">DBC에 정의된 신호 정보</param>
        /// <param name="data">원시 바이트 데이터</param>
        /// <returns>변환된 실제 값</returns>
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

        /// <summary>
        /// [UI 업데이트 메서드]
        /// 메시지 타입에 따라 적절한 UI 요소 업데이트
        /// </summary>
        /// <param name="messageName">CAN 메시지 이름</param>
        /// <param name="signalName">신호 이름</param>
        /// <param name="value">디코딩된 값</param>
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