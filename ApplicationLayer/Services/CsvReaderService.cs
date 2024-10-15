using ApplicationLayer.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services
{
    public class CsvReaderService : ICsvReaderService
    {
        private readonly ILogger<CsvReaderService> _logger;

        public CsvReaderService(ILogger<CsvReaderService> logger)
        {
            _logger = logger;
        }

        public async Task<ConcurrentDictionary<DateTime, decimal>> ReadHourlyConsumptionAsync(string csvFilePath)
        {
            _logger.LogInformation("Reading CSV file from path: {csvFilePath}", csvFilePath);

            var hourlyConsumption = new ConcurrentDictionary<DateTime, decimal>();

            if (!File.Exists(csvFilePath))
            {
                _logger.LogError("CSV file does not exist: {csvFilePath}", csvFilePath);
                return hourlyConsumption;
            }

            try
            {
                using var reader = new StreamReader(csvFilePath);
                var headerLine = await reader.ReadLineAsync().ConfigureAwait(false); // Skip header

                string line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    var (timestamp, consumption) = ParseCsvLine(line);
                    if (timestamp != default)
                    {
                        hourlyConsumption.AddOrUpdate(timestamp, consumption, (key, oldValue) => oldValue + consumption);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading CSV file.");
                throw new CsvReadingException("An error occurred while reading the CSV file.", ex);
            }

            _logger.LogInformation("CSV file read successfully.");
            return hourlyConsumption;
        }

        private (DateTime timestamp, decimal consumption) ParseCsvLine(string line)
        {
            var columns = line.Split(';');
            if (columns.Length < 7 ||
                !DateTime.TryParse(columns[5], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) ||
                !decimal.TryParse(columns[6].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var consumption))
            {
                _logger.LogWarning("Invalid or empty data: {line}", line);
                return (default, 0);
            }

            try
            {
                var timeZoneId = TimeZoneInfo.Local.Id;
                var helsinkiTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                timestamp = TimeZoneInfo.ConvertTime(timestamp, helsinkiTimeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                _logger.LogError("Time zone not found.");
                throw;
            }

            return (timestamp, consumption);
        }
    }

    public class CsvReadingException : Exception
    {
        public CsvReadingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
