# CANtoUSBinTEST

### USB-CAN-A_TestToConsole
USB - CAN - A로 데이터를 받아 디버깅하는 과정만 구현  
사용한 외부 라이브러리  
1. System.IO.Ports(https://www.nuget.org/packages/System.IO.Ports)  
2. DbcParserLib(https://www.nuget.org/packages/DbcParserLib)
    ```csharp
    //.Net CLI
    dotnet add package System.IO.Ports --version 9.0.2
    dotnet add package DbcParserLib --version 1.7.0
    ```
---  
### FinalTest
USB - CAN - A로 데이터를 받아 디버깅하는 과정을 비동기 처리 후 UI에 계기판 및 텍스트로 출력
사용한 외부 라이브러리  
1. System.IO.Ports(https://www.nuget.org/packages/System.IO.Ports)  
2. DbcParserLib(https://www.nuget.org/packages/DbcParserLib)
    ```csharp
    //.Net CLI
    dotnet add package System.IO.Ports --version 9.0.2
    dotnet add package DbcParserLib --version 1.7.0
    ```
---  
