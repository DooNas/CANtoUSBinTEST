using FinalTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalTest.Services
{
    public interface ICanDataService
    {
        event EventHandler<CanData> DataReceived;
        Task StartAsync();
        Task StopAsync();
        bool Connect(string portName);
        void Disconnect();
    }
}
