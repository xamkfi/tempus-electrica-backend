using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace ApplicationLayer.Tests
{
    public class SaveHistoryDataServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IElectricityRepository> _electricityRepositoryMock;
        private readonly ILogger<SaveHistoryDataService> _logger;

        public SaveHistoryDataServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _electricityRepositoryMock = new Mock<IElectricityRepository>();
            _logger = new LoggerFactory().CreateLogger<SaveHistoryDataService>();
        }

        [Fact]
        public async Task LoadDataAsync_ShouldLoadDataSuccessfully()
        {
            // Arrange
            var apiResponse = new
            {
                prices = new List<ElectricityPriceData>
                {
                    new ElectricityPriceData
                    {
                        StartDate = DateTime.UtcNow.AddHours(-2),
                        EndDate = DateTime.UtcNow.AddHours(-1),
                        Price = 0.10m
                    },
                    new ElectricityPriceData
                    {
                        StartDate = DateTime.UtcNow.AddHours(-1),
                        EndDate = DateTime.UtcNow,
                        Price = 0.12m
                    }
                }
            };

            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(apiResponse);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(jsonResponse),
               })
               .Verifiable();

            var httpClient = new HttpClient(handlerMock.Object);

            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            _electricityRepositoryMock.Setup(repo => repo.GetLatestStartDateAsync())
                .ReturnsAsync(DateTime.UtcNow.AddHours(-3));

            _electricityRepositoryMock.Setup(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var service = new SaveHistoryDataService(
                _httpClientFactoryMock.Object,
                _electricityRepositoryMock.Object,
                _logger);

            // Act
            await service.LoadDataAsync();

            // Assert
            _electricityRepositoryMock.Verify(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()), Times.Once);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Once(),
               ItExpr.IsAny<HttpRequestMessage>(),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task LoadDataAsync_ShouldNotSaveData_WhenNoNewData()
        {
            // Arrange
            var apiResponse = new
            {
                prices = new List<ElectricityPriceData>
                {
                    new ElectricityPriceData
                    {
                        StartDate = DateTime.UtcNow.AddHours(-4),
                        EndDate = DateTime.UtcNow.AddHours(-3),
                        Price = 0.10m
                    },
                    new ElectricityPriceData
                    {
                        StartDate = DateTime.UtcNow.AddHours(-3),
                        EndDate = DateTime.UtcNow.AddHours(-2),
                        Price = 0.12m
                    }
                }
            };

            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(apiResponse);

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(jsonResponse),
               })
               .Verifiable();

            var httpClient = new HttpClient(handlerMock.Object);

            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            _electricityRepositoryMock.Setup(repo => repo.GetLatestStartDateAsync())
                .ReturnsAsync(DateTime.UtcNow.AddHours(-2));

            var service = new SaveHistoryDataService(
                _httpClientFactoryMock.Object,
                _electricityRepositoryMock.Object,
                _logger);

            // Act
            await service.LoadDataAsync();

            // Assert
            _electricityRepositoryMock.Verify(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()), Times.Never);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Once(),
               ItExpr.IsAny<HttpRequestMessage>(),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task LoadDataAsync_ShouldHandleApiFailure()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.InternalServerError,
               })
               .Verifiable();

            var httpClient = new HttpClient(handlerMock.Object);

            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var service = new SaveHistoryDataService(
                _httpClientFactoryMock.Object,
                _electricityRepositoryMock.Object,
                _logger);

            // Act
            await service.LoadDataAsync();

            // Assert
            _electricityRepositoryMock.Verify(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()), Times.Never);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Once(),
               ItExpr.IsAny<HttpRequestMessage>(),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task LoadDataAsync_ShouldHandleExceptions()
        {
            // Arrange
            _httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Throws(new Exception("HttpClientFactory exception"));

            var service = new SaveHistoryDataService(
                _httpClientFactoryMock.Object,
                _electricityRepositoryMock.Object,
                _logger);

            // Act
            await service.LoadDataAsync();

            // Assert
            // No exception should be thrown, and error should be logged
            _electricityRepositoryMock.Verify(repo => repo.AddRangeAsync(It.IsAny<IEnumerable<ElectricityPriceData>>()), Times.Never);
        }
    }
}
