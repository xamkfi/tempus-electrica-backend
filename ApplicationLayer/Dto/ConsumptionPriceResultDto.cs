using ApplicationLayer.Dto;

public class ConsumptionPriceCalculationResult
{
    public decimal TotalSpotPrice { get; set; }
    public decimal TotalFixedPrice { get; set; }
    public string CheaperOption { get; set; }
    public decimal TotalConsumption { get; set; }
    public decimal PriceDifference { get; set; }
    public decimal OptimizedPriceDifference { get; set; }
    public decimal EquivalentFixedPrice { get; set; }
    public decimal TotalOptimizedSpotPrice { get; set; }
    public List<MonthlyConsumptionData> MonthlyData { get; set; }
    public List<WeeklyConsumptionData> WeeklyData { get; set; }
    public List<DailyConsumptionData> DailyData { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}