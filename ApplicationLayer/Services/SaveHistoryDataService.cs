using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ApplicationLayer.Services
{
    public class SaveHistoryDataService : ISaveHistoryDataService
    {
        private readonly IElectricityRepository _electricityRepository;
        private readonly ILogger _logger;

        public SaveHistoryDataService(IElectricityRepository electricityRepository, ILogger<SaveHistoryDataService> logger)
        {
            _electricityRepository = electricityRepository;
            _logger = logger;
        }

        public async Task<bool> ProcessCsvFileAsync(IFormFile csvFile)
        {
            _logger.LogInformation("ProcessCsvFileAsync started at {StartTime}", DateTime.UtcNow);
            if (csvFile == null)
            {
                _logger.LogWarning("No CSV file provided.");
                return false;
            }
            _logger.LogInformation("CSV file received: {FileName}, Size: {FileSize} bytes", csvFile.FileName, csvFile.Length);

            try
            {
                using var reader = new StreamReader(csvFile.OpenReadStream());
                var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ";",
                    HasHeaderRecord = true
                });

                var records = new List<ElectricityPriceCsvRecord>();

                await foreach (var record in csv.GetRecordsAsync<ElectricityPriceCsvRecord>())
                {
                    if (!string.IsNullOrWhiteSpace(record.Timestamp))
                    {
                        records.Add(record);
                    }
                }

                var electricityPriceDataList = records.Select(record =>
                {
                    var startDate = DateTime.Parse(record.Timestamp, CultureInfo.InvariantCulture);
                    var endDate = startDate.AddHours(1);

                    return new ElectricityPriceData
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        Price = record.Price
                    };
                }).ToList();

                _logger.LogInformation("Parsed {RecordCount} records from the CSV file.", records.Count);

                return await ProcessElectricityPriceDataAsync(electricityPriceDataList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file");
                return false;
            }
            finally
            {
                _logger.LogInformation("ProcessCsvFileAsync ended at {EndTime}", DateTime.UtcNow);
            }
        }

        private async Task<bool> ProcessElectricityPriceDataAsync(List<ElectricityPriceData> electricityPriceDataList)
        {
            _logger.LogInformation("Processing {RecordCount} electricity price records.", electricityPriceDataList.Count);

            try
            {
                const int batchSize = 1000;
                var nonDuplicateBatches = new List<List<ElectricityPriceData>>();
                var currentBatch = new List<ElectricityPriceData>();

                foreach (var electricityPriceData in electricityPriceDataList)
                {
                    currentBatch.Add(electricityPriceData);

                    if (currentBatch.Count == batchSize)
                    {
                        var filteredBatch = await FilterDuplicatesAsync(currentBatch);
                        if (filteredBatch.Any())
                        {
                            nonDuplicateBatches.Add(filteredBatch);
                        }
                        _logger.LogInformation("Processed a batch of {BatchSize} records.", batchSize);
                        currentBatch.Clear();
                    }
                }

                if (currentBatch.Any())
                {
                    var filteredBatch = await FilterDuplicatesAsync(currentBatch);
                    if (filteredBatch.Any())
                    {
                        nonDuplicateBatches.Add(filteredBatch);
                    }
                }

                foreach (var batch in nonDuplicateBatches)
                {
                    await _electricityRepository.AddBatchElectricityPricesAsync(batch);
                    _logger.LogInformation("Saved a batch of {BatchSize} non-duplicate records to the database.", batch.Count);
                }

                _logger.LogInformation("Processed and saved all records successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing electricity price data");
                return false;
            }
        }

        private async Task<List<ElectricityPriceData>> FilterDuplicatesAsync(List<ElectricityPriceData> batch)
        {
            _logger.LogInformation("Filtering duplicates in a batch of {BatchSize} records.", batch.Count);

            var startDateList = batch.Select(e => e.StartDate).ToList();
            var endDateList = batch.Select(e => e.EndDate).ToList();

            var duplicates = await _electricityRepository.GetDuplicatesAsync(startDateList, endDateList);
            _logger.LogInformation("{DuplicateCount} duplicates found in the batch.", duplicates.Count);

            var filteredBatch = batch.Where(e => !duplicates.Any(d => d.StartDate == e.StartDate && d.EndDate == e.EndDate)).ToList();
            _logger.LogInformation("Filtered batch size after removing duplicates: {FilteredBatchSize}", filteredBatch.Count);

            return filteredBatch;
        }
    }
}
