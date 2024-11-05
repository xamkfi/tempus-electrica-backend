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
                        // Round down the timestamp to the nearest hour after conversion
                        var roundedTimestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0);

                        // Aggregate the consumption values for the same hour
                        hourlyConsumption.AddOrUpdate(roundedTimestamp, consumption, (key, oldValue) => oldValue + consumption);
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

            // Ensure that the line has enough columns and that the timestamp and consumption are parsed correctly.
            if (columns.Length < 7 ||
                !DateTime.TryParse(columns[5], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp) ||
                !decimal.TryParse(columns[6].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var consumption))
            {
                _logger.LogWarning("Invalid or empty data: {line}", line);
                return (default, 0);
            }

            // Ensure that the DateTimeKind is set to Utc before converting to Helsinki time.
            timestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);

            try
            {
                var helsinkiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
                var helsinkiTimestamp = TimeZoneInfo.ConvertTimeFromUtc(timestamp, helsinkiTimeZone);

                return (helsinkiTimestamp, consumption);
            }
            catch (TimeZoneNotFoundException)
            {
                _logger.LogError("Time zone 'FLE Standard Time' not found.");
                throw;
            }
            
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
