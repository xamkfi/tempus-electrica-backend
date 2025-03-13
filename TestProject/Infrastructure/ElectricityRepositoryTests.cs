using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;


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

        //try living without logging -- good luck :)
        private readonly ITestOutputHelper _output;

        public ElectricityRepositoryTests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Theory]
        [InlineData("2025-01-01", "2025-01-02",  // First fetch dates
           "2025-01-01", "2025-01-04",    // Second fetch dates
           "2025-01-01", "2025-01-02")]   // Third fetch dates - original test case
        [InlineData("2025-02-03", "2025-02-06",   // First fetch: endDate - 3 days
           "2025-02-04", "2025-02-06",    // Second fetch: endDate - 2 days
           "2025-02-04", "2025-02-05")]   // Third fetch: endDate - 1 day (and endDate is one day less)
        public async Task NoDuplicateDataFetched(
        string firstStartDate, string firstEndDate,
        string secondStartDate, string secondEndDate,
        string thirdStartDate, string thirdEndDate)
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ElectricityDbContext>()
                        .UseInMemoryDatabase(databaseName: "NoDuplicatedDB")
                        .Options;
            var loggerMock = new Mock<ILogger<ElectricityRepository>>();

            //real cache is required, otherwise doesn't work
            var cache = new MemoryCache(new MemoryCacheOptions());

            using (var context = new ElectricityDbContext(options))
            {
                //populating the in-memory database -- real db would be preferable
                var testData = new List<ElectricityPriceData>
                {
                    // Data for January 2025
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 01, 01, 0, 0, 0),
                        EndDate = new DateTime(2025, 01, 01, 23, 0, 0),
                        Price = 10.50M,
                    },
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 01, 02, 0, 0, 0),

                        EndDate = new DateTime(2025, 01, 02, 23, 0, 0),
                        Price = 11.25M,
                    },
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 01, 03, 0, 0, 0),

                        EndDate = new DateTime(2025, 01, 03, 23, 0, 0),
                        Price = 10.75M,
                    },
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 01, 04, 0, 0, 0),

                        EndDate = new DateTime(2025, 01, 04, 23, 0, 0),
                        Price = 11.00M,
                    },

                    // Data for February 2025
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 02, 03, 0, 0, 0),

                        EndDate = new DateTime(2025, 02, 03, 23, 0, 0),
                        Price = 11.50M,
                    },
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 02, 04, 0, 0, 0),

                        EndDate = new DateTime(2025, 02, 04, 23, 0, 0),
                        Price = 11.75M,
                    },
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 02, 05, 0, 0, 0),

                        EndDate = new DateTime(2025, 02, 05, 23, 0, 0),
                        Price = 11.25M,
                    },
                    new ElectricityPriceData
                    {
                        StartDate = new DateTime(2025, 02, 06, 0, 0, 0),

                        EndDate = new DateTime(2025, 02, 06, 23, 0, 0),
                        Price= 11.00M,
                    }
                };

                await context.ElectricityPriceDatas.AddRangeAsync(testData);
                await context.SaveChangesAsync();

                //Act
                var repository = new ElectricityRepository(context, loggerMock.Object, cache);

                // First fetch
                var cachedElectricityData = await repository.GetPricesForPeriodAsync(
                    DateTime.Parse(firstStartDate),
                    DateTime.Parse(firstEndDate));
                _output.WriteLine("First fetch result:");
                foreach (var data in cachedElectricityData)
                {
                    _output.WriteLine($"StartDate: {data.StartDate}, EndDate: {data.EndDate}, Price: {data.Price}");
                }
                Assert.NotEmpty(cachedElectricityData);
                var firstFetchCount = cachedElectricityData.Count();

                // Second fetch
                cachedElectricityData = await repository.GetPricesForPeriodAsync(
                    DateTime.Parse(secondStartDate),
                    DateTime.Parse(secondEndDate));
                _output.WriteLine("Second fetch result:");
                foreach (var data in cachedElectricityData)
                {
                    _output.WriteLine($"StartDate: {data.StartDate}, EndDate: {data.EndDate}, Price: {data.Price}");
                }

                // Third fetch
                cachedElectricityData = await repository.GetPricesForPeriodAsync(
                    DateTime.Parse(thirdStartDate),
                    DateTime.Parse(thirdEndDate));
                _output.WriteLine("Third fetch result:");
                foreach (var data in cachedElectricityData)
                {
                    _output.WriteLine($"StartDate: {data.StartDate}, EndDate: {data.EndDate}, Price: {data.Price}");
                }
                var lastFetchCount = cachedElectricityData.Count();

                //Assert
                //there are only 24 hours in a day, if duplicates exist, then value should be above 24
                Assert.InRange(lastFetchCount, 0, 24);
            }
        }
    }
}