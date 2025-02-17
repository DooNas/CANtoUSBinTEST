using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CANtoUSB_mainTester;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent(); 
        AngleSlider.Value = 0;
        // 게이지들도 0도로 초기화
        MyGauge_bp.SetGaugeAngle(0);
        MyGauge_gp.SetGaugeAngle(0);
    }

    // Slider의 값이 변경될 때마다 호출되는 이벤트 핸들러
    private void AngleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MyGauge_bp != null)
        {
            MyGauge_bp.SetGaugeAngle(e.NewValue);
            // TextBlock 업데이트
            AngleValue.Text = $"현재 각도: {e.NewValue:F0}°";
        }

        if (MyGauge_gp != null)
        {
            MyGauge_gp.SetGaugeAngle(e.NewValue);
            // TextBlock 업데이트
            AngleValue.Text = $"현재 각도: {e.NewValue:F0}°";
        }
    }
}