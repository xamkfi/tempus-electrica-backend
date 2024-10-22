using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto
{
    public class ProcessedCsvDataResult
    {
        public required decimal TotalSpotPrice { get; set; }
        public required decimal TotalFixedPrice { get; set; }
        public required decimal TotalConsumption { get; set; }
        public required Dictionary<(int Month, int Year), MonthlyConsumptionData> MonthlyData { get; set; }
        public required Dictionary<(int Week, int Year), WeeklyConsumptionData> WeeklyData { get; set; }
        public required Dictionary<DateTime, DailyConsumptionData> DailyData { get; set; }
    }
}
