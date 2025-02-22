using CommunityToolkit.Mvvm.ComponentModel;

namespace FinalTest.Models
{
    public class FuelData : ObservableObject
    {
        private double leftQuantity;
        public double LeftQuantity
        {
            get => leftQuantity;
            set => SetProperty(ref leftQuantity, value);
        }

        private double rightQuantity;
        public double RightQuantity
        {
            get => rightQuantity;
            set => SetProperty(ref rightQuantity, value);
        }
    }
}
