using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CANtoUSB_UserControlLib.Utills.Chart
{
    public partial class mini_type1_bp : UserControl
    {
        private const double GAUGE_START_ANGLE = 135; // 외곽 게이지 시작 각도
        private const double HAND_START_ANGLE = 225;  // 시침 시작 각도
        private const double MAX_ROTATION = 270;      // 최대 회전 각도
        private readonly double _radius = 155;        // 게이지 반지름
        private readonly Point _center = new Point(160, 160); // 게이지 중심점

        public mini_type1_bp()
        {
            InitializeComponent();
            UpdateGauge(0);
        }

        public void SetGaugeAngle(double inputValue)
        {
            // 입력값을 0과 MAX_ROTATION 사이로 제한합니다.
            double normalizedAngle = Math.Min(Math.Max(inputValue, 0), MAX_ROTATION);
            UpdateGauge(normalizedAngle);
        }

        private void UpdateGauge(double angle)
        {
            // 게이지의 시작 및 끝 점 계산
            Point startPoint = GetPointOnCircle(GAUGE_START_ANGLE);
            Point endPoint = GetPointOnCircle(GAUGE_START_ANGLE + angle);

            // Path 업데이트
            GaugeFigure.StartPoint = startPoint;
            GaugeSegment.Point = endPoint;
            GaugeSegment.Size = new Size(_radius, _radius);
            GaugeSegment.IsLargeArc = angle > 180;

            // 시침 회전 (225도에서 시작)
            GlowRotation.Angle = HAND_START_ANGLE + angle;

            // 외곽 게이지 색상 설정 (하늘색)
            GaugeArc.Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 255));
        }

        // 주어진 각도(degree)를 기반으로 원 위의 점을 계산합니다.
        private Point GetPointOnCircle(double angleDegrees)
        {
            double angleRadians = angleDegrees * Math.PI / 180;
            return new Point(
                _center.X + _radius * Math.Cos(angleRadians),
                _center.Y + _radius * Math.Sin(angleRadians)
            );
        }
    }
}