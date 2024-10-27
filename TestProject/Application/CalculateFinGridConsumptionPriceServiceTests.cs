using ApplicationLayer.Dto;
using ApplicationLayer.Dto.Consumption.Consumption;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System.Collections.Concurrent;

namespace TestProject.Application
{
    public class CalculateFinGridConsumptionPriceServiceTests
    {
        private Mock<ICsvReaderService> _csvReaderServiceMock;
        private Mock<IElectricityRepository> _electricityRepositoryMock;
        private Mock<IConsumptionDataProcessor> _consumptionDataProcessorMock;
        private Mock<IConsumptionOptimizer> _consumptionOptimizerMock;
        private Mock<ILogger<CalculateFinGridConsumptionPriceService>> _loggerMock;
        private CalculateFinGridConsumptionPriceService _calculateFingridConsumptionPrice;

        public CalculateFinGridConsumptionPriceServiceTests()
        {
            // Initialize mocks
            _csvReaderServiceMock = new Mock<ICsvReaderService>();
            _electricityRepositoryMock = new Mock<IElectricityRepository>();
            _consumptionDataProcessorMock = new Mock<IConsumptionDataProcessor>();
            _consumptionOptimizerMock = new Mock<IConsumptionOptimizer>();
            _loggerMock = new Mock<ILogger<CalculateFinGridConsumptionPriceService>>();

            // Use a dictionary to simulate IConfiguration
            var inMemorySettings = new Dictionary<string, string>
        {
            {"OptimizePercentage", "0.25"} 
        };

            // Create the custom configuration
            var testConfiguration = new TestConfiguration(inMemorySettings);

            // Create the service instance with mocked dependencies
            _calculateFingridConsumptionPrice = new CalculateFinGridConsumptionPriceService(
                _csvReaderServiceMock.Object,
                _electricityRepositoryMock.Object,
                _consumptionDataProcessorMock.Object,
                _consumptionOptimizerMock.Object,
                _loggerMock.Object,
                testConfiguration // Use the custom configuration
            );
        }


        


        [Fact]
        public async Task CalculatePricesAsync_MissingCsvData_EmptyResult()
        {
            // Arrange
            var csvFilePath = "nonexistentfile.csv";
            decimal fixedPrice = 0.20m;

            // Act
            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            // Assert
            Assert.Equal(0, result.TotalSpotPrice);
            Assert.Equal(0, result.TotalFixedPrice);
            Assert.Equal("Error calculating data, or no data were found", result.CheaperOption);
            Assert.Equal(0, result.EquivalentFixedPrice);
        }

        [Fact]
        public async Task CalculatePricesAsync_InvalidCsvFormat_EmptyResult()
        {
            // Arrange
            var csvFilePath = "invaliddata_20230531_20230601.csv";
            decimal fixedPrice = 0.20m;

            // Mock the CSV reader service to throw CsvReadingException for invalid format
            _csvReaderServiceMock.Setup(service => service.ReadHourlyConsumptionAsync(csvFilePath))
                .ThrowsAsync(new CsvReadingException("Invalid CSV format.", null));

            // Act
            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            // Assert
            Assert.Equal(0, result.TotalSpotPrice);
            Assert.Equal(0, result.TotalFixedPrice);
            Assert.Equal("Error calculating data, or no data were found", result.CheaperOption);
            Assert.Equal(0, result.EquivalentFixedPrice);
        }
        [Fact]
        public async Task CalculateTotalConsumptionPricesAsync_ShouldCalculateCorrectSpotPrice()
        {
            // Arrange
            var csvFilePath = "validdata_20240601_20240602.csv";
            decimal fixedPrice = 7.5M;

            var hourlyConsumption = new ConcurrentDictionary<DateTime, decimal>
            {
                [new DateTime(2024, 6, 20, 13, 0, 0)] = 100,  // 100 kWh consumption at 13:00
                [new DateTime(2024, 6, 20, 14, 0, 0)] = 200,  // 200 kWh consumption at 14:00
                [new DateTime(2024, 6, 20, 15, 0, 0)] = 150   // 150 kWh consumption at 15:00
            };

            // Mock the CsvReaderService to return predefined consumption data
            _csvReaderServiceMock.Setup(service => service.ReadHourlyConsumptionAsync(csvFilePath))
                .ReturnsAsync(hourlyConsumption);

            // Set up the mock to return prices for the relevant time period
            DateTime startDate = new DateTime(2024, 6, 20, 13, 0, 0);
            DateTime endDate = new DateTime(2024, 6, 20, 16, 0, 0);
            _electricityRepositoryMock.Setup(repo => repo.GetPricesForPeriodAsync(startDate, endDate))
                .ReturnsAsync(new List<ElectricityPriceData>
                {
            new ElectricityPriceData { StartDate = startDate, EndDate = startDate.AddHours(1), Price = 14.2M },
            new ElectricityPriceData { StartDate = startDate.AddHours(1), EndDate = startDate.AddHours(2), Price = 13M },
            new ElectricityPriceData { StartDate = startDate.AddHours(2), EndDate = startDate.AddHours(3), Price = 13M },
                });

            // Mock the ProcessConsumptionData method
            _consumptionDataProcessorMock.Setup(processor => processor.ProcessConsumptionData(
                    It.IsAny<ConcurrentDictionary<DateTime, decimal>>(),
                    It.IsAny<IEnumerable<ElectricityPriceData>>(),
                    fixedPrice))
                .Returns(new ProcessedCsvDataResult
                {
                    TotalSpotPrice = 51.8M,  // Total spot price calculation: 100 * 14.2 + 200 * 13 + 150 * 13
                    TotalFixedPrice = 33.75M, // Fixed price calculation: 450 kWh * 7.5 cents
                    TotalConsumption = 450M,  // Total consumption: 100 + 200 + 150
                    MonthlyData = new Dictionary<(int, int), MonthlyConsumptionData>(),
                    WeeklyData = new Dictionary<(int, int), WeeklyConsumptionData>(),
                    DailyData = new Dictionary<DateTime, DailyConsumptionData>()
                });

            // Act
            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            // Assert
            Assert.NotNull(result);
            //CalculateTotalConsumptionPricesAsync always returns prices as eur
            Assert.Equal(0.518M, result.TotalSpotPrice); //0.518 eur or 51.8 cents
            Assert.Equal(0.3375M, result.TotalFixedPrice); //0.3375 eur or 33.75 cents
            Assert.Equal("FixedPrice", result.CheaperOption);
        }





