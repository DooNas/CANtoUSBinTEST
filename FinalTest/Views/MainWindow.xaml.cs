using System.ComponentModel;
using System.Windows;
using FinalTest.ViewModels;

namespace FinalTest.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            InitializeGauges();
            SubscribeToPropertyChanges();
        }

        private void InitializeGauges()
        {
            // 엔진 온도 게이지 초기화
            Engine1TempGauge.SetValueRange(0, 4095);
            Engine1TempGauge.SetUnit("°C");

            Engine2TempGauge.SetValueRange(0, 4095);
            Engine2TempGauge.SetUnit("°C");

            // 연료량 게이지 초기화
            FuelLeftGauge.SetValueRange(0, 6553.5);
            FuelLeftGauge.SetUnit("kg");

            FuelRightGauge.SetValueRange(0, 6553.5);
            FuelRightGauge.SetUnit("kg");
        }

        private void SubscribeToPropertyChanges()
        {
            // EngineData의 속성 변경 구독
            _viewModel.EngineData.PropertyChanged += OnEngineDataPropertyChanged;

            // FuelData의 속성 변경 구독
            _viewModel.FuelData.PropertyChanged += OnFuelDataPropertyChanged;
        }

        private void OnEngineDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.EngineData.Engine1Temperature):
                        Dispatcher.Invoke(() => Engine1TempGauge.SetValue(_viewModel.EngineData.Engine1Temperature));
                        break;
                    case nameof(_viewModel.EngineData.Engine2Temperature):
                        Dispatcher.Invoke(() => Engine2TempGauge.SetValue(_viewModel.EngineData.Engine2Temperature));
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"게이지 업데이트 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnFuelDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.FuelData.LeftQuantity):
                        Dispatcher.Invoke(() => FuelLeftGauge.SetValue(_viewModel.FuelData.LeftQuantity));
                        break;
                    case nameof(_viewModel.FuelData.RightQuantity):
                        Dispatcher.Invoke(() => FuelRightGauge.SetValue(_viewModel.FuelData.RightQuantity));
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"게이지 업데이트 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _viewModel.DisconnectCommand.Execute(null);
            base.OnClosing(e);
        }
    }
}