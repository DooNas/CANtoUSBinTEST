using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalTest.Models
{
    public class FlightStatus : BaseModel
    {
        private int _mode;
        private bool _autopilotEngaged;
        private int _landingGearStatus;
        private double _flapsPosition;
        private double _altitude;
        private double _verticalSpeed;

        public int Mode { get => _mode; set => SetField(ref _mode, value); }
        public bool AutopilotEngaged { get => _autopilotEngaged; set => SetField(ref _autopilotEngaged, value); }
        public int LandingGearStatus { get => _landingGearStatus; set => SetField(ref _landingGearStatus, value); }
        public double FlapsPosition { get => _flapsPosition; set => SetField(ref _flapsPosition, value); }
        public double Altitude { get => _altitude; set => SetField(ref _altitude, value); }
        public double VerticalSpeed { get => _verticalSpeed; set => SetField(ref _verticalSpeed, value); }
    }

    public class EngineData : BaseModel
    {
        private double _engine1Temp;
        private double _engine2Temp;
        private double _engine1Thrust;
        private double _engine2Thrust;
        private int _engine1Status;
        private int _engine2Status;

        public double Engine1Temp { get => _engine1Temp; set => SetField(ref _engine1Temp, value); }
        public double Engine2Temp { get => _engine2Temp; set => SetField(ref _engine2Temp, value); }
        public double Engine1Thrust { get => _engine1Thrust; set => SetField(ref _engine1Thrust, value); }
        public double Engine2Thrust { get => _engine2Thrust; set => SetField(ref _engine2Thrust, value); }
        public int Engine1Status { get => _engine1Status; set => SetField(ref _engine1Status, value); }
        public int Engine2Status { get => _engine2Status; set => SetField(ref _engine2Status, value); }
    }

    public class FuelData : BaseModel
    {
        private double _leftQuantity;
        private double _rightQuantity;

        public double LeftQuantity { get => _leftQuantity; set => SetField(ref _leftQuantity, value); }
        public double RightQuantity { get => _rightQuantity; set => SetField(ref _rightQuantity, value); }
    }

    public class EnvironmentData : BaseModel
    {
        private double _temperature;
        private double _pressure;
        private double _windSpeed;
        private double _windDirection;
        private int _turbulenceLevel;

        public double Temperature { get => _temperature; set => SetField(ref _temperature, value); }
        public double Pressure { get => _pressure; set => SetField(ref _pressure, value); }
        public double WindSpeed { get => _windSpeed; set => SetField(ref _windSpeed, value); }
        public double WindDirection { get => _windDirection; set => SetField(ref _windDirection, value); }
        public int TurbulenceLevel { get => _turbulenceLevel; set => SetField(ref _turbulenceLevel, value); }
    }
}
