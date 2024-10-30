using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto.Consumption
{
    public class ConsumptionSettings
    {
        public decimal SavingsPerFireplaceUse { get; set; } = 8M;
        public Dictionary<int, decimal> MonthlyWeights { get; set; } = new Dictionary<int, decimal>
        {
            { 1, 0.12M }, { 2, 0.10M }, { 3, 0.09M }, { 4, 0.08M },
            { 5, 0.07M }, { 6, 0.06M }, { 7, 0.06M }, { 8, 0.06M },
            { 9, 0.07M }, { 10, 0.09M }, { 11, 0.10M }, { 12, 0.10M }
        };
        public Dictionary<int, decimal> MonthlyProductionPerPanel { get; set; } = new Dictionary<int, decimal>
        {
            { 1, 6.3M }, { 2, 15.5M }, { 3, 33.6M }, { 4, 41.7M },
            { 5, 51.3M }, { 6, 49.5M }, { 7, 49.1M }, { 8, 42.7M },
            { 9, 29.6M }, { 10, 18.0M }, { 11, 6.4M }, { 12, 3.1M }
        };
        public decimal FloorHeatingConsumptionPerSquareMeter { get; set; } = 100M;
        public Dictionary<WorkShiftType, Dictionary<int, decimal>> WorkShiftWeights { get; set; } = new Dictionary<WorkShiftType, Dictionary<int, decimal>>
        {
            { WorkShiftType.DayWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.02M }, { 1, 0.02M }, { 2, 0.02M }, { 3, 0.02M }, { 4, 0.02M },
                    { 5, 0.02M }, { 6, 0.03M }, { 7, 0.03M }, { 8, 0.03M }, { 9, 0.03M },
                    { 10, 0.03M }, { 11, 0.03M }, { 12, 0.03M }, { 13, 0.03M }, { 14, 0.03M },
                    { 15, 0.03M }, { 16, 0.03M }, { 17, 0.05M }, { 18, 0.07M }, { 19, 0.08M },
                    { 20, 0.08M }, { 21, 0.07M }, { 22, 0.05M }, { 23, 0.04M }
                }
            },
            { WorkShiftType.RemoteWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.02M }, { 1, 0.02M }, { 2, 0.02M }, { 3, 0.02M }, { 4, 0.02M },
                    { 5, 0.03M }, { 6, 0.04M }, { 7, 0.05M }, { 8, 0.06M }, { 9, 0.07M },
                    { 10, 0.07M }, { 11, 0.07M }, { 12, 0.07M }, { 13, 0.07M }, { 14, 0.07M },
                    { 15, 0.07M }, { 16, 0.07M }, { 17, 0.06M }, { 18, 0.05M }, { 19, 0.04M },
                    { 20, 0.03M }, { 21, 0.03M }, { 22, 0.02M }, { 23, 0.02M }
                }
            },
            { WorkShiftType.ShiftWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.04M }, { 1, 0.04M }, { 2, 0.04M }, { 3, 0.04M }, { 4, 0.04M },
                    { 5, 0.05M }, { 6, 0.05M }, { 7, 0.05M }, { 8, 0.05M }, { 9, 0.05M },
                    { 10, 0.05M }, { 11, 0.05M }, { 12, 0.05M }, { 13, 0.05M }, { 14, 0.05M },
                    { 15, 0.05M }, { 16, 0.05M }, { 17, 0.05M }, { 18, 0.05M }, { 19, 0.05M },
                    { 20, 0.05M }, { 21, 0.05M }, { 22, 0.05M }, { 23, 0.05M }
                }
            }
        };
    }

    public class ConsumptionResult
    {
        public decimal MinConsumption { get; set; }
        public decimal AverageConsumption { get; set; }
        public decimal MaxConsumption { get; set; }
    }
}
