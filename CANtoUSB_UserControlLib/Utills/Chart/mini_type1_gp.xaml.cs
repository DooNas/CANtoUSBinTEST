using System.Windows.Controls;

namespace CANtoUSB_UserControlLib.Utills.Chart
{
    /// <summary>
    /// mini_type1_gp.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class mini_type1_gp : UserControl
    {
        public mini_type1_gp()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 계기판의 회전 각도를 설정하는 메서드
        /// </summary>
        /// <param name="inputValue">회전 각도(0~270 범위의 슬라이더 값)</param>
        public void SetGaugeAngle(double inputValue)
        {
            // 내부 포인터 각도는 (225 + inputValue)를 360으로 나눈 나머지로 계산
            double pointerAngle = (225 + inputValue) % 360;
            GlowRotation.Angle = pointerAngle;
        }
    }
}