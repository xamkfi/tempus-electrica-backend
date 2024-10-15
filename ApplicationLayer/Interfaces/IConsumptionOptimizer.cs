using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Interfaces
{
    public interface IConsumptionOptimizer
    {
        ConcurrentDictionary<DateTime, decimal> OptimizeConsumption(
            ConcurrentDictionary<DateTime, decimal> hourlyConsumption,
            decimal optimizePercentage);
    }
}
