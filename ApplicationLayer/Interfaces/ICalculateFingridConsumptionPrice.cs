using ApplicationLayer.Dto;

public interface ICalculateFingridConsumptionPrice
{
     Task<(decimal totalSpotPrice, decimal totalFixedPrice, string cheaperOption, decimal totalConsumption, decimal priceDifference, decimal optimizedPriceDifference, decimal equivalentFixedPrice, decimal totalOptimizedSpotPrice, List<MonthlyConsumptionData> monthlyData, List<WeeklyConsumptionData> weeklyData, List<DailyConsumptionData> dailyData, DateTime startDate, DateTime endDate)> CalculateTotalConsumptionPricesAsync(string csvFilePath, decimal? fixedPrice);

}
