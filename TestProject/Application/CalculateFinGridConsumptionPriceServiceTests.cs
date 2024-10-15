using ApplicationLayer.Dto;
using ApplicationLayer.Dto.Consumption.Consumption;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace TestProject.Application
{
    public class CalculateFinGridConsumptionPriceServiceTests
    {
        private readonly ICalculateFingridConsumptionPrice _calculateFingridConsumptionPrice;
        private readonly Mock<IElectricityPriceService> _electricityPriceServiceMock;
        private readonly Mock<ICsvReaderService> _csvReaderServiceMock;
        private readonly Mock<IConsumptionDataProcessor> _consumptionDataProcessorMock;
        private readonly Mock<IConsumptionOptimizer> _consumptionOptimizerMock;
        private readonly Mock<ILogger<CalculateFinGridConsumptionPriceService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;

        public CalculateFinGridConsumptionPriceServiceTests()
        {
            // Mock dependencies
            _csvReaderServiceMock = new Mock<ICsvReaderService>();
            _electricityPriceServiceMock = new Mock<IElectricityPriceService>();
            _consumptionDataProcessorMock = new Mock<IConsumptionDataProcessor>();
            _consumptionOptimizerMock = new Mock<IConsumptionOptimizer>();
            _loggerMock = new Mock<ILogger<CalculateFinGridConsumptionPriceService>>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup configuration mock
            _configurationMock.Setup(config => config.GetValue<decimal>("OptimizePercentage", It.IsAny<decimal>())).Returns(0.25M);

            // Setup electricity price service mock
            _electricityPriceServiceMock
                .Setup(service => service.GetElectricityPricesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<ElectricityPriceData>
                {
                    new ElectricityPriceData { StartDate = new DateTime(2024, 6, 20, 10, 0, 0), EndDate = new DateTime(2024, 6, 20, 11, 0, 0), Price = 14.2M },
                    new ElectricityPriceData { StartDate = new DateTime(2024, 6, 20, 11, 0, 0), EndDate = new DateTime(2024, 6, 20, 12, 0, 0), Price = 12M },
                    new ElectricityPriceData { StartDate = new DateTime(2024, 6, 20, 12, 0, 0), EndDate = new DateTime(2024, 6, 20, 13, 0, 0), Price = 13M }
                });

            // Instantiate the service with mocked dependencies
            _calculateFingridConsumptionPrice = new CalculateFinGridConsumptionPriceService(
                _csvReaderServiceMock.Object,
                _electricityPriceServiceMock.Object,
                _consumptionDataProcessorMock.Object,
                _consumptionOptimizerMock.Object,
                _loggerMock.Object,
                _configurationMock.Object);
        }

        public static string MockCsvData()
        {
            var culture = CultureInfo.CreateSpecificCulture("en-US");
            var dateTimeFormat = "yyyy-MM-ddTHH:mm:ssZ";

            return "MeteringPointGSRN;Product Type;Resolution;Unit Type;Reading Type;Start Time;Quantity;Quality\n" +
                   "643000000000000000;872000000000;PT15M;kWh;BN01;" + DateTime.Parse("2024-06-20T07:00:00Z", culture, DateTimeStyles.AdjustToUniversal).ToString(dateTimeFormat) + ";200.0;OK\n" +
                   "643000000000000000;872000000000;PT15M;kWh;BN01;" + DateTime.Parse("2024-06-20T08:00:00Z", culture, DateTimeStyles.AdjustToUniversal).ToString(dateTimeFormat) + ";120.0;OK\n" +
                   "643000000000000000;872000000000;PT15M;kWh;BN01;" + DateTime.Parse("2024-06-20T09:00:00Z", culture, DateTimeStyles.AdjustToUniversal).ToString(dateTimeFormat) + ";100.0;OK\n";
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
            var csvData = "Invalid data\n";
            var csvFilePath = "invaliddata_20230531_20230601.csv";
            await File.WriteAllTextAsync(csvFilePath, csvData);
            decimal fixedPrice = 0.20m;

            // Mock the CSV reader service to return empty data due to invalid format
            _csvReaderServiceMock.Setup(service => service.ReadHourlyConsumptionAsync(csvFilePath))
                .ThrowsAsync(new CsvReadingException("Invalid CSV format.", null));

            // Act
            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            // Assert
            Assert.Equal(0, result.TotalSpotPrice);
            Assert.Equal(0, result.TotalFixedPrice);
            Assert.Equal("Error calculating data, or no data were found", result.CheaperOption);
            Assert.Equal(0, result.EquivalentFixedPrice);

            // Cleanup
            File.Delete(csvFilePath);
        }

        [Fact]
        public async Task CalculateTotalConsumptionPricesAsync_ValidCsvFilePath_ReturnsCorrectResult()
        {
            // Arrange
            var csvData = MockCsvData();
            var csvFilePath = "validdata_20240601_20240602.csv";
            await File.WriteAllTextAsync(csvFilePath, csvData);
            decimal fixedPrice = 7.5M;

            // Mock the CSV reader service to return the expected hourly consumption
            var hourlyConsumption = new ConcurrentDictionary<DateTime, decimal>
            {
                [new DateTime(2024, 6, 20, 7, 0, 0)] = 200.0M,
                [new DateTime(2024, 6, 20, 8, 0, 0)] = 120.0M,
                [new DateTime(2024, 6, 20, 9, 0, 0)] = 100.0M,
            };

            _csvReaderServiceMock.Setup(service => service.ReadHourlyConsumptionAsync(csvFilePath))
                .ReturnsAsync(hourlyConsumption);

            // Mock the consumption data processor to return processed data
            var processedData = new ProcessedCsvDataResult
            {
                TotalSpotPrice = 55.8M * 100, // Since the service divides by 100
                TotalFixedPrice = 31.5M * 100,
                TotalConsumption = 420.0M,
                MonthlyData = new Dictionary<(int Month, int Year), MonthlyConsumptionData>
                {
                    {
                        (6, 2024),
                        new MonthlyConsumptionData
                        {
                            Month = 6,
                            Year = 2024,
                            Consumption = 420.0M,
                            SpotPrice = 55.8M,
                            FixedPrice = 31.5M
                        }
                    }
                },
                WeeklyData = new Dictionary<(int Week, int Year), WeeklyConsumptionData>
                {
                    {
                        (25, 2024),
                        new WeeklyConsumptionData
                        {
                            Week = 25,
                            Year = 2024,
                            Consumption = 420.0M,
                            SpotPrice = 55.8M,
                            FixedPrice = 31.5M
                        }
                    }
                },
                DailyData = new Dictionary<DateTime, DailyConsumptionData>
                {
                    {
                        new DateTime(2024, 6, 20),
                        new DailyConsumptionData
                        {
                            Day = "20.6.2024",
                            Consumption = 420.0M,
                            SpotPrice = 55.8M,
                            FixedPrice = 31.5M
                        }
                    }
                }
            };

            _consumptionDataProcessorMock
                .Setup(processor => processor.ProcessConsumptionData(hourlyConsumption, It.IsAny<List<ElectricityPriceData>>(), fixedPrice))
                .Returns(processedData);

            // Mock the consumption optimizer to return the same consumption (no optimization)
            _consumptionOptimizerMock
                .Setup(optimizer => optimizer.OptimizeConsumption(hourlyConsumption, It.IsAny<decimal>()))
                .Returns(hourlyConsumption);

            // Act
            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            // Assert
            Assert.Equal(55.8M, result.TotalSpotPrice);
            Assert.Equal(31.5M, result.TotalFixedPrice);
            Assert.Equal("FixedPrice", result.CheaperOption);
            Assert.Equal(420.0M, result.TotalConsumption);
            Assert.Equal(24.3M, result.PriceDifference);
            Assert.Single(result.MonthlyData);
            Assert.Single(result.WeeklyData);
            Assert.Single(result.DailyData);

            // Cleanup
            File.Delete(csvFilePath);
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
}
