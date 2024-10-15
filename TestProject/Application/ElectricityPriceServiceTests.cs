using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Dto;
using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.Options;
using ApplicationLayer.Dto.Consumption;

namespace TestProject.Application
{
    public class ElectricityPriceServiceTests
    {
        [Fact]
        public async Task GetElectricityPriceDataAsync_ReturnsCorrectResults()
        {
            // Arrange
            var request = new CombinedRequestDtoIn
            {
                Year = 2023,
                FixedPrice = 8.00M,
                HouseType = HouseType.DetachedHouse,
                SquareMeters = 100,
                WorkShiftType = WorkShiftType.DayWorker,
                HeatingType = HeatingType.ElectricHeating,
                HasElectricCar = true,
                HasSauna = true,
                SaunaHeatingFrequency = 1,
                HasFireplace = true,
                FireplaceFrequency = 2,
                NumberOfCars = 1,
                NumberOfResidents = 3,
                ElectricCarkWhUsagePerYear = 2800,
                HasSolarPanel = true,
                SolarPanel = 15
            };

            var electricityPriceData = new List<ElectricityPriceData>
            {
                new ElectricityPriceData
                {
                    StartDate = new DateTime(2022, 1, 1),
                    EndDate = new DateTime(2022, 1, 2),
                    Price = 431.45M
                },
            };

            var electricityRepositoryMock = new Mock<IElectricityRepository>();
            electricityRepositoryMock
                .Setup(repo => repo.GetPricesForPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(electricityPriceData);

            var loggerMock = new Mock<ILogger<ElectricityPriceService>>();

            // Create ConsumptionSettings with default or test-specific values
            var consumptionSettings = new ConsumptionSettings
            {
                SavingsPerFireplaceUse = 8M,
                FloorHeatingConsumptionPerSquareMeter = 100M,
                MonthlyWeights = new Dictionary<int, decimal>
                {
                    { 1, 0.12M }, { 2, 0.10M }, { 3, 0.09M }, { 4, 0.08M },
                    { 5, 0.07M }, { 6, 0.06M }, { 7, 0.06M }, { 8, 0.06M },
                    { 9, 0.07M }, { 10, 0.09M }, { 11, 0.10M }, { 12, 0.10M }
                },
                MonthlyProductionPerPanel = new Dictionary<int, decimal>
                {
                    { 1, 6.3M }, { 2, 15.5M }, { 3, 33.6M }, { 4, 41.7M },
                    { 5, 51.3M }, { 6, 49.5M }, { 7, 49.1M }, { 8, 42.7M },
                    { 9, 29.6M }, { 10, 18.0M }, { 11, 6.4M }, { 12, 3.1M }
                },
                WorkShiftWeights = new Dictionary<WorkShiftType, Dictionary<int, decimal>>
                {
                    { WorkShiftType.DayWorker, new Dictionary<int, decimal>
                        {
                            // Hourly weights for DayWorker
                            { 0, 0.02M }, { 1, 0.02M }, { 2, 0.02M }, { 3, 0.02M }, { 4, 0.02M },
                            { 5, 0.02M }, { 6, 0.03M }, { 7, 0.03M }, { 8, 0.03M }, { 9, 0.03M },
                            { 10, 0.03M }, { 11, 0.03M }, { 12, 0.03M }, { 13, 0.03M }, { 14, 0.03M },
                            { 15, 0.03M }, { 16, 0.03M }, { 17, 0.05M }, { 18, 0.07M }, { 19, 0.08M },
                            { 20, 0.08M }, { 21, 0.07M }, { 22, 0.05M }, { 23, 0.04M }
                        }
                    },
                    // Add other WorkShiftTypes if needed
                }
            };

            var options = Options.Create(consumptionSettings);

            var electricityPriceService = new ElectricityPriceService(
                electricityRepositoryMock.Object,
                loggerMock.Object,
                options);

            // Act
            var result = await electricityPriceService.GetElectricityPriceDataAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Fixed price", result.CheaperOption);
            Assert.Equal(8.00M, request.FixedPrice);
            Assert.Equal(2023, request.Year);

            Assert.Equal(1068.12M, result.TotalFixedPriceCost);
            Assert.Equal(13351.5M, result.AverageConsumption);
        }

        [Fact]
        public async Task GetElectricityPriceDataAsync_ThrowsValidationException_WhenYearIsMissing()
        {
            // Arrange
            var request = new CombinedRequestDtoIn
            {
                FixedPrice = 8.00M,
                HouseType = HouseType.DetachedHouse,
                SquareMeters = 100,
                // Year is missing
            };

            var electricityRepositoryMock = new Mock<IElectricityRepository>();
            var loggerMock = new Mock<ILogger<ElectricityPriceService>>();

            var consumptionSettings = new ConsumptionSettings
            {
                // Initialize settings as needed
            };

            var options = Options.Create(consumptionSettings);

            var electricityPriceService = new ElectricityPriceService(
                electricityRepositoryMock.Object,
                loggerMock.Object,
                options);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => electricityPriceService.GetElectricityPriceDataAsync(request));
        }
    }
}
