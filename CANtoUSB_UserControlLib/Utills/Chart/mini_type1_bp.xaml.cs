using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CANtoUSB_UserControlLib.Utills.Chart
{
    /// <summary>
    /// mini_type1_bp.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class mini_type1_bp : UserControl
    {
        public mini_type1_bp()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 계기판의 회전 각도를 설정하는 메서드
        /// </summary>
        /// <param name="angle">회전 각도(도 단위)</param>
        public void SetGaugeAngle(double inputValue)
        {
            // inputValue: 0~270 (슬라이더 값)
            // 내부 포인터 각도는 (225 + inputValue)를 360으로 나눈 나머지로 계산
            double pointerAngle = (225 + inputValue) % 360;
            GlowRotation.Angle = pointerAngle;
        }
    }
}
