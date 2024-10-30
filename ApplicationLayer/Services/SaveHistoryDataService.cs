using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

                //Fetch oldest startdate on database
                 var oldestDateInDb = await _electricityRepository.GetOldestStartDateAsync();
                var cutoffDate = new DateTime(2015, 1, 1, 2, 0, 0, DateTimeKind.Utc);

                //If oldest startdate in database equals cutoffDate, then no data has been fetched
                //If there is no data in db, or oldest startdate doesnt equal cutooffDate, then this logic is runned
                if (oldestDateInDb <= cutoffDate)
                {
                    _logger.LogInformation("No new data to fetch; the database is already up to date.");
                    return; // Skip the API call
                }

                var client = _httpClientFactory.CreateClient();
                var apiUrl = "https://sahkotin.fi/prices?fix&vat&start=2015-01-01T00:00:00.000Z";

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

                    var dataList = dataResponse.Prices;

                    // Fetch existing records from the database to filter out duplicates
                    var existingRecords = await _electricityRepository.GetPricesForPeriodAsync(cutoffDate, DateTime.UtcNow);
                    var existingRecordSet = new HashSet<(DateTime Start, DateTime End)>(existingRecords.Select(e => (e.StartDate, e.EndDate)));
                    //Convert datetimes to finnish timezone
                    var finnishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
                    var nonDuplicateDataList = new List<ElectricityPriceData>();

                    foreach (var data in dataList)
                    {
                        data.StartDate = TimeZoneInfo.ConvertTimeFromUtc(data.StartDate, finnishTimeZone);
                        data.EndDate = data.StartDate.AddHours(1);

                        // Check for duplicates using the HashSet
                        if (!existingRecordSet.Contains((data.StartDate, data.EndDate)))
                        {
                            nonDuplicateDataList.Add(data);
                        }
                        else
                        {
                            _logger.LogInformation($"Duplicate data found for StartDate: {data.StartDate}, EndDate: {data.EndDate}. Skipping.");
                        }
                    }

                    _logger.LogInformation($"Saving {nonDuplicateDataList.Count} unique records to the database...");

                    // Save non-duplicate data in batches
                    const int batchSize = 1000; // Adjust the batch size as needed
                    for (int i = 0; i < nonDuplicateDataList.Count; i += batchSize)
                    {
                        var batch = nonDuplicateDataList.Skip(i).Take(batchSize).ToList();
                        await _electricityRepository.AddBatchElectricityPricesAsync(batch);
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