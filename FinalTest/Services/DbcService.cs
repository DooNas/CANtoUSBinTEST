using DbcParserLib.Model;
using DbcParserLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalTest.Services
{
    /// <summary>
    /// [DBC 파일을 처리하는 서비스]
    /// CAN 메시지와 시그널을 분석하여 해석하는 역할
    /// </summary>
    public class DbcService
    {
        private readonly Dbc _dbc; // DBC 데이터 저장 객체

        /// <summary>
        /// [DBC 파일 로드]
        /// 주어진 DBC 파일을 파싱하여 내부 저장
        /// </summary>
        /// <param name="dbcPath">DBC 파일 경로</param>
        public DbcService(string dbcPath)
        {
            _dbc = Parser.ParseFromPath(dbcPath);
        }

        /// <summary>
        /// [CAN 메시지 조회]
        /// 특정 CAN ID에 해당하는 메시지를 반환
        /// </summary>
        /// <param name="canId">CAN 메시지 ID</param>
        /// <returns>CAN 메시지 객체 (없으면 null)</returns>
        public Message GetMessage(uint canId)
        {
            return _dbc.Messages.FirstOrDefault(m => m.ID == canId);
        }

        /// <summary>
        /// [시그널 값 디코딩]
        /// 데이터에서 시그널을 추출하여 실제 값으로 변환
        /// </summary>
        /// <param name="signal">DBC 시그널 객체</param>
        /// <param name="data">CAN 데이터 페이로드</param>
        /// <returns>디코딩된 실제 값</returns>
        public double DecodeSignal(Signal signal, byte[] data)
        {
            // 데이터에서 시그널의 비트 범위를 추출
            int rawValue = ExtractBits(data, signal.StartBit, signal.Length, signal.ByteOrder == 1);

            // 실제 값으로 변환 (Factor 및 Offset 적용)
            return (rawValue * signal.Factor) + signal.Offset;
        }

        /// <summary>
        /// [비트 추출 메서드]
        /// 특정 위치에서 비트 값을 읽어 정수 값으로 변환
        /// </summary>
        /// <param name="data">CAN 데이터 페이로드</param>
        /// <param name="startBit">시작 비트 위치</param>
        /// <param name="length">비트 길이</param>
        /// <param name="isLittleEndian">리틀 엔디안 여부</param>
        /// <returns>추출된 정수 값</returns>
        private int ExtractBits(byte[] data, int startBit, int length, bool isLittleEndian)
        {
            int value = 0;
            int byteIndex = startBit / 8;  // 시작 비트의 바이트 인덱스
            int bitIndex = startBit % 8;   // 시작 비트의 바이트 내 비트 인덱스

            for (int i = 0; i < length; i++)
            {
                if (byteIndex >= data.Length) break; // 데이터 범위 초과 방지

                // 해당 비트 값 추출 (비트 마스킹)
                int bitValue = (data[byteIndex] >> bitIndex) & 1;

                // 결과값에 추가 (리틀 엔디안 고려)
                value |= (bitValue << i);

                // 다음 비트로 이동
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
}
