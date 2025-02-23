using System;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FinalTest
{
    public class BufferMonitor : INotifyPropertyChanged
    {
        private readonly DispatcherTimer monitorTimer;
        private readonly ConcurrentQueue<byte[]> rawQueue;

        private int _bufferSize;
        private int _processedCount;
        private int _droppedCount;

        private const int MinInterval = 10;  // 최소 인터벌 (10ms)
        private const int MaxInterval = 100; // 최대 인터벌 (100ms)
        private const int MaxBufferSizeThreshold = 1000; // 최대 버퍼 크기 기준

        /// <summary>
        /// [현재 버퍼 크기]
        /// rawQueue에 저장된 패킷 개수를 나타냄
        /// </summary>
        public int BufferSize
        {
            get => _bufferSize;
            set
            {
                if (SetField(ref _bufferSize, value))
                {
                    AdjustMonitoringInterval(); // 버퍼 크기가 변경될 때 인터벌 자동 조정
                }
            }
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
                Interval = TimeSpan.FromMilliseconds(MinInterval) // 초기값 10ms
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
        /// [버퍼 상태 업데이트]
        /// </summary>
        private void UpdateBufferStatus()
        {
            BufferSize = rawQueue.Count;
        }

        /// <summary>
        /// [인터벌 동적 조정]
        /// 버퍼 크기에 따라 10~100ms 범위에서 자동 조정
        /// </summary>
        private void AdjustMonitoringInterval()
        {
            // 버퍼 크기를 최대 기준 (1000)으로 정규화하여 인터벌 계산
            double ratio = Math.Min(1.0, (double)BufferSize / MaxBufferSizeThreshold);
            int newInterval = (int)(MinInterval + (MaxInterval - MinInterval) * ratio);

            monitorTimer.Interval = TimeSpan.FromMilliseconds(newInterval);
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
