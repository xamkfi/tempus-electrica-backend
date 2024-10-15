using ApplicationLayer.Dto.Consumption.Consumption;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Dto.Consumption
{
    public class ConsumptionPriceCalculationResult
    {
        /// <summary>
        /// Total spot price calculated from consumption data.
        /// </summary>
        public decimal TotalSpotPrice { get; set; }

        /// <summary>
        /// Total fixed price calculated from consumption data.
        /// </summary>
        public decimal TotalFixedPrice { get; set; }

        /// <summary>
        /// Indicates which pricing option is cheaper: FixedPrice or SpotPrice.
        /// </summary>
        public string CheaperOption { get; set; }

        /// <summary>
        /// Total consumption over the period.
        /// </summary>
        public decimal TotalConsumption { get; set; }

        /// <summary>
        /// Difference in price between the spot price and fixed price.
        /// </summary>
        public decimal PriceDifference { get; set; }

        /// <summary>
        /// Difference in price after optimizing consumption.
        /// </summary>
        public decimal OptimizedPriceDifference { get; set; }

        /// <summary>
        /// Equivalent fixed price per unit based on spot pricing.
        /// </summary>
        public decimal EquivalentFixedPrice { get; set; }

        /// <summary>
        /// Total spot price after optimizing consumption.
        /// </summary>
        public decimal TotalOptimizedSpotPrice { get; set; }

        /// <summary>
        /// Monthly consumption data.
        /// </summary>
        public List<Dto.Consumption.Consumption.MonthlyConsumptionData> MonthlyData { get; set; }

        /// <summary>
        /// Weekly consumption data.
        /// </summary>
        public List<Dto.Consumption.Consumption.WeeklyConsumptionData> WeeklyData { get; set; }

        /// <summary>
        /// Daily consumption data.
        /// </summary>
        public List<Dto.Consumption.Consumption.DailyConsumptionData> DailyData { get; set; }

        /// <summary>
        /// Start date of the consumption period.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of the consumption period.
        /// </summary>
        public DateTime EndDate { get; set; }

        public ConsumptionPriceCalculationResult()
        {
            MonthlyData = new List<Dto.Consumption.Consumption.MonthlyConsumptionData>();
            WeeklyData = new List<Dto.Consumption.Consumption.WeeklyConsumptionData>();
            DailyData = new List<Dto.Consumption.Consumption.DailyConsumptionData>();
        }
    }
}
