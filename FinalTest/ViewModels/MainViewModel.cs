using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DbcParserLib;
using DbcParserLib.Model;
using FinalTest.Models;
using FinalTest.Services;
using FinalTest.Utils;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;

namespace FinalTest.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ICanDataService _canDataService;
        private Dbc? _dbc;

        [ObservableProperty]
        private ObservableCollection<string> availablePorts = new();

        [ObservableProperty]
        private string? selectedPort;

        [ObservableProperty]
        private string connectionStatus = "Disconnected";

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private bool isConnecting;

        [ObservableProperty]
        private string dbcStatus = "DBC 파일 로드 대기중";

        public FlightData FlightData { get; } = new();
        public EngineData EngineData { get; } = new();
        public NavigationData NavigationData { get; } = new();
        public FuelData FuelData { get; } = new();
        public EnvironmentData EnvironmentData { get; } = new();

        public MainViewModel()
        {
            _canDataService = new CanDataService();
            LoadPorts();
            LoadDbcFile();
        }

        public MainViewModel(ICanDataService canDataService)
        {
            _canDataService = canDataService ?? throw new ArgumentNullException(nameof(canDataService));
            _canDataService.DataReceived += OnCanDataReceived;
            LoadPorts();
            LoadDbcFile();
        }

        [RelayCommand(CanExecute = nameof(CanConnect))]
        private async Task ConnectAsync()
        {
            if (string.IsNullOrEmpty(SelectedPort)) return;

            try
            {
                IsConnecting = true;
                ConnectionStatus = "연결 시도 중...";

                if (_canDataService.Connect(SelectedPort))
                {
                    await _canDataService.StartAsync();
                    IsConnected = true;
                    ConnectionStatus = "연결됨";
                }
                else
                {
                    ConnectionStatus = "연결 실패";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = "연결 오류";
                MessageBox.Show($"연결 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private bool CanConnect() =>
            !IsConnected &&
            !IsConnecting &&
            !string.IsNullOrEmpty(SelectedPort);

        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private async Task DisconnectAsync()
        {
            try
            {
                await _canDataService.StopAsync();
                _canDataService.Disconnect();
                IsConnected = false;
                ConnectionStatus = "연결 해제됨";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"연결 해제 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanDisconnect() =>
            IsConnected && !IsConnecting;

        [RelayCommand]
        private void RefreshPorts()
        {
            LoadPorts();
        }

        private void LoadPorts()
        {
            AvailablePorts.Clear();
            try
            {
                var ports = SerialPort.GetPortNames();
                foreach (string port in ports)
                {
                    AvailablePorts.Add(port);
                }

                if (AvailablePorts.Count > 0 && string.IsNullOrEmpty(SelectedPort))
                {
                    SelectedPort = AvailablePorts[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"포트 목록 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDbcFile()
        {
            try
            {
                _dbc = Parser.ParseFromPath("E:\\GitHub\\Remote\\CANtoUSBinTEST\\FinalTest\\vcan.dbc");
                DbcStatus = "DBC 파일 로드됨";
            }
            catch (Exception ex)
            {
                DbcStatus = "DBC 파일 로드 실패";
                MessageBox.Show($"DBC 파일 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCanDataReceived(object? sender, CanData data)
        {
            var message = _dbc?.Messages.FirstOrDefault(m => m.ID == data.CanId);
            if (message == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateDataModels(message, data.Payload);
            });
        }

        private void UpdateDataModels(Message message, byte[] payload)
        {
            switch (message.Name)
            {
                case "FLIGHT_STATUS":
                    UpdateFlightStatus(message, payload);
                    break;
                case "ENGINE_DATA":
                    UpdateEngineData(message, payload);
                    break;
                case "NAV_DATA":
                    UpdateNavigationData(message, payload);
                    break;
                case "FUEL_SYSTEM":
                    UpdateFuelSystem(message, payload);
                    break;
                case "ENV_DATA":
                    UpdateEnvironmentData(message, payload);
                    break;
            }
        }

        private void UpdateFlightStatus(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = SignalDecoder.DecodeSignal(signal, payload);
                switch (signal.Name)
                {
                    case "flight_mode":
                        FlightData.FlightMode = GetFlightModeName((int)value);
                        break;
                    case "autopilot_engaged":
                        FlightData.AutopilotEngaged = value == 1;
                        break;
                    case "landing_gear_status":
                        FlightData.LandingGearStatus = GetLandingGearStatus((int)value);
                        break;
                    case "flaps_position":
                        FlightData.FlapsPosition = value;
                        break;
                    case "aircraft_altitude":
                        FlightData.Altitude = value;
                        break;
                    case "vertical_speed":
                        FlightData.VerticalSpeed = value;
                        break;
                }
            }
        }

        private void UpdateEngineData(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = SignalDecoder.DecodeSignal(signal, payload);
                switch (signal.Name)
                {
                    case "engine1_thrust":
                        EngineData.Engine1Thrust = value;
                        break;
                    case "engine2_thrust":
                        EngineData.Engine2Thrust = value;
                        break;
                    case "engine1_temp":
                        EngineData.Engine1Temperature = value;
                        break;
                    case "engine2_temp":
                        EngineData.Engine2Temperature = value;
                        break;
                    case "engine1_status":
                        EngineData.Engine1Status = GetEngineStatus((int)value);
                        break;
                    case "engine2_status":
                        EngineData.Engine2Status = GetEngineStatus((int)value);
                        break;
                }
            }
        }

        private void UpdateNavigationData(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = SignalDecoder.DecodeSignal(signal, payload);
                switch (signal.Name)
                {
                    case "latitude":
                        NavigationData.Latitude = value;
                        break;
                    case "longitude":
                        NavigationData.Longitude = value;
                        break;
                }
            }
        }

        private void UpdateFuelSystem(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = SignalDecoder.DecodeSignal(signal, payload);
                switch (signal.Name)
                {
                    case "fuel_qty_left":
                        FuelData.LeftQuantity = value;
                        break;
                    case "fuel_qty_right":
                        FuelData.RightQuantity = value;
                        break;
                }
            }
        }

        private void UpdateEnvironmentData(Message message, byte[] payload)
        {
            foreach (var signal in message.Signals)
            {
                double value = SignalDecoder.DecodeSignal(signal, payload);
                switch (signal.Name)
                {
                    case "outside_air_temp":
                        EnvironmentData.OutsideAirTemp = value;
                        break;
                    case "air_pressure":
                        EnvironmentData.AirPressure = value;
                        break;
                    case "wind_speed":
                        EnvironmentData.WindSpeed = value;
                        break;
                    case "wind_direction":
                        EnvironmentData.WindDirection = value;
                        break;
                    case "turbulence_level":
                        EnvironmentData.TurbulenceLevel = GetTurbulenceLevel((int)value);
                        break;
                }
            }
        }

        private static string GetFlightModeName(int mode) => mode switch
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

        private static string GetLandingGearStatus(int status) => status switch
        {
            0 => "UP",
            1 => "DOWN",
            2 => "MOVING",
            3 => "FAULT",
            _ => "UNKNOWN"
        };

        private static string GetEngineStatus(int status) => status switch
        {
            0 => "OFF",
            1 => "STARTING",
            2 => "RUNNING",
            3 => "SHUTTING_DOWN",
            4 => "FAULT",
            _ => "UNKNOWN"
        };

        private static string GetTurbulenceLevel(int level) => level switch
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