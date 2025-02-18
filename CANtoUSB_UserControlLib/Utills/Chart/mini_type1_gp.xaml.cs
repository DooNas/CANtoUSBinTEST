using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CANtoUSB_UserControlLib.Utills.Chart
{
    public partial class mini_type1_gp : UserControl
    {
        private const double GAUGE_START_ANGLE = 135; // 외곽 게이지 시작 각도
        private const double HAND_START_ANGLE = 225;  // 시침 시작 각도
        private const double MAX_ROTATION = 270;      // 최대 회전 각도
        private readonly double _radius = 155;        // 게이지 반지름
        private readonly Point _center = new Point(160, 160); // 게이지 중심점

        private double _minValue = 0;    // 최소값
        private double _maxValue = 100;  // 최대값
        private string _unit = "°C";  // 기본 단위 설정
        private double _currentValue = 0; // 현재 값을 저장하기 위한 필드 추가

        public mini_type1_gp()
        {
            InitializeComponent();
            UpdateGauge(0, 0);
        }

        /// <summary>
        /// 게이지의 단위를 설정합니다.
        /// </summary>
        /// <param name="unit">표시할 단위 (예: °C, °F, %, RPM 등)</param>
        public void SetUnit(string unit)
        {
            _unit = unit;

            // 현재 저장된 값으로 업데이트
            UpdateGauge(
                (GlowRotation.Angle - HAND_START_ANGLE),
                _currentValue
            );
        }

        /// <summary>
        /// 게이지의 값 범위를 설정합니다.
        /// </summary>
        /// <param name="minValue">최소값</param>
        /// <param name="maxValue">최대값</param>
        public void SetValueRange(double minValue, double maxValue)
        {
            if (minValue >= maxValue)
                throw new ArgumentException("최소값은 최대값보다 작아야 합니다.");

            _minValue = minValue;
            _maxValue = maxValue;
        }

        /// <summary>
        /// 게이지에 값을 설정합니다.
        /// </summary>
        /// <param name="value">설정할 값</param>
        public void SetValue(double value)
        {
            // 입력값을 최소값과 최대값 사이로 제한
            double normalizedValue = Math.Min(Math.Max(value, _minValue), _maxValue);

            // 퍼센트로 변환 (0-100)
            double percentage = ((normalizedValue - _minValue) / (_maxValue - _minValue)) * 100;

            // 각도로 변환 (0-270)
            double angle = (percentage * MAX_ROTATION) / 100;

            UpdateGauge(angle, normalizedValue);
        }

        private void UpdateGauge(double angle, double value)
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

            // 외곽 게이지 색상 설정 (초록색)
            GaugeArc.Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 0));

            // 값의 범위에 따른 색상 설정
            double percentage = ((value - _minValue) / (_maxValue - _minValue)) * 100;
            Color valueColor;

            if (percentage <= 33.33)
                valueColor = Colors.Yellow;     // 1단계: 노랑
            else if (percentage <= 66.66)
                valueColor = Colors.LimeGreen;  // 2단계: 초록
            else
                valueColor = Colors.Red;        // 3단계: 빨강

            // 온도 값과 단위를 분리하여 다른 색상 적용
            var valueText = new Run($"{(int)value}") { Foreground = new SolidColorBrush(valueColor) };
            var unitText = new Run("°C") { Foreground = Brushes.White };

            TemperatureDisplay.Inlines.Clear();
            TemperatureDisplay.Inlines.Add(valueText);
            TemperatureDisplay.Inlines.Add(unitText);
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