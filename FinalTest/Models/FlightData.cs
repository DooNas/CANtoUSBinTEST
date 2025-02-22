using CommunityToolkit.Mvvm.ComponentModel;

namespace FinalTest.Models
{
    public class FlightData : ObservableObject
    {
        private string flightMode;
        public string FlightMode
        {
            get => flightMode;
            set => SetProperty(ref flightMode, value);
        }

        private bool autopilotEngaged;
        public bool AutopilotEngaged
        {
            get => autopilotEngaged;
            set => SetProperty(ref autopilotEngaged, value);
        }

        private string landingGearStatus;
        public string LandingGearStatus
        {
            get => landingGearStatus;
            set => SetProperty(ref landingGearStatus, value);
        }

        private double flapsPosition;
        public double FlapsPosition
        {
            get => flapsPosition;
            set => SetProperty(ref flapsPosition, value);
        }

        private double altitude;
        public double Altitude
        {
            get => altitude;
            set => SetProperty(ref altitude, value);
        }

        private double verticalSpeed;
        public double VerticalSpeed
        {
            get => verticalSpeed;
            set => SetProperty(ref verticalSpeed, value);
        }
    }
}
