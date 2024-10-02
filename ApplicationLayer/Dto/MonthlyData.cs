using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto
{
    public class MonthlyData
    {
        public int Month { get; set; }
        public decimal Consumption { get; set; }
        public decimal SpotPriceAverageOfMonth { get; set; }
        public decimal FixedPriceAverageOfMonth { get; set; }
        public decimal FixedPriceTotal { get; set; }
        public decimal SpotPriceTotal { get; set; }
        public decimal AverageConsumptionPerHour { get; set; }
    }
}
