using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using DbcParserLib;
using DbcParserLib.Model;

class Program
{
    static SerialPort serialPort;
    static Dbc dbc;
    static Dictionary<uint, int> canCount = new Dictionary<uint, int>();

    static void Main()
    {
        string portName = "COM7"; // 실제 장치의 COM 포트 번호 확인 필요
        int baudRate = 2000000;   // USB-CAN-A 기본 baud rate

        // DBC 파일 로드
        string dbcFilePath = "E:\\Project\\.NET\\Connect\\vcan.dbc"; // DBC 파일 경로 수정
        LoadDbcFile(dbcFilePath);

        // SerialPort 설정
        serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
        serialPort.DataReceived += DataReceivedHandler;

        try
        {
            serialPort.Open();
            Console.WriteLine($"포트 {portName}가 열렸습니다. 데이터 수신 대기 중...");
            Console.WriteLine("종료하려면 Ctrl+C를 누르세요.");
            Console.ReadLine(); // 프로그램 종료 방지
        }
        catch (Exception ex)
        {
            Console.WriteLine($"포트 열기 실패: {ex.Message}");
        }
    }

    // DBC 파일 로드 함수
    private static void LoadDbcFile(string path)
    {
        try
        {
            dbc = Parser.ParseFromPath(path);
            Console.WriteLine("DBC 파일 로드 완료");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DBC 파일 로드 실패: {ex.Message}");
        }
    }

    // 데이터 수신 이벤트 핸들러
    private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = (SerialPort)sender;
        byte[] buffer = new byte[sp.BytesToRead];
        sp.Read(buffer, 0, buffer.Length);
        ProcessReceivedData(buffer);
    }

    // 수신된 데이터에서 패킷을 파싱하는 함수
    private static void ProcessReceivedData(byte[] data)
    {
        // 최소 패킷 길이: 헤더(1) + 제어(1) + 표준 프레임 CAN ID(2) + 데이터(0) + 종료(1) = 5바이트로 가정
        int i = 0;
        while (i <= data.Length - 5)
        {
            // 헤더(0xAA) 확인
            if (data[i] != 0xAA)
            {
                i++;
                continue;
            }

            // 제어 바이트 읽기 (헤더 다음 바이트)
            byte control = data[i + 1];
            int dlc = control & 0x0F; // 하위 4비트: 데이터 길이(DLC)
            bool isExtended = ((control >> 5) & 0x01) == 1; // 비트5: 1이면 확장 프레임, 0이면 표준 프레임

            // CAN ID 길이: 표준 프레임이면 2바이트, 확장 프레임이면 4바이트
            int idLength = isExtended ? 4 : 2;
            // 전체 패킷 길이 = 헤더(1) + 제어(1) + CAN ID(idLength) + 데이터(dlc) + 종료(1)
            int packetLength = 1 + 1 + idLength + dlc + 1;
            if (i + packetLength > data.Length)
            {
                // 아직 수신된 데이터가 부족하면 break
                break;
            }

            // 종료 코드(0x55) 확인
            if (data[i + packetLength - 1] != 0x55)
            {
                i++;
                continue;
            }

            // CAN ID 추출 (리틀 엔디안 방식)
            uint canId = 0;
            if (isExtended)
            {
                // 확장 프레임: 4바이트
                canId = (uint)data[i + 2]
                      | ((uint)data[i + 3] << 8)
                      | ((uint)data[i + 4] << 16)
                      | ((uint)data[i + 5] << 24);
            }
            else
            {
                // 표준 프레임: 2바이트
                canId = (uint)data[i + 2] | ((uint)data[i + 3] << 8);
            }

            // CAN 데이터(페이로드) 추출
            byte[] payload = new byte[dlc];
            Array.Copy(data, i + 1 + 1 + idLength, payload, 0, dlc);

            // 파싱 결과 출력
            Console.WriteLine("----");
            Console.WriteLine($"패킷 위치: {i}");
            Console.WriteLine($"프레임 종류: {(isExtended ? "확장 프레임" : "표준 프레임")}");
            Console.WriteLine($"DLC(데이터 길이): {dlc}");
            Console.WriteLine($"CAN ID: 0x{canId:X}");
            Console.WriteLine($"CAN 데이터: {BitConverter.ToString(payload)}");

            // DBC 파일을 이용한 CAN 데이터 디코딩 실행
            DecodeCANMessage(canId, payload);

            // CAN 데이터 갯수 파악
            if (dbc != null)
            {
                if (dbc.Messages.Any(m => m.ID == canId))
                {
                    if (canCount.ContainsKey(canId))
                        canCount[canId]++;
                    else
                        canCount[canId] = 1;
                }
            }

            // 다음 패킷으로 이동
            i += packetLength;
        }
    }

    // DBC 파일을 이용한 CAN 데이터 디코딩
    private static void DecodeCANMessage(uint canId, byte[] payload)
    {
        if (dbc == null)
        {
            Console.WriteLine("DBC 파일이 로드되지 않았습니다.");
            return;
        }
        var message = dbc.Messages.FirstOrDefault(m => m.ID == canId);
        if (message == null)
        {
            Console.WriteLine($"ID 0x{canId:X}에 대한 메시지가 DBC 파일에 없음");
            return;
        }
        Console.WriteLine($"[CAN 메시지 디코딩] {message.Name}");
        foreach (var signal in message.Signals)
        {
            double value = DecodeSignal(signal, payload);
            Console.WriteLine($" {signal.Name}: {value} {signal.Unit}");
        }
    }

    // CAN 데이터에서 신호를 추출하여 실제 물리적 값으로 변환
    private static double DecodeSignal(Signal signal, byte[] data)
    {
        bool isLittleEndian = signal.ByteOrder == 1; // 1이면 Little Endian, 0이면 Big Endian
        int rawValue = ExtractBits(data, signal.StartBit, signal.Length, isLittleEndian);
        double physicalValue = (rawValue * signal.Factor) + signal.Offset;
        return physicalValue;
    }

    // 특정 비트 위치에서 데이터를 추출하는 함수
    private static int ExtractBits(byte[] data, int startBit, int length, bool isLittleEndian)
    {
        int value = 0;
        int byteIndex = startBit / 8;
        int bitIndex = startBit % 8;
        for (int i = 0; i < length; i++)
        {
            int bitValue = (data[byteIndex] >> bitIndex) & 1;
            value |= (bitValue << i);
            bitIndex++;
            if (bitIndex == 8)
            {
                byteIndex++;
                bitIndex = 0;
            }
        }

        return value;
    }
}
