using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto
{
    public class ElectricityPriceResultDto
    {
        public decimal TotalFixedPriceCost { get; set; }
        public decimal TotalSpotPriceCost { get; set; }
        public decimal CostDifference { get; set; }
        public string CheaperOption { get; set; }
        public decimal AverageConsumption { get; set; }
        public decimal MinConsumption { get; set; }
        public decimal MaxConsumption { get; set; }
        public decimal AverageHourlySpotPrice { get; set; }
        public List<MonthlyData> MonthlyData { get; set; }
    }
}
