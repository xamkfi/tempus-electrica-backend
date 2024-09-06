using Moq;
using Domain.Interfaces;
using ApplicationLayer.Services;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace TestProject.Application
{
    public class ElectricityServiceTests
    {
        [Fact]
        public async Task AddElectricityPricesAsync_ValidInput_ReturnsTrue()
        {
            // Arrange
            var electricityRepositoryMock = new Mock<IElectricityRepository>();
            var loggerMock = new Mock<ILogger<ElectrictyService>>();  

            electricityRepositoryMock.Setup(repo => repo.AddRangeElectricityPricesAsync(It.IsAny<List<ElectricityPriceData>>()))
                                     .ReturnsAsync(true);

            var service = new ElectrictyService(electricityRepositoryMock.Object, loggerMock.Object);

            var electricityPriceDataDtoIn = new ApplicationLayer.Dto.ElectricityPriceDataDtoIn
            {
                Prices = new List<ApplicationLayer.Dto.PriceInfo>
                {
                    new ApplicationLayer.Dto.PriceInfo(){ EndDate = DateTime.Now.AddHours(1), StartDate = DateTime.Now, Price = 10 },
                    new ApplicationLayer.Dto.PriceInfo(){ EndDate = DateTime.Now.AddHours(2), StartDate = DateTime.Now.AddHours(1), Price = 10 }
                }
            };

            // Act
            var result = await service.AddElectricityPricesAsync(electricityPriceDataDtoIn);

            // Assert
            Assert.True(result);
            electricityRepositoryMock.Verify(repo => repo.AddRangeElectricityPricesAsync(It.IsAny<List<ElectricityPriceData>>()), Times.Once);
        }

        [Fact]
        public async Task AddElectricityPricesAsync_ExceptionThrown_ReturnsFalse()
        {
            // Arrange
            var electricityRepositoryMock = new Mock<IElectricityRepository>();
            var loggerMock = new Mock<ILogger<ElectrictyService>>();  
            electricityRepositoryMock.Setup(repo => repo.AddRangeElectricityPricesAsync(It.IsAny<List<ElectricityPriceData>>()))
                                     .ThrowsAsync(new Exception("Simulated exception"));

            var service = new ElectrictyService(electricityRepositoryMock.Object, loggerMock.Object);

            var electricityPriceDataDtoIn = new ApplicationLayer.Dto.ElectricityPriceDataDtoIn
            {
                Prices = new List<ApplicationLayer.Dto.PriceInfo>
                {
                    new ApplicationLayer.Dto.PriceInfo(){ EndDate = DateTime.Now.AddHours(1), StartDate = DateTime.Now, Price = 10 },
                    new ApplicationLayer.Dto.PriceInfo(){ EndDate = DateTime.Now.AddHours(2), StartDate = DateTime.Now.AddHours(1), Price = 10 }
                }
            };

            // Act
            var result = await service.AddElectricityPricesAsync(electricityPriceDataDtoIn);

            // Assert
            Assert.False(result);
            electricityRepositoryMock.Verify(repo => repo.AddRangeElectricityPricesAsync(It.IsAny<List<ElectricityPriceData>>()), Times.Once);
        }
    }
}
