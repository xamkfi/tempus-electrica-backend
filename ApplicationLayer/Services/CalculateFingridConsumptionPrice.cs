using System.Collections.Concurrent;
using ApplicationLayer.Dto;
using ApplicationLayer.Dto.Consumption.Consumption;
using ApplicationLayer.Dto.Consumption;
using ApplicationLayer.Interfaces;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services
{
    public class CalculateFinGridConsumptionPriceService : ICalculateFingridConsumptionPrice
    {
        private readonly ICsvReaderService _csvReaderService;
        private readonly IElectricityRepository _electricityRepository;
        private readonly IConsumptionDataProcessor _consumptionDataProcessor;
        private readonly IConsumptionOptimizer _consumptionOptimizer;
        private readonly ILogger<CalculateFinGridConsumptionPriceService> _logger;
        private readonly decimal _optimizePercentage;

        public CalculateFinGridConsumptionPriceService(
            ICsvReaderService csvReaderService,
            IElectricityRepository electricityRepository,
            IConsumptionDataProcessor consumptionDataProcessor,
            IConsumptionOptimizer consumptionOptimizer,
            ILogger<CalculateFinGridConsumptionPriceService> logger,
            IConfiguration configuration)
        {
            _csvReaderService = csvReaderService;
            _electricityRepository = electricityRepository;
            _consumptionDataProcessor = consumptionDataProcessor;
            _consumptionOptimizer = consumptionOptimizer;
            _logger = logger;
            _optimizePercentage = configuration.GetValue<decimal>("OptimizePercentage", 0.25M);
        }

        public enum PriceOption
        {
            FixedPrice,
            SpotPrice,
            Error
        }

        public async Task<ConsumptionPriceCalculationResult> CalculateTotalConsumptionPricesAsync(string csvFilePath, decimal? fixedPrice)
        {
            _logger.LogInformation("Start calculating total consumption prices.");

            if (string.IsNullOrEmpty(csvFilePath))
            {
                _logger.LogError("CSV file path is invalid or file does not exist: {csvFilePath}", csvFilePath);
                return GetDefaultResult();
            }

            try
            {
                _logger.LogInformation("Reading hourly consumption from CSV.");
                var hourlyConsumption = await _csvReaderService.ReadHourlyConsumptionAsync(csvFilePath).ConfigureAwait(false);

                if (hourlyConsumption == null || !hourlyConsumption.Any())
                {
                    _logger.LogWarning("No consumption data found in CSV.");
                    return GetDefaultResult();
                }

                var startDate = hourlyConsumption.Keys.Min();
                var endDate = hourlyConsumption.Keys.Max().AddHours(1);

                _logger.LogInformation("Fetching electricity prices.");
                var electricityPrices = await _electricityRepository.GetPricesForPeriodAsync(startDate, endDate).ConfigureAwait(false);

                if (electricityPrices == null || !electricityPrices.Any())
                {
                    _logger.LogError("No electricity prices found for the given period.");
                    return GetDefaultResult();
                }

                _logger.LogInformation("Processing consumption data.");
                var processedData = _consumptionDataProcessor.ProcessConsumptionData(hourlyConsumption, electricityPrices, fixedPrice);
       
                
                var cheaperOption = DetermineCheaperOption(processedData.TotalSpotPrice, processedData.TotalFixedPrice, fixedPrice);
                var priceDifference = Math.Abs(processedData.TotalSpotPrice - processedData.TotalFixedPrice) / 100;
                var equivalentFixedPrice = cheaperOption == PriceOption.SpotPrice ? processedData.TotalSpotPrice / processedData.TotalConsumption : 0;

                _logger.LogInformation("Optimizing consumption.");
                var optimizedConsumption = _consumptionOptimizer.OptimizeConsumption(hourlyConsumption, _optimizePercentage);
                var optimizedData = _consumptionDataProcessor.ProcessConsumptionData(optimizedConsumption, electricityPrices, fixedPrice);

                var optimizedPriceDifference = Math.Abs(optimizedData.TotalSpotPrice - processedData.TotalFixedPrice) / 100;

                _logger.LogInformation("Calculation completed successfully.");

                return new ConsumptionPriceCalculationResult
                {
                    TotalSpotPrice = processedData.TotalSpotPrice / 100,
                    TotalFixedPrice = processedData.TotalFixedPrice / 100,
                    CheaperOption = cheaperOption.ToString(),
                    TotalConsumption = processedData.TotalConsumption,
                    PriceDifference = priceDifference,
                    OptimizedPriceDifference = optimizedPriceDifference,
                    EquivalentFixedPrice = equivalentFixedPrice,
                    TotalOptimizedSpotPrice = optimizedData.TotalSpotPrice / 100,
                    MonthlyData = DataFormatter.FormatMonthlyData(processedData.MonthlyData),
                    WeeklyData = DataFormatter.FormatWeeklyData(processedData.WeeklyData),
                    DailyData = DataFormatter.FormatDailyData(processedData.DailyData),
                    StartDate = startDate,
                    EndDate = endDate
                };
            }
            catch (CsvReadingException ex)
            {
                _logger.LogError(ex, "Error reading CSV file.");
                return GetDefaultResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating electricity consumption price.");
                throw new CalculationException("An error occurred while calculating the consumption price.", ex);
            }
        }

        private PriceOption DetermineCheaperOption(decimal totalSpotPrice, decimal totalFixedPrice, decimal? fixedPrice)
        {
            try
            {
                if (totalFixedPrice == 0 || totalSpotPrice == 0)
                {
                    _logger.LogWarning("Either total fixed price or total spot price is zero, cannot determine the cheaper option.");
                    return PriceOption.Error;
                }

                var cheaperOption = fixedPrice.HasValue && totalFixedPrice < totalSpotPrice ? PriceOption.FixedPrice : PriceOption.SpotPrice;
                _logger.LogInformation("Cheaper option determined: {cheaperOption}", cheaperOption);
                return cheaperOption;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining the cheaper option.");
                return PriceOption.Error;
            }
        }

        private static ConsumptionPriceCalculationResult GetDefaultResult()
        {
            return new ConsumptionPriceCalculationResult
            {
                TotalSpotPrice = 0,
                TotalFixedPrice = 0,
                CheaperOption = "Error calculating data, or no data were found",
                TotalConsumption = 0,
                PriceDifference = 0,
                OptimizedPriceDifference = 0,
                EquivalentFixedPrice = 0,
                TotalOptimizedSpotPrice = 0,
                MonthlyData = new List<Dto.Consumption.Consumption.MonthlyConsumptionData>(),
                WeeklyData = new List<Dto.Consumption.Consumption.WeeklyConsumptionData>(),
                DailyData = new List<Dto.Consumption.Consumption.DailyConsumptionData>(),
                StartDate = default,
                EndDate = default
            };
        }
    }
    public class CalculationException : Exception
    {
        public CalculationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}