using System;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace FinalTest
{
    /// <summary>
    /// [CAN 데이터 버퍼 상태 모니터링 클래스]
    /// 데이터 처리 성능과 오류 상태를 실시간으로 추적
    /// </summary>
    public class BufferMonitor : INotifyPropertyChanged
    {
        private readonly DispatcherTimer monitorTimer;
        private readonly ConcurrentQueue<byte[]> rawQueue;

        // 현재 버퍼에 저장된 패킷 수
        private int _bufferSize;
        // 성공적으로 처리된 패킷 수
        private int _processedCount;
        // 오류로 인해 드롭된 패킷 수
        private int _droppedCount;


        public int BufferSize
        {
            get => _bufferSize;
            set => SetField(ref _bufferSize, value);
        }

        public int ProcessedCount
        {
            get => _processedCount;
            set => SetField(ref _processedCount, value);
        }

        public int DroppedCount
        {
            get => _droppedCount;
            set => SetField(ref _droppedCount, value);
        }

        public BufferMonitor(ConcurrentQueue<byte[]> queue)
        {
            rawQueue = queue;
            monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            monitorTimer.Tick += MonitorTimer_Tick;
        }

        public void Start()
        {
            monitorTimer.Start();
        }

        public void Stop()
        {
            monitorTimer.Stop();
        }

        public void IncrementProcessedCount()
        {
            ProcessedCount++;
        }

        public void IncrementDroppedCount()
        {
            DroppedCount++;
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            UpdateBufferStatus();
        }

        /// <summary>
        /// [버퍼 상태 업데이트 메서드]
        /// [queue]ms 간격으로 호출되어 현재 버퍼 상태 갱신
        /// </summary>
        private void UpdateBufferStatus()
        {
            BufferSize = rawQueue.Count;
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}