using Domain.Entities;



namespace ApplicationLayer.Interfaces
{
    public interface IDateRangeDataService
    {
        Task<IEnumerable<ElectricityPriceData>> GetPricesForPeriodAsync(DateTime startDate, DateTime endDate);
    }
}
