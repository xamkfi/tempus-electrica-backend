using ApplicationLayer.Dto;
using static ApplicationLayer.Services.ElectricityPriceService;

namespace ApplicationLayer.Interfaces
{
    public interface IElectricityPriceService
    {
        Task<ElectricityPriceResultDto> GetElectricityPriceDataAsync(CombinedRequestDtoIn request);
    }
}
