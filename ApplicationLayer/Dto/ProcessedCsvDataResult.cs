using ApplicationLayer.Dto.Consumption.Consumption;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto
{
    public class ProcessedCsvDataResult
    {
        public decimal TotalSpotPrice { get; set; }
        public decimal TotalFixedPrice { get; set; }
        public decimal TotalConsumption { get; set; }
        public Dictionary<(int Month, int Year), MonthlyConsumptionData> MonthlyData { get; set; }
        public Dictionary<(int Week, int Year), WeeklyConsumptionData> WeeklyData { get; set; }
        public Dictionary<DateTime, DailyConsumptionData> DailyData { get; set; }
    }
}
