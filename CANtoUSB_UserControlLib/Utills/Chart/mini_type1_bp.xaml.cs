using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        private double _minValue = 0;    // 최소값
        private double _maxValue = 100;  // 최대값
        private string _unit = "°C";  // 기본 단위 설정

        public mini_type1_bp()
        {
            InitializeComponent();
            UpdateGauge(0);
        }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(mini_type1_bp),
        new PropertyMetadata(0.0, OnValueChanged));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is mini_type1_bp gauge)
            {
                gauge.SetValue((double)e.NewValue);
            }
        }

        /// <summary>
        /// 게이지의 단위를 설정합니다.
        /// </summary>
        /// <param name="unit">표시할 단위 (예: °C, °F, %, RPM 등)</param>
        public void SetUnit(string unit)
        {
            _unit = unit;
            UpdateGauge(GlowRotation.Angle - HAND_START_ANGLE);
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
            double normalizedValue = Math.Clamp(value, _minValue, _maxValue);
            // 퍼센트로 변환 (0-100)
            double percentage = (normalizedValue - _minValue) / (_maxValue - _minValue);
            // 각도로 변환 (0-270)
            double angle = percentage * MAX_ROTATION;

            UpdateGauge(angle, normalizedValue);
        }

        private void UpdateGauge(double angle, double value = 0)
        {
            // 게이지의 시작 및 끝 점 계산
            UpdateGaugePath(angle);
            UpdateGaugeNeedle(angle);
            UpdateGaugeDisplay(value);
        }

        private void UpdateGaugePath(double angle)
        {
            Point startPoint = GetPointOnCircle(GAUGE_START_ANGLE);
            Point endPoint = GetPointOnCircle(GAUGE_START_ANGLE + angle);

            // Path 업데이트
            GaugeFigure.StartPoint = startPoint;
            GaugeSegment.Point = endPoint;
            GaugeSegment.Size = new Size(_radius, _radius);
            GaugeSegment.IsLargeArc = angle > 180;

            // 외곽 게이지 색상 결정(하늘색)
            GaugeArc.Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 255));
        }

        private void UpdateGaugeNeedle(double angle)
        {
            GlowRotation.Angle = HAND_START_ANGLE + angle;
        }

        private void UpdateGaugeDisplay(double value)
        {
            double percentage = (value - _minValue) / (_maxValue - _minValue);
            Color valueColor = GetValueColor(percentage);

            TemperatureDisplay.Inlines.Clear();
            TemperatureDisplay.Inlines.Add(new Run($"{(int)value}") { Foreground = new SolidColorBrush(valueColor) });
            TemperatureDisplay.Inlines.Add(new Run(_unit) { Foreground = Brushes.White });
        }

        private Color GetValueColor(double percentage)
        {
            // 값의 범위에 따른 색상 변경
            return percentage switch
            {
                <= 0.33 => Colors.Yellow,
                <= 0.66 => Colors.LimeGreen,
                _ => Colors.Red
            };
        }

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
