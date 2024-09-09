using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Globalization;

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

            var (totalSpotPrice, totalFixedPrice, cheaperOption, _, _, equivalentFixedPrice, _, _, _, _, _) = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);


            Assert.Equal(0, totalSpotPrice);
            Assert.Equal(0, totalFixedPrice);
            Assert.Equal("Error calculating data, or no data were found", cheaperOption);
            Assert.Equal(0, equivalentFixedPrice);
        }

        [Fact]
        public async Task CalculatePricesAsync_InvalidCsvFormat_EmptyResult()
        {
            
            var csvData = "Invalid data\n";
            var csvFilePath = "invaliddata_20230531_20230601.csv";

            await File.WriteAllTextAsync(csvFilePath, csvData);
            decimal fixedPrice = 0.20m;

            
            var (totalSpotPrice, totalFixedPrice, cheaperOption, _, _, equivalentFixedPrice, _, _, _, _, _) = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            Assert.Equal(0, totalSpotPrice);
            Assert.Equal(0, totalFixedPrice);
            Assert.Equal("Error calculating data, or no data were found", cheaperOption);
            Assert.Equal(0, equivalentFixedPrice);

            File.Delete(csvFilePath); 
        }

        [Fact]
        public async Task CalculateTotalConsumptionPricesAsync_ValidCsvFilePath_ReturnsCorrectResult()
        {
            
            var csvData = MockCsvData();
            var csvFilePath = "validdata_20240601_20240602.csv";
            await File.WriteAllTextAsync(csvFilePath, csvData);
            decimal fixedPrice = 7.5M;

            var (totalSpotPrice, totalFixedPrice, cheaperOption, totalConsumption, priceDifference, _, monthlyData, weeklyData, dailyData, _, _) = await _calculateFingridConsumptionPrice.CalculateTotalConsumptionPricesAsync(csvFilePath, fixedPrice);

            Assert.Equal(55.8M, totalSpotPrice);  
            Assert.Equal(31.5M, totalFixedPrice); 
            Assert.Equal("Fixed price", cheaperOption);
            Assert.Equal(420.0M, totalConsumption);
            Assert.Equal(24.3M, priceDifference);
            Assert.Single(monthlyData);
            Assert.Single(weeklyData);
            Assert.Single(dailyData);

            File.Delete(csvFilePath); 
        }
    }
}