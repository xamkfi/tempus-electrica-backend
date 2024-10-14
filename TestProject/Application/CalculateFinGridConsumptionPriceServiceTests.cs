using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Globalization;
using ApplicationLayer.Dto;

namespace TestProject.Application
{
    public class CalculateFinGridConsumptionPriceServiceTests
    {
        private readonly ICalculateFingridConsumptionPrice _calculateFingridConsumptionPrice;
        private readonly Mock<IElectricityRepository> _electricityRepositoryMock;
        private readonly Mock<ILogger<CalculateFinGridConsumptionPriceService>> _loggerMock;

        public CalculateFinGridConsumptionPriceServiceTests()
        {
            _electricityRepositoryMock = new Mock<IElectricityRepository>();
            _loggerMock = new Mock<ILogger<CalculateFinGridConsumptionPriceService>>();
            _electricityRepositoryMock.Setup(repo => repo.GetPricesForPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<ElectricityPriceData>
            {
                new ElectricityPriceData { StartDate = new DateTime(2024, 6, 20, 10, 0, 0), EndDate = new DateTime(2024, 6, 20, 11, 0, 0), Price = 14.2M },
                new ElectricityPriceData { StartDate = new DateTime(2024, 6, 20, 11, 0, 0), EndDate = new DateTime(2024, 6, 20, 12, 0, 0), Price = 12M },
                new ElectricityPriceData { StartDate = new DateTime(2024, 6, 20, 12, 0, 0), EndDate = new DateTime(2024, 6, 20, 13, 0, 0), Price = 13M }
            });

            _calculateFingridConsumptionPrice = new CalculateFinGridConsumptionPriceService(_electricityRepositoryMock.Object, _loggerMock.Object);
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
            var csvFilePath = "nonexistentfile.csv";
            decimal fixedPrice = 0.20m;

            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            Assert.Equal(0, result.TotalSpotPrice);
            Assert.Equal(0, result.TotalFixedPrice);
            Assert.Equal("Error calculating data, or no data were found", result.CheaperOption);
            Assert.Equal(0, result.EquivalentFixedPrice);
        }

        [Fact]
        public async Task CalculatePricesAsync_InvalidCsvFormat_EmptyResult()
        {
            var csvData = "Invalid data\n";
            var csvFilePath = "invaliddata_20230531_20230601.csv";

            await File.WriteAllTextAsync(csvFilePath, csvData);
            decimal fixedPrice = 0.20m;

            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            Assert.Equal(0, result.TotalSpotPrice);
            Assert.Equal(0, result.TotalFixedPrice);
            Assert.Equal("Error calculating data, or no data were found", result.CheaperOption);
            Assert.Equal(0, result.EquivalentFixedPrice);

            File.Delete(csvFilePath);
        }

        [Fact]
        public async Task CalculateTotalConsumptionPricesAsync_ValidCsvFilePath_ReturnsCorrectResult()
        {
            var csvData = MockCsvData();
            var csvFilePath = "validdata_20240601_20240602.csv";
            await File.WriteAllTextAsync(csvFilePath, csvData);
            decimal fixedPrice = 7.5M;

            var result = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            Assert.Equal(55.8M, result.TotalSpotPrice);
            Assert.Equal(31.5M, result.TotalFixedPrice);
            Assert.Equal("FixedPrice", result.CheaperOption);
            Assert.Equal(420.0M, result.TotalConsumption);
            Assert.Equal(24.3M, result.PriceDifference);
            Assert.Single(result.MonthlyData);
            Assert.Single(result.WeeklyData);
            Assert.Single(result.DailyData);

            File.Delete(csvFilePath);
        }

        [Fact]
        public void OptimizeConsumption_ShouldMove25PercentOfAfternoonConsumptionToMorning()
        {
            //Arrange
            var loggerMock = new Mock<ILogger<CalculateFinGridConsumptionPriceService>>();
            var service = new CalculateFinGridConsumptionPriceService(_electricityRepositoryMock.Object, loggerMock.Object);

            var hourlyConsumption = new ConcurrentDictionary<DateTime, decimal>
            {
                [new DateTime(2024, 9, 16, 13, 0, 0)] = 100, // 25% = 25
                [new DateTime(2024, 9, 16, 14, 0, 0)] = 200, // 25% = 50
                [new DateTime(2024, 9, 16, 15, 0, 0)] = 300, // 25% = 75
                [new DateTime(2024, 9, 17, 01, 0, 0)] = 0,
                [new DateTime(2024, 9, 17, 02, 0, 0)] = 0,
                [new DateTime(2024, 9, 17, 03, 0, 0)] = 0
            };

            //Act
            var optimizedConsumption = service.OptimizeConsumption(hourlyConsumption);

            //Assert
            Assert.Equal(25, optimizedConsumption[new DateTime(2024, 9, 17, 01, 0, 0)]); // 25% from 13:00
            Assert.Equal(50, optimizedConsumption[new DateTime(2024, 9, 17, 02, 0, 0)]); // 25% from 14:00
            Assert.Equal(75, optimizedConsumption[new DateTime(2024, 9, 17, 03, 0, 0)]); // 25% from 15:00
            Assert.Equal(75, optimizedConsumption[new DateTime(2024, 9, 16, 13, 0, 0)]); // 100 - 25
            Assert.Equal(150, optimizedConsumption[new DateTime(2024, 9, 16, 14, 0, 0)]); // 200 - 50
            Assert.Equal(225, optimizedConsumption[new DateTime(2024, 9, 16, 15, 0, 0)]); // 300 - 75
        }
    }
}
