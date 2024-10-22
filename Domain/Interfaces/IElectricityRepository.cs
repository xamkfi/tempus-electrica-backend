using Domain.Entities;


namespace Domain.Interfaces
{
    public interface IElectricityRepository
    {

        Task<bool> AddRangeElectricityPricesAsync(IEnumerable<ElectricityPriceData> electricityPriceDataDtoIn);
        Task<bool> IsDuplicateAsync(DateTime startDate, DateTime endDate);

        Task<bool> AddBatchElectricityPricesAsync(IEnumerable<ElectricityPriceData> electricityPriceData);

        Task<List<ElectricityPriceData>> GetDuplicatesAsync(List<DateTime> startDates, List<DateTime> endDates);

        Task<IEnumerable<ElectricityPriceData>> GetPricesForPeriodAsync(DateTime startDate, DateTime endDate);

        Task<DateTime> GetLatestStartDateAsync();
        Task AddRangeAsync(IEnumerable<ElectricityPriceData> dataList);

    }
}
