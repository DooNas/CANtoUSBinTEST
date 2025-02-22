using CommunityToolkit.Mvvm.ComponentModel;

namespace FinalTest.Models
{
    public class EngineData : ObservableObject
    {
        private double engine1Thrust;
        public double Engine1Thrust
        {
            get => engine1Thrust;
            set => SetProperty(ref engine1Thrust, value);
        }

        private double engine2Thrust;
        public double Engine2Thrust
        {
            get => engine2Thrust;
            set => SetProperty(ref engine2Thrust, value);
        }

        private double engine1Temperature;
        public double Engine1Temperature
        {
            get => engine1Temperature;
            set => SetProperty(ref engine1Temperature, value);
        }

        private double engine2Temperature;
        public double Engine2Temperature
        {
            get => engine2Temperature;
            set => SetProperty(ref engine2Temperature, value);
        }

        private string engine1Status;
        public string Engine1Status
        {
            get => engine1Status;
            set => SetProperty(ref engine1Status, value);
        }

        private string engine2Status;
        public string Engine2Status
        {
            get => engine2Status;
            set => SetProperty(ref engine2Status, value);
        }
    }

}
