using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Dto;
using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

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
                new ElectricityPriceData { StartDate = new DateTime(2022, 1, 1), EndDate = new DateTime(2022, 2, 1), Price = 431.45M },
            };

            var electricityRepositoryMock = new Mock<IElectricityRepository>();
            electricityRepositoryMock.Setup(repo => repo.GetPricesForPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(electricityPriceData);

            var loggerMock = new Mock<ILogger<ElectricityPriceService>>();

            var electricityPriceService = new ElectricityPriceService(electricityRepositoryMock.Object, loggerMock.Object);

            // Act
            var result = await electricityPriceService.GetElectricityPriceDataAsync(request);

            // Assert
            Assert.Equal(1019.48M, result.totalFixedPriceCost);
            Assert.Equal(12743.5M, result.totalConsumption);
        }

        [Fact]
        public async Task GetElectricityPriceDataAsync_ReturnsValidationError_WhenYearIsMissing()
        {
            // Arrange
            var request = new CombinedRequestDtoIn
            {
                FixedPrice = 8.00M,
                HouseType = HouseType.DetachedHouse,
                SquareMeters = 100,
            };

            var electricityRepositoryMock = new Mock<IElectricityRepository>();
            var loggerMock = new Mock<ILogger<ElectricityPriceService>>();

            var electricityPriceService = new ElectricityPriceService(electricityRepositoryMock.Object, loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => electricityPriceService.GetElectricityPriceDataAsync(request));
        }
    }
}