using CommunityToolkit.Mvvm.ComponentModel;

namespace FinalTest.Models
{
    public class EnvironmentData : ObservableObject
    {
        private double outsideAirTemp;
        public double OutsideAirTemp
        {
            get => outsideAirTemp;
            set => SetProperty(ref outsideAirTemp, value);
        }

        private double airPressure;
        public double AirPressure
        {
            get => airPressure;
            set => SetProperty(ref airPressure, value);
        }

        private double windSpeed;
        public double WindSpeed
        {
            get => windSpeed;
            set => SetProperty(ref windSpeed, value);
        }

        private double windDirection;
        public double WindDirection
        {
            get => windDirection;
            set => SetProperty(ref windDirection, value);
        }

        private string turbulenceLevel;
        public string TurbulenceLevel
        {
            get => turbulenceLevel;
            set => SetProperty(ref turbulenceLevel, value);
        }
    }
}
