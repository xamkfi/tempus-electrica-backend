using ApplicationLayer.Interfaces;
using Domain.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services
{
    public class DateRangeDataService : IDateRangeDataService
    {
        private readonly IElectricityRepository _electricityRepository;
        private readonly ILogger<DateRangeDataService> _logger;

        public DateRangeDataService(IElectricityRepository electricityRepository, ILogger<DateRangeDataService> logger)
        {
            _electricityRepository = electricityRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<ElectricityPriceData>> GetPricesForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("GetPricesForPeriodAsync called with startDate: {StartDate}, endDate: {EndDate}", startDate, endDate);

            try
            {
                IEnumerable<ElectricityPriceData> prices = await _electricityRepository.GetPricesForPeriodAsync(startDate, endDate);

                foreach (ElectricityPriceData price in prices)
                {
                    _logger.LogInformation("Retrieved price data: Start Date: {StartDate}, End Date: {EndDate}, Price: {Price}",
                        price.StartDate, price.EndDate, price.Price);
                }

                _logger.LogInformation("Electricity prices retrieved successfully for period: {StartDate} - {EndDate}.", startDate, endDate);
                return prices;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving electricity prices for period: {StartDate} - {EndDate}", startDate, endDate);
                throw;
            }
        }
    }
}
