using Application.Dto;
using static ApplicationLayer.Services.ElectricityPriceService;

namespace ApplicationLayer.Interfaces
{
    public interface IElectricityPriceService
    {
        Task<(decimal totalFixedPriceCost, decimal totalSpotPriceCost, decimal costDifference, string cheaperOption, decimal totalConsumption, decimal averageHourlySpotPrice, List<MonthlyData> monthlyData)> GetElectricityPriceDataAsync(CombinedRequestDtoIn request);
    }
}
