using ApplicationLayer.Dto;
using ApplicationLayer.Services;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;


public class SaveHistoryDataServiceTests
{
    private readonly Mock<IElectricityRepository> _mockRepository;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<SaveHistoryDataService>> _mockLogger;
    private readonly SaveHistoryDataService _service;

    public SaveHistoryDataServiceTests()
    {
        _mockRepository = new Mock<IElectricityRepository>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<SaveHistoryDataService>>();
        _service = new SaveHistoryDataService(_mockHttpClientFactory.Object, _mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LoadDataAsync_SuccessfulApiCall_StoresDataInRepository()
    {
        // Arrange
        var mockHttpClient = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var pricesResponse = new ElectricityPricesResponse
        {
            Prices = new List<ElectricityPriceData>
            {
                new ElectricityPriceData { StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddHours(1), Price = 10.5M },
                new ElectricityPriceData { StartDate = DateTime.UtcNow.AddHours(1), EndDate = DateTime.UtcNow.AddHours(2), Price = 11.0M }
            }
        };

        mockHttpClient.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(pricesResponse)),
            })
            .Verifiable();

        var httpClient = new HttpClient(mockHttpClient.Object);
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Set up the mock for GetLatestStartDateAsync and GetPricesForPeriodAsync
        _mockRepository.Setup(repo => repo.GetOldestStartDateAsync()).ReturnsAsync(DateTime.UtcNow.AddYears(-1));
        _mockRepository.Setup(repo => repo.GetPricesForPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                       .ReturnsAsync(new List<ElectricityPriceData>()); // No existing records

        // Act
        await _service.LoadDataAsync();

        // Assert
        _mockRepository.Verify(repo => repo.AddBatchElectricityPricesAsync(It.IsAny<List<ElectricityPriceData>>()), Times.Once);
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Data loading completed successfully.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadDataAsync_ApiReturnsNoData_LogsInformation()
    {
        // Arrange
        var mockHttpClient = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var emptyResponse = new ElectricityPricesResponse { Prices = new List<ElectricityPriceData>() };

        mockHttpClient.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(emptyResponse)),
            })
            .Verifiable();

        var httpClient = new HttpClient(mockHttpClient.Object);
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Set up the mock for GetLatestStartDateAsync
        _mockRepository.Setup(repo => repo.GetOldestStartDateAsync()).ReturnsAsync(DateTime.UtcNow.AddYears(-1));
        _mockRepository.Setup(repo => repo.GetPricesForPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                       .ReturnsAsync(new List<ElectricityPriceData>()); // No existing records

        // Act
        await _service.LoadDataAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No data received from the API.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
        _mockRepository.Verify(repo => repo.AddBatchElectricityPricesAsync(It.IsAny<List<ElectricityPriceData>>()), Times.Never);
    }

    [Fact]
    public async Task LoadDataAsync_ApiCallFails_LogsError()
    {
        // Arrange
        var mockHttpClient = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        mockHttpClient.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            })
            .Verifiable();

        var httpClient = new HttpClient(mockHttpClient.Object);
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Set up the mock for GetLatestStartDateAsync
        _mockRepository.Setup(repo => repo.GetOldestStartDateAsync()).ReturnsAsync(DateTime.UtcNow.AddYears(-1));

        // Act
        await _service.LoadDataAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("API call failed with status code")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task LoadDataAsync_ExceptionThrown_LogsError()
    {
        // Arrange
        var mockHttpClient = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        mockHttpClient.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new Exception("Test exception"))
            .Verifiable();

        var httpClient = new HttpClient(mockHttpClient.Object);
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Set up the mock for GetLatestStartDateAsync
        _mockRepository.Setup(repo => repo.GetOldestStartDateAsync()).ReturnsAsync(DateTime.UtcNow.AddYears(-1));

        // Act
        await _service.LoadDataAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("An error occurred while loading data")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }
}
