using ApplicationLayer.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services
{
    public class ConsumptionOptimizer : IConsumptionOptimizer
    {
        private readonly ILogger<ConsumptionOptimizer> _logger;

        public ConsumptionOptimizer(ILogger<ConsumptionOptimizer> logger)
        {
            _logger = logger;
        }

        public ConcurrentDictionary<DateTime, decimal> OptimizeConsumption(
            ConcurrentDictionary<DateTime, decimal> hourlyConsumption,
            decimal optimizePercentage)
        {
            _logger.LogInformation("Optimizing consumption.");

            var optimizedConsumption = new ConcurrentDictionary<DateTime, decimal>();

            foreach (var (timestamp, consumption) in hourlyConsumption)
            {
                var roundedTimestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0);
                optimizedConsumption.AddOrUpdate(roundedTimestamp, consumption, (key, oldValue) => oldValue + consumption);
            }

            var maxTimestamp = optimizedConsumption.Keys.Max();

            foreach (var (timestamp, consumption) in optimizedConsumption)
            {
                if (timestamp.Hour >= 12 && timestamp.Hour <= 23)
                {
                    var consumptionToMove = consumption * optimizePercentage;

                    var nextDayTimestamp = timestamp.AddHours(12);
                    if (nextDayTimestamp <= maxTimestamp)
                    {
                        optimizedConsumption.AddOrUpdate(timestamp, consumption, (key, oldValue) => oldValue - consumptionToMove);
                        optimizedConsumption.AddOrUpdate(nextDayTimestamp, consumptionToMove, (key, oldValue) => oldValue + consumptionToMove);
                    }
                    else
                    {
                        _logger.LogWarning("Cannot move consumption beyond end date for timestamp {timestamp}.", timestamp);
                    }
                }
            }

            _logger.LogInformation("Consumption optimized successfully.");
            return optimizedConsumption;
        }
    }
}
