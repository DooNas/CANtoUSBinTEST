using System;
using System.IO.Ports;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace FinalTest.Services
{
    /// <summary>
    /// [시리얼 통신 관리 서비스 클래스]
    /// CAN 데이터의 수신과 기초적인 패킷 처리 담당
    /// </summary>
    public class SerialService : IDisposable
    {
        // 시리얼 포트 객체
        private SerialPort _serialPort;

        // 멀티스레드 환경에서 안전한 큐 (수신된 데이터 저장)
        private readonly ConcurrentQueue<byte[]> _rawQueue = new ConcurrentQueue<byte[]>();

        // 데이터 처리 비동기 작업을 제어하기 위한 취소 토큰
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _processTask;

        // 마지막으로 처리된 CAN 데이터 저장 (중복 전송 방지)
        private readonly Dictionary<uint, byte[]> _lastProcessedData = new Dictionary<uint, byte[]>();

        // 시리얼 포트 접근을 위한 Lock 객체 (멀티스레드 환경 보호)
        private readonly object _serialLock = new object();

        /// <summary>
        /// [CAN 데이터 수신 이벤트]
        /// </summary>
        public event EventHandler<(uint canId, byte[] payload)> DataReceived;

        /// <summary>
        /// [연결 상태 변경 이벤트]
        /// </summary>
        public event EventHandler<string> ConnectionStatusChanged;

        /// <summary>
        /// [현재 연결 상태 확인]
        /// </summary>
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public SerialService()
        {
            _serialPort = new SerialPort
            {
                BaudRate = 2000000, // 2Mbps
                DataBits = 8,       // 8비트 데이터
                Parity = Parity.None, // 패리티 없음
                StopBits = StopBits.One // 정지 비트 1개
            };
        }

        /// <summary>
        /// [시리얼 포트 연결]
        /// 지정된 포트 이름으로 연결을 시도하고, 데이터 수신을 시작함.
        /// </summary>
        public void Connect(string portName)
        {
            try
            {
                // 기존 연결이 있다면 먼저 닫음
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }

                _serialPort.PortName = portName;
                _serialPort.DataReceived += SerialPort_DataReceived; // 데이터 수신 이벤트 등록
                _serialPort.Open();

                // 데이터 처리 작업 시작
                _cts = new CancellationTokenSource();
                _processTask = Task.Run(() => ProcessDataAsync(_cts.Token));

                ConnectionStatusChanged?.Invoke(this, "Connected");
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// [시리얼 포트 연결 해제]
        /// 데이터 수신 중단 및 포트 닫기
        /// </summary>
        public void Disconnect()
        {
            _cts.Cancel(); // 데이터 처리 비동기 작업 중단

            if (_serialPort?.IsOpen == true)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived; // 이벤트 핸들러 해제
                _serialPort.Close();
            }

            ConnectionStatusChanged?.Invoke(this, "Disconnected");
        }

        /// <summary>
        /// [데이터 수신 이벤트 핸들러]
        /// 시리얼 포트에서 데이터가 수신되면 큐에 추가
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_serialPort.IsOpen) return;

            lock (_serialLock) // 멀티스레드 환경 보호
            {
                int bytesToRead = _serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                _serialPort.Read(buffer, 0, bytesToRead);
                _rawQueue.Enqueue(buffer);
            }
        }

        /// <summary>
        /// [수신된 데이터를 비동기 처리]
        /// </summary>
        private async Task ProcessDataAsync(CancellationToken token)
        {
            List<byte> buffer = new List<byte>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    while (_rawQueue.TryDequeue(out byte[] data))
                    {
                        buffer.AddRange(data);
                    }

                    ProcessBuffer(buffer);
                    await Task.Delay(10, token); // CPU 과부하 방지
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessDataAsync] Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// [수신된 데이터 버퍼에서 유효한 패킷 추출 및 처리]
        /// </summary>
        private void ProcessBuffer(List<byte> buffer)
        {
            while (buffer.Count >= 5)
            {
                int i = 0;
                bool processedPacket = false;

                while (i < buffer.Count - 5)
                {
                    if (buffer[i] != 0xAA) // 패킷 시작 바이트 확인
                    {
                        i++;
                        continue;
                    }

                    byte control = buffer[i + 1];
                    int dlc = control & 0x0F; // 데이터 길이 (0~15)
                    bool isExtended = ((control >> 5) & 0x01) == 1; // 확장 CAN ID 여부
                    int idLength = isExtended ? 4 : 2;
                    int packetLength = 1 + 1 + idLength + dlc + 1; // 전체 패킷 크기 계산

                    if (i + packetLength > buffer.Count) // 데이터 부족하면 중단
                        break;

                    if (buffer[i + packetLength - 1] != 0x55) // 패킷 종료 바이트 확인
                    {
                        i++;
                        continue;
                    }

                    uint canId = isExtended
                        ? (uint)buffer[i + 2] | ((uint)buffer[i + 3] << 8) | ((uint)buffer[i + 4] << 16) | ((uint)buffer[i + 5] << 24)
                        : (uint)buffer[i + 2] | ((uint)buffer[i + 3] << 8);

                    byte[] payload = new byte[dlc];
                    buffer.CopyTo(i + 1 + 1 + idLength, payload, 0, dlc);

                    if (!_lastProcessedData.TryGetValue(canId, out byte[] lastPayload) ||
                        !payload.SequenceEqual(lastPayload))
                    {
                        _lastProcessedData[canId] = payload.ToArray();
                        DataReceived?.Invoke(this, (canId, payload));
                    }

                    // 불필요한 데이터 삭제 (성능 최적화)
                    buffer = buffer.Skip(i + packetLength).ToList();
                    processedPacket = true;
                    break;
                }

                if (!processedPacket)
                    break;
            }
        }

        /// <summary>
        /// [자원 해제 메서드]
        /// </summary>
        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_serialPort != null)
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        /// <summary>
        /// [사용 가능한 시리얼 포트 목록 가져오기]
        /// </summary>
        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }
    }
}
