using ApplicationLayer.Dto;
using static ApplicationLayer.Services.ElectricityPriceService;

namespace ApplicationLayer.Interfaces
{
    public interface IElectricityPriceService
    {
        Task<(decimal totalFixedPriceCost, decimal totalSpotPriceCost, decimal costDifference, string cheaperOption, decimal totalAverageConsumption, decimal totalMinConsumption, decimal totalMaxConsumption, decimal averageHourlySpotPrice, List<MonthlyData> monthlyData)> GetElectricityPriceDataAsync(CombinedRequestDtoIn request);
    }
}
