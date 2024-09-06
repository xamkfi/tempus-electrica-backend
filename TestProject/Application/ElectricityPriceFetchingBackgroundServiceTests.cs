using System.Net;
using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;


public class ElectricityPriceFetchingBackgroundServiceTests
{
    private readonly Mock<ILogger<ElectricityPriceFetchingBackgroundService>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IElectrictyService> _electricityServiceMock;
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;

    public ElectricityPriceFetchingBackgroundServiceTests()
    {
        _loggerMock = new Mock<ILogger<ElectricityPriceFetchingBackgroundService>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _electricityServiceMock = new Mock<IElectrictyService>();
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object);
        _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();

        _httpClientFactoryMock.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _serviceScopeFactoryMock.Setup(factory => factory.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(scope => scope.ServiceProvider.GetService(typeof(IElectrictyService))).Returns(_electricityServiceMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFetchAndSaveDataSuccessfully()
    {
        // Arrange
        var expectedUrl = "http://example.com/spotprice";
        _configurationMock.Setup(config => config["ServiceUrls:SpotPriceUrl"]).Returns(expectedUrl);

        var exampleData = new ElectricityPriceDataDtoIn
        {
            Prices = new List<PriceInfo>
            {
                new PriceInfo { Price = 100m, StartDate = DateTime.Now, EndDate = DateTime.Now.AddHours(1) }
            }
        };
        var jsonResponse = JsonConvert.SerializeObject(exampleData);

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri.ToString() == expectedUrl),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        _electricityServiceMock.Setup(service => service.AddElectricityPricesAsync(It.IsAny<ElectricityPriceDataDtoIn>()))
            .ReturnsAsync(true);

        var service = new ElectricityPriceFetchingBackgroundService(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _serviceScopeFactoryMock.Object
        );

        // Act
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var task = service.StartAsync(cancellationTokenSource.Token);

        // Simulate a delay to allow the service to execute
        await Task.Delay(3000);

        // Cancel the token to stop the service
        cancellationTokenSource.Cancel();

        // Assert
        await task; // Ensure the task completes
        _electricityServiceMock.Verify(service => service.AddElectricityPricesAsync(It.IsAny<ElectricityPriceDataDtoIn>()), Times.Once);
    }
   

}

    

    