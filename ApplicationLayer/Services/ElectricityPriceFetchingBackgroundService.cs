using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class ElectricityPriceFetchingBackgroundService : BackgroundService
{
    private readonly ILogger<ElectricityPriceFetchingBackgroundService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ElectricityPriceFetchingBackgroundService(
        ILogger<ElectricityPriceFetchingBackgroundService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Electricity Price Fetching Background Service is starting.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var electricityService = scope.ServiceProvider.GetRequiredService<IElectrictyService>();
                await FetchElectricityPricesAsync(electricityService, stoppingToken);
            }
            var delay = CalculateNextFetchTime(now);
            _logger.LogInformation("Starting delay of {Delay} until next fetch", delay);
            await Task.Delay(delay, stoppingToken);
        }
        _logger.LogInformation("Electricity Price Fetching Background Service is stopping.");
    }

    private TimeSpan CalculateNextFetchTime(DateTime currentTime)
    {
        var nextHour = currentTime.AddHours(1).Date.AddHours(currentTime.Hour + 1);
        var nextFetchTime = new DateTime(nextHour.Year, nextHour.Month, nextHour.Day, nextHour.Hour, 1, 0);
        return nextFetchTime - currentTime;
    }

    private async Task FetchElectricityPricesAsync(IElectrictyService electricityService, CancellationToken stoppingToken)
    {

        var spotPriceUrl = _configuration["ServiceUrls:SpotPriceUrl"];
        var httpClient = _httpClientFactory.CreateClient();
        _logger.LogInformation("Fetching electricity prices from {Url}", spotPriceUrl);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync(spotPriceUrl, stoppingToken);
            var responseTime = stopwatch.Elapsed;
            _logger.LogInformation("Received response in {ResponseTime}ms", responseTime.TotalMilliseconds);

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Prices fetched: {Content}", content);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Fetched data is empty. Retrying after 1 minute.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await FetchElectricityPricesAsync(electricityService, stoppingToken);
                return;
            }
            await SaveElectricityPrices(content, electricityService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching electricity prices");
        }
    }

    private async Task SaveElectricityPrices(string electricityDataJson, IElectrictyService electricityService)
    {
        try
        {
            var electricityData = JsonConvert.DeserializeObject<ElectricityPriceDataDtoIn>(electricityDataJson);
            if (electricityData != null && electricityData.Prices != null && electricityData.Prices.Any())
            {
                // Define the Finnish time zone
                var finnishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

                // Adjust StartDate to Finnish time and set EndDate
                foreach (var data in electricityData.Prices)
                {
                    // Convert the StartDate from UTC to Finnish time
                    data.StartDate = TimeZoneInfo.ConvertTimeFromUtc(data.StartDate, finnishTimeZone);

                    // Set the EndDate to one hour after the StartDate
                    data.EndDate = data.StartDate.AddHours(1);
                }

                // Save the adjusted data to the database
                bool success = await electricityService.AddElectricityPricesAsync(electricityData);
                if (success)
                {
                    _logger.LogInformation("Data saved successfully to the database.");
                }
                else
                {
                    _logger.LogWarning("No new data was added to the database (all duplicates or invalid entries).");
                }
            }
            else
            {
                _logger.LogWarning("Invalid data received. No data saved to the database.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while saving data to the database.");
        }
    }
}
