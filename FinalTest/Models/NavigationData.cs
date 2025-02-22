using CommunityToolkit.Mvvm.ComponentModel;

namespace FinalTest.Models
{
    public class NavigationData : ObservableObject
    {
        private double latitude;
        public double Latitude
        {
            get => latitude;
            set => SetProperty(ref latitude, value);
        }

        private double longitude;
        public double Longitude
        {
            get => longitude;
            set => SetProperty(ref longitude, value);
        }
    }
}
