using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CANtoUSB_mainTester
{
    public partial class MainWindow : Window
    {
        private const double MIN_TEMP = -20;  // 최소 온도
        private const double MAX_TEMP = 80;   // 최대 온도

        public MainWindow()
        {
            InitializeComponent();

            // 게이지의 값 범위 설정
            MyGauge_bp.SetValueRange(MIN_TEMP, MAX_TEMP);
            MyGauge_gp.SetValueRange(MIN_TEMP, MAX_TEMP);

            // 슬라이더 범위 설정
            AngleSlider.Minimum = MIN_TEMP;
            AngleSlider.Maximum = MAX_TEMP;
            AngleSlider.Value = 0;

            // 게이지 초기값 설정
            MyGauge_bp.SetValue(0);
            MyGauge_gp.SetValue(0);
        }

        // Slider의 값이 변경될 때마다 호출되는 이벤트 핸들러
        private void AngleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MyGauge_bp != null)
            {
                MyGauge_bp.SetValue(e.NewValue);
                // TextBlock 업데이트
                AngleValue.Text = $"현재 온도: {e.NewValue:F0}°C";
            }
            if (MyGauge_gp != null)
            {
                MyGauge_gp.SetValue(e.NewValue);
            }
        }
    }
}