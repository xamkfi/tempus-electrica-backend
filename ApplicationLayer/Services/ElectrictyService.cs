using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services 
{
    public class ElectrictyService : IElectrictyService
    {
        private readonly IElectricityRepository _electricityRepository;
        private readonly ILogger<ElectrictyService> _logger;

        public ElectrictyService(IElectricityRepository electricityRepository, ILogger<ElectrictyService> logger)
        {
            _electricityRepository = electricityRepository;
            _logger = logger;
        }

        public async Task<bool> AddElectricityPricesAsync(ElectricityPriceDataDtoIn electricityPriceDataDtoIn)
        {
            _logger.LogInformation("AddElectricityPricesAsync started at {StartTime}", DateTime.UtcNow);

            if (electricityPriceDataDtoIn == null || electricityPriceDataDtoIn.Prices == null || !electricityPriceDataDtoIn.Prices.Any())
            {
                _logger.LogWarning("No data provided or empty Prices list.");
                return false;
            }

            try
            {
                List<ElectricityPriceData> electricityPriceData = new List<ElectricityPriceData>();
                bool hasNewData = false;

                foreach (var priceInfo in electricityPriceDataDtoIn.Prices)
                {
                    if (priceInfo.StartDate == DateTime.MinValue || priceInfo.EndDate == DateTime.MinValue || priceInfo.Price <= 0)
                    {
                        _logger.LogWarning("Invalid price data detected. Skipping entry with StartDate: {StartDate}, EndDate: {EndDate}, Price: {Price}", priceInfo.StartDate, priceInfo.EndDate, priceInfo.Price);
                        continue;
                    }

                    _logger.LogInformation("Checking for duplicates: StartDate: {StartDate}, EndDate: {EndDate}", priceInfo.StartDate, priceInfo.EndDate);
                    var isDuplicate = await _electricityRepository.IsDuplicateAsync(priceInfo.StartDate, priceInfo.EndDate);

                    if (isDuplicate)
                    {
                        _logger.LogInformation("Duplicate found. StartDate: {StartDate}, EndDate: {EndDate} already exists in the database.", priceInfo.StartDate, priceInfo.EndDate);
                        continue;
                    }

                    hasNewData = true;
                    electricityPriceData.Add(priceInfo.ToEntity());
                    _logger.LogInformation("Added price data: StartDate: {StartDate}, EndDate: {EndDate}, Price: {Price}", priceInfo.StartDate, priceInfo.EndDate, priceInfo.Price);
                }

                if (!hasNewData)
                {
                    _logger.LogInformation("All records were duplicates or invalid. No new data added.");
                    return false; // All data was duplicates or invalid
                }

                var result = await _electricityRepository.AddRangeElectricityPricesAsync(electricityPriceData);

                if (result)
                {
                    _logger.LogInformation("All non-duplicate records added to the database successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to add records to the database.");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred when adding data to the database: {Message}", ex.Message);
                return false;
            }
            finally
            {
                _logger.LogInformation("AddElectricityPricesAsync ended at {EndTime}", DateTime.UtcNow);
            }
        }
    }
}
