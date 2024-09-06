using ApplicationLayer.Dto;


namespace ApplicationLayer.Interfaces
{
    public interface IElectrictyService
    {
        Task<bool> AddElectricityPricesAsync(ElectricityPriceDataDtoIn electricityPriceDataDtoIn);

    }
}
