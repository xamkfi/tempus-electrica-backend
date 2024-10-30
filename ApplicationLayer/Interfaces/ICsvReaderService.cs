using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Interfaces
{
    public interface ICsvReaderService
    {
        Task<ConcurrentDictionary<DateTime, decimal>> ReadHourlyConsumptionAsync(string csvFilePath);
    }
}
