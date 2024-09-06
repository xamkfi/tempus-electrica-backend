using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;


namespace TestProject.Infrastructure
{
    public class ElectricityRepositoryTests
    {
        [Fact]
        public async Task AddRangeElectricityPricesAsync_ValidInput_ReturnsTrue()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ElectricityDbContext>()
                .UseInMemoryDatabase(databaseName: "Test_AddRangeElectricityPricesAsync_ValidInput")
                .Options;

            var loggerMock = new Mock<ILogger<ElectricityRepository>>();
            var cacheMock = new Mock<IMemoryCache>();

            using (var context = new ElectricityDbContext(options))
            {
                var repository = new ElectricityRepository(context, loggerMock.Object, cacheMock.Object);

                var electricityPriceData = new List<ElectricityPriceData>
                {
                    new ElectricityPriceData { /* Initialize properties */ },
                    new ElectricityPriceData { /* Initialize properties */ },
                };

                // Act
                var result = await repository.AddRangeElectricityPricesAsync(electricityPriceData);

                // Assert
                Assert.True(result);
                Assert.True(context.ElectricityPriceDatas.Count() == 2);
            }
        }

        [Fact]
        public async Task AddRangeElectricityPricesAsync_ExceptionThrown_ReturnsFalse()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ElectricityDbContext>()
                .UseInMemoryDatabase(databaseName: "Test_AddRangeElectricityPricesAsync_ExceptionThrown")
                .Options;

            var loggerMock = new Mock<ILogger<ElectricityRepository>>();
            var cacheMock = new Mock<IMemoryCache>();

            using (var context = new ElectricityDbContext(options))
            {
                var repository = new ElectricityRepository(context, loggerMock.Object, cacheMock.Object);

                // Create an empty list
                var electricityPriceData = new List<ElectricityPriceData>();

                // Act
                var result = await repository.AddRangeElectricityPricesAsync(electricityPriceData);

                // Assert
                Assert.False(result);
                Assert.True(context.ElectricityPriceDatas.Count() == 0);
            }
        }

        [Fact]
        public async Task IsDuplicateAsync_ShouldReturnTrue_WhenDuplicateExists()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ElectricityDbContext>()
                .UseInMemoryDatabase(databaseName: "Test_IsDuplicateAsync_ShouldReturnTrue")
                .Options;

            var loggerMock = new Mock<ILogger<ElectricityRepository>>();
            var cacheMock = new Mock<IMemoryCache>();

            using (var context = new ElectricityDbContext(options))
            {
                var repository = new ElectricityRepository(context, loggerMock.Object, cacheMock.Object);

                var startDate = new DateTime(2023, 1, 1);
                var endDate = new DateTime(2023, 1, 2);
                context.ElectricityPriceDatas.Add(new ElectricityPriceData { StartDate = startDate, EndDate = endDate });
                await context.SaveChangesAsync();

                // Act
                var result = await repository.IsDuplicateAsync(startDate, endDate);

                // Assert
                Assert.True(result);
            }
        }

        [Fact]
        public async Task IsDuplicateAsync_ShouldReturnFalse_WhenDuplicateDoesNotExist()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ElectricityDbContext>()
                .UseInMemoryDatabase(databaseName: "Test_IsDuplicateAsync_ShouldReturnFalse")
                .Options;

            var loggerMock = new Mock<ILogger<ElectricityRepository>>();
            var cacheMock = new Mock<IMemoryCache>();

            using (var context = new ElectricityDbContext(options))
            {
                var repository = new ElectricityRepository(context, loggerMock.Object, cacheMock.Object);

                var startDate = new DateTime(2023, 1, 1);
                var endDate = new DateTime(2023, 1, 2);

                // Act
                var result = await repository.IsDuplicateAsync(startDate, endDate);

                // Assert
                Assert.False(result);
            }
        }
    }
}
