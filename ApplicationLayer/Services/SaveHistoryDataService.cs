using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplicationLayer.Services
{
    public class SaveHistoryDataService : ISaveHistoryDataService
    {
        private readonly IElectricityRepository _electricityRepository;
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public SaveHistoryDataService(IHttpClientFactory httpClientFactory, IElectricityRepository electricityRepository, ILogger<SaveHistoryDataService> logger)
        {
            _electricityRepository = electricityRepository;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task LoadDataAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var apiUrl = "https://sahkotin.fi/prices?vat&start=2020-01-01T00:00:00.000Z";

                _logger.LogInformation("Fetching data from API...");

                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var dataResponse = JsonSerializer.Deserialize<ElectricityPricesResponse>(jsonResponse, options);

                    if (dataResponse?.Prices == null || !dataResponse.Prices.Any())
                    {
                        _logger.LogInformation("No data received from the API.");
                        return;
                    }

                    // Rest of your logic remains the same, but use dataResponse.Prices instead of dataList
                    var dataList = dataResponse.Prices;

                    // Check the latest StartDate in the database
                    var latestStartDate = await _electricityRepository.GetLatestStartDateAsync();

                    // Filter out data that is already in the database
                    var newDataList = dataList
                        .Where(d => d.StartDate > latestStartDate)
                        .OrderBy(d => d.StartDate)
                        .ToList();

                    if (!newDataList.Any())
                    {
                        _logger.LogInformation("No new data to save.");
                        return;
                    }

                    _logger.LogInformation($"Saving {newDataList.Count} new records to the database...");

                    // Save data in batches
                    const int batchSize = 1000; // Adjust the batch size as needed
                    for (int i = 0; i < newDataList.Count; i += batchSize)
                    {
                        var batch = newDataList.Skip(i).Take(batchSize).ToList();
                        await _electricityRepository.AddRangeAsync(batch);
                    }

                    _logger.LogInformation("Data loading completed successfully.");
                }
                else
                {
                    _logger.LogError($"API call failed with status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading data.");
            }
        }
    }
}