        [Fact]
        public void OptimizeConsumption_ShouldMove25PercentOfAfternoonConsumptionToMorning()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ConsumptionOptimizer>>();
            var consumptionOptimizer = new ConsumptionOptimizer(loggerMock.Object);

            var hourlyConsumption = new ConcurrentDictionary<DateTime, decimal>
            {
                // Afternoon consumption
                [new DateTime(2024, 9, 16, 13, 0, 0)] = 100, // 25% = 25
                [new DateTime(2024, 9, 16, 14, 0, 0)] = 200, // 25% = 50
                [new DateTime(2024, 9, 16, 15, 0, 0)] = 300, // 25% = 75

                // Morning consumption (should receive the 25% moved from afternoon)
                [new DateTime(2024, 9, 17, 1, 0, 0)] = 0,
                [new DateTime(2024, 9, 17, 2, 0, 0)] = 0,
                [new DateTime(2024, 9, 17, 3, 0, 0)] = 0
            };

            decimal optimizePercentage = 0.25M;

            // Act
            var optimizedConsumption = consumptionOptimizer.OptimizeConsumption(hourlyConsumption, optimizePercentage);

            // Assert
            // Verify that 25% of the consumption from 13:00-15:00 has been moved to the morning period (01:00-03:00)
            Assert.Equal(25, optimizedConsumption[new DateTime(2024, 9, 17, 1, 0, 0)]);   // 25% from 13:00
            Assert.Equal(50, optimizedConsumption[new DateTime(2024, 9, 17, 2, 0, 0)]);   // 25% from 14:00
            Assert.Equal(75, optimizedConsumption[new DateTime(2024, 9, 17, 3, 0, 0)]);   // 25% from 15:00

            // Verify that the afternoon values have been reduced by the moved amounts
            Assert.Equal(75, optimizedConsumption[new DateTime(2024, 9, 16, 13, 0, 0)]);   // 100 - 25
            Assert.Equal(150, optimizedConsumption[new DateTime(2024, 9, 16, 14, 0, 0)]);  // 200 - 50
            Assert.Equal(225, optimizedConsumption[new DateTime(2024, 9, 16, 15, 0, 0)]);  // 300 - 75
        }
    }
    public class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string> _settings;

        public TestConfiguration(Dictionary<string, string> settings)
        {
            _settings = settings;
        }

        public string this[string key]
        {
            get => _settings.TryGetValue(key, out var value) ? value : null;
            set => _settings[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return Enumerable.Empty<IConfigurationSection>(); // Return an empty list as a default implementation
        }

        public IChangeToken GetReloadToken()
        {
            return new CancellationChangeToken(new CancellationToken()); // Return a no-op change token
        }

        public IConfigurationSection GetSection(string key)
        {
            return new TestConfigurationSection(_settings, key); // Use a custom section for the given key
        }

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (_settings.TryGetValue(key, out var value) &&
                value != null &&
                Convert.ChangeType(value, typeof(T)) is T result)
            {
                return result;
            }
            return defaultValue;
        }
    }

    // Custom IConfigurationSection implementation
    public class TestConfigurationSection : IConfigurationSection
    {
        private readonly Dictionary<string, string> _settings;
        private readonly string _key;

        public TestConfigurationSection(Dictionary<string, string> settings, string key)
        {
            _settings = settings;
            _key = key;
        }

        public string this[string key] // This needs to have a setter
        {
            get => _settings.TryGetValue($"{_key}:{key}", out var value) ? value : null;
            set => _settings[$"{_key}:{key}"] = value; // Implement the setter
        }

        public string Key => _key;

        public string Path => _key;

        public string Value
        {
            get => _settings.TryGetValue(_key, out var value) ? value : null;
            set => _settings[_key] = value; // Not commonly used in tests, but added for completeness
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken() => new CancellationChangeToken(new CancellationToken());

        public IConfigurationSection GetSection(string key) // Implement this method
        {
            return new TestConfigurationSection(_settings, $"{_key}:{key}"); // Create a new section based on the key
        }
    }

}
