using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FinalTest.Models
{
    /// <summary>
    /// [기본 모델 클래스]
    /// INotifyPropertyChanged 인터페이스를 구현하여 속성 변경 시 UI에 자동 반영되도록 지원
    /// </summary>
    public class BaseModel : INotifyPropertyChanged
    {
        /// <summary>
        /// 속성 변경 이벤트 (MVVM 패턴에서 데이터 바인딩에 사용)
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 속성 값이 변경되었을 때 PropertyChanged 이벤트를 발생시킴
        /// </summary>
        /// <param name="propertyName">변경된 속성 이름 (자동으로 호출됨)</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 속성 값을 변경하고 변경 사항이 있을 경우 이벤트를 발생시키는 메서드
        /// </summary>
        /// <typeparam name="T">속성의 데이터 타입</typeparam>
        /// <param name="field">현재 속성 값</param>
        /// <param name="value">새로운 속성 값</param>
        /// <param name="propertyName">속성 이름 (자동으로 설정됨)</param>
        /// <returns>값이 변경되었으면 true, 아니면 false</returns>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// [비행 상태 클래스]
    /// 항공기의 비행 상태 정보를 저장 및 관리
    /// </summary>
    public class FlightStatus : BaseModel
    {
        private int _mode;                 // 현재 비행 모드
        private bool _autopilotEngaged;     // 자동 조종 장치 활성화 여부
        private int _landingGearStatus;     // 착륙 장치 상태 (0: 해제, 1: 전개)
        private double _flapsPosition;      // 플랩 위치 (0.0 ~ 1.0)
        private double _altitude;           // 현재 고도 (m)
        private double _verticalSpeed;      // 수직 속도 (m/s)

        public int Mode { get => _mode; set => SetField(ref _mode, value); }
        public bool AutopilotEngaged { get => _autopilotEngaged; set => SetField(ref _autopilotEngaged, value); }
        public int LandingGearStatus { get => _landingGearStatus; set => SetField(ref _landingGearStatus, value); }
        public double FlapsPosition { get => _flapsPosition; set => SetField(ref _flapsPosition, value); }
        public double Altitude { get => _altitude; set => SetField(ref _altitude, value); }
        public double VerticalSpeed { get => _verticalSpeed; set => SetField(ref _verticalSpeed, value); }
    }

    /// <summary>
    /// [엔진 데이터 클래스]
    /// 항공기의 엔진 온도, 추력, 상태 정보 저장
    /// </summary>
    public class EngineData : BaseModel
    {
        private double _engine1Temp;   // 엔진 1 온도 (°C)
        private double _engine2Temp;   // 엔진 2 온도 (°C)
        private double _engine1Thrust; // 엔진 1 추력 (kN)
        private double _engine2Thrust; // 엔진 2 추력 (kN)
        private int _engine1Status;    // 엔진 1 상태 (0: 정지, 1: 활성화)
        private int _engine2Status;    // 엔진 2 상태 (0: 정지, 1: 활성화)

        public double Engine1Temp { get => _engine1Temp; set => SetField(ref _engine1Temp, value); }
        public double Engine2Temp { get => _engine2Temp; set => SetField(ref _engine2Temp, value); }
        public double Engine1Thrust { get => _engine1Thrust; set => SetField(ref _engine1Thrust, value); }
        public double Engine2Thrust { get => _engine2Thrust; set => SetField(ref _engine2Thrust, value); }
        public int Engine1Status { get => _engine1Status; set => SetField(ref _engine1Status, value); }
        public int Engine2Status { get => _engine2Status; set => SetField(ref _engine2Status, value); }
    }

    /// <summary>
    /// [연료 데이터 클래스]
    /// 항공기의 연료 잔량 정보 저장
    /// </summary>
    public class FuelData : BaseModel
    {
        private double _leftQuantity;  // 좌측 연료 탱크 잔량 (L)
        private double _rightQuantity; // 우측 연료 탱크 잔량 (L)

        public double LeftQuantity { get => _leftQuantity; set => SetField(ref _leftQuantity, value); }
        public double RightQuantity { get => _rightQuantity; set => SetField(ref _rightQuantity, value); }
    }

    /// <summary>
    /// [환경 데이터 클래스]
    /// 기온, 기압, 풍속, 난기류 정보 저장
    /// </summary>
    public class EnvironmentData : BaseModel
    {
        private double _temperature;      // 외부 온도 (°C)
        private double _pressure;         // 기압 (hPa)
        private double _windSpeed;        // 풍속 (m/s)
        private double _windDirection;    // 풍향 (도)
        private int _turbulenceLevel;     // 난기류 수준 (0~5)

        public double Temperature { get => _temperature; set => SetField(ref _temperature, value); }
        public double Pressure { get => _pressure; set => SetField(ref _pressure, value); }
        public double WindSpeed { get => _windSpeed; set => SetField(ref _windSpeed, value); }
        public double WindDirection { get => _windDirection; set => SetField(ref _windDirection, value); }
        public int TurbulenceLevel { get => _turbulenceLevel; set => SetField(ref _turbulenceLevel, value); }
    }
}
