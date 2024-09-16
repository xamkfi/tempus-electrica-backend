﻿using System.Globalization;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ApplicationLayer.Dto;
using Domain.Entities;


namespace ApplicationLayer.Services
{
    public class CalculateFinGridConsumptionPriceService : ICalculateFingridConsumptionPrice
    {
        private readonly IElectricityRepository _electricityRepository;
        private readonly ILogger<CalculateFinGridConsumptionPriceService> _logger;

        public CalculateFinGridConsumptionPriceService(IElectricityRepository electricityRepository, ILogger<CalculateFinGridConsumptionPriceService> logger)
        {
            _electricityRepository = electricityRepository;
            _logger = logger;
        }
        public enum PriceOption
        {
            FixedPrice,
            SpotPrice,
            Error
        }
        const decimal OptimizePercentage = 0.25M;

        public async Task<(
        decimal totalSpotPrice,
        decimal totalFixedPrice,
        string cheaperOption,
        decimal totalConsumption,
        decimal priceDifference,
        decimal equivalentFixedPrice,
        decimal totalOptimizedSpotPrice,
        List<MonthlyConsumptionData> monthlyData,
        List<WeeklyConsumptionData> weeklyData,
        List<DailyConsumptionData> dailyData,
        DateTime startDate,
        DateTime endDate
        )> CalculateTotalConsumptionPricesAsync(
        string csvFilePath,
        decimal? fixedPrice
)
        {
            _logger.LogInformation("Start calculating total consumption prices.");

            if (!IsValidFilePath(csvFilePath))
            {
                _logger.LogError("CSV file path is invalid or file does not exist: {csvFilePath}", csvFilePath);
                return GetDefaultResult();
            }

            try
            {
                _logger.LogInformation("Reading CSV file from path: {csvFilePath}", csvFilePath);

                DateTime startDate;
                DateTime endDate;
                ConcurrentDictionary<DateTime, decimal> hourlyConsumption = new ConcurrentDictionary<DateTime, decimal>();

                using (var reader = new StreamReader(csvFilePath))
                {

                    var headerLine = await reader.ReadLineAsync(); //Skip header
                    var firstLine = await reader.ReadLineAsync(); 
                    if (firstLine == null)
                    {
                        _logger.LogWarning("CSV file contains no data.");
                        return GetDefaultResult();
                    }

                    //Process the first line directly after reading it
                    var (firstTimestamp, firstConsumption) = ParseCsvLine(firstLine);
                    if (firstTimestamp == default)
                    {
                        _logger.LogWarning("Invalid timestamp in the first line.");
                        return GetDefaultResult();
                    }

                    //Initialize startDate and endDate with the first timestamp
                    startDate = firstTimestamp;
                    endDate = firstTimestamp;

                    //Add the first consumption value to the dictionary
                    hourlyConsumption.AddOrUpdate(firstTimestamp, firstConsumption, (key, oldValue) => oldValue + firstConsumption);

                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var (timestamp, consumption) = ParseCsvLine(line);
                        if (timestamp != default)
                        {
                            hourlyConsumption.AddOrUpdate(timestamp, consumption, (key, oldValue) => oldValue + consumption);

                            //Update endDate
                            if (timestamp > endDate)
                            {
                                endDate = timestamp;
                            }
                        }
                    }
                }

                //Adjust endDate to the next full hour if it's not already a full hour
                if (endDate.Minute != 0 || endDate.Second != 0 || endDate.Millisecond != 0)
                {
                    endDate = new DateTime(endDate.Year, endDate.Month, endDate.Day, endDate.Hour, 0, 0).AddHours(1);
                }

                if (startDate == default || endDate == default)
                {
                    _logger.LogError("Invalid timestamps in the CSV file.");
                    return GetDefaultResult();
                }

                _logger.LogInformation("Fetching electricity prices from {startDate} to {endDate}", startDate, endDate);
                //Fetch hourly electricityprice data with given start & end -date
                var electricityPrices = await GetElectricityPricesAsync(startDate, endDate);

                if (!electricityPrices.Any())
                {
                    _logger.LogError("No electricity prices found for the given period.");
                    return GetDefaultResult();
                }

                //Process the aggregated data
                var (totalSpotPrice, totalFixedPrice, totalConsumption, monthlyData, weeklyData, dailyData) = ProcessCsvData(hourlyConsumption, electricityPrices, fixedPrice);
                var cheaperOption = DetermineCheaperOption(totalSpotPrice, totalFixedPrice, fixedPrice);

                //Calculate the price difference based on the cheaper option
                decimal priceDifference = Math.Round((totalSpotPrice - totalFixedPrice), 2);

                //Calculate the equivalent fixed price if spot price is cheaper
                decimal equivalentFixedPrice = 0;
                if (cheaperOption == PriceOption.SpotPrice)
                {
                    equivalentFixedPrice = totalSpotPrice / totalConsumption * 100;
                }

                //Optimize consumption and calculate the optimized spot price
                var optimizedConsumption = OptimizeConsumption(hourlyConsumption);
                var totalOptimizedSpotPrice = optimizedConsumption.Sum(x => CalculatePricesForConsumption(x.Key, x.Value, electricityPrices)) / 100;

                _logger.LogInformation("Total spot price: {totalSpotPrice}, Total fixed price: {totalFixedPrice}, Total consumption: {totalConsumption}, Cheaper option: {cheaperOption}, Price difference: {priceDifference}, Equivalent fixed price: {equivalentFixedPrice}, Total optimized spot price: {totalOptimizedSpotPrice}", totalSpotPrice, totalFixedPrice, totalConsumption, cheaperOption, priceDifference, equivalentFixedPrice, totalOptimizedSpotPrice);

                return (totalSpotPrice / 100, totalFixedPrice / 100, cheaperOption.ToString(), totalConsumption, priceDifference / 100,
                    equivalentFixedPrice / 100, totalOptimizedSpotPrice, FormatMonthlyData(monthlyData), FormatWeeklyData(weeklyData), FormatDailyData(dailyData), startDate, endDate);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating electricity consumption price.");
                throw;
            }
        }

        public (decimal totalSpotPrice, decimal totalFixedPrice, decimal totalConsumption,
            Dictionary<(int Month, int Year), MonthlyConsumptionData> monthlyData, Dictionary<(int Week, int Year),
            WeeklyConsumptionData> weeklyData, Dictionary<DateTime, DailyConsumptionData> dailyData)ProcessCsvData(ConcurrentDictionary<DateTime, decimal> hourlyConsumption, List<ElectricityPriceData> electricityPrices, decimal? fixedPrice)
        {
            _logger.LogInformation("Processing CSV data.");

            var monthlyData = new Dictionary<(int Month, int Year), MonthlyConsumptionData>();
            var weeklyData = new Dictionary<(int Week, int Year), WeeklyConsumptionData>();
            var dailyData = new Dictionary<DateTime, DailyConsumptionData>();

            decimal totalSpotPrice = 0.0m;
            decimal totalFixedPrice = 0.0m;
            decimal totalConsumption = 0.0m;

            //Iterate through each entry in the hourly consumption data
            foreach (var (hourlyTimestamp, consumption) in hourlyConsumption)
            {
                //Calculate the spot price for the given hourly timestamp and consumption
                var spotPrice = CalculatePricesForConsumption(hourlyTimestamp, consumption, electricityPrices);
                //Accumulate the spot price and consumption
                totalSpotPrice += spotPrice;


                var fixedCost = fixedPrice.HasValue ? consumption * fixedPrice.Value : 0;
                totalFixedPrice += fixedCost;
                totalConsumption += consumption;

                CalculateMonthlyData(monthlyData, hourlyTimestamp, consumption, spotPrice, fixedCost);
                CalculateWeeklyData(weeklyData, hourlyTimestamp, consumption, spotPrice, fixedCost);
                CalculateDailyData(dailyData, hourlyTimestamp, consumption, spotPrice, fixedCost);
            }

            _logger.LogInformation("CSV data processed successfully.");
            return (totalSpotPrice, totalFixedPrice, totalConsumption, monthlyData, weeklyData, dailyData);
        }

        public async Task<List<ElectricityPriceData>> GetElectricityPricesAsync(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("Getting electricity prices for the period from {startDate} to {endDate}.", startDate, endDate);

            var prices = await _electricityRepository.GetPricesForPeriodAsync(startDate, endDate);

            if (prices != null)
            {
                _logger.LogInformation("Retrieved {count} electricity price records.", prices.Count());
                return prices.ToList();
            }
            else
            {
                _logger.LogWarning("No electricity prices found for the given period; prices list is null.");
                return new List<ElectricityPriceData>();
            }
        }

        public (DateTime timestamp, decimal consumption) ParseCsvLine(string line)
        {
            //Split the CSV line into columns using ';' as a delimiter
            var columns = line.Split(';');
            if (columns.Length < 7 ||
                !DateTime.TryParse(columns[5], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) || //parse the 6th column as a DateTime
                !decimal.TryParse(columns[6].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var consumption)) //parse the 7th column as a decimal
            {
                _logger.LogWarning("Invalid or empty data: {line}", line);
                return (default, 0);
            }

            //Convert timestamp to Helsinki time zone (which is UTC+2 or UTC+3 based on daylight saving time)
            var helsinkiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
            timestamp = TimeZoneInfo.ConvertTime(timestamp, helsinkiTimeZone);

            return (timestamp, consumption);
        }

        private decimal CalculatePricesForConsumption(DateTime timestamp, decimal consumption, List<ElectricityPriceData> electricityPrices)
        {
            var price = electricityPrices.FirstOrDefault(p => p.StartDate <= timestamp && p.EndDate > timestamp);
            if (price == null)
            {
                _logger.LogInformation("No price found for timestamp: {timestamp:yyyy-MM-dd HH:mm:ss.fffffff}", timestamp);
                return 0;
            }

            return consumption * price.Price;
        }
        public ConcurrentDictionary<DateTime, decimal> OptimizeConsumption(ConcurrentDictionary<DateTime, decimal> hourlyConsumption)
        {
            _logger.LogInformation("Optimizing consumption by moving 25% of 12:00-23:59 consumption to 00:00-11:59 period.");

            //New ConcurrentDictionary to store the optimized consumption values.
            var optimizedConsumption = new ConcurrentDictionary<DateTime, decimal>();

            //Iterate over the original hourly consumption dictionary.
            foreach (var (timestamp, consumption) in hourlyConsumption)
            {
                //Add the rounded timestamp and its corresponding consumption to the optimized consumption dictionary.
                //If the timestamp already exists, add the current consumption to the existing value.
                var roundedTimestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0);
                optimizedConsumption.AddOrUpdate(roundedTimestamp, consumption, (key, oldValue) => oldValue + consumption);
            }
            //Last timestamp in csv file
            var maxTimestamp = optimizedConsumption.Keys.Max();

            //Iterate over the optimized consumption dictionary again to apply the optimization logic.
            foreach (var (timestamp, consumption) in optimizedConsumption)
            {
                //Only consumption in time range 12:00-23:59 will be optimized 
                if (timestamp.Hour >= 12 && timestamp.Hour <= 23)
                {
                    //Calculate the amount of consumption to move
                    var consumptionToMove = consumption * OptimizePercentage;

                    //Ensure that moving consumption will not exceed the maximum available timestamp.
                    var nextDayTimestamp = timestamp.AddHours(12);
                    if (nextDayTimestamp <= maxTimestamp)
                    {
                        // Reduce the consumption for the current hour by the amount to be moved.
                        optimizedConsumption.AddOrUpdate(timestamp, consumption, (key, oldValue) => oldValue - consumptionToMove);

                        // Move the consumption to the corresponding hour.
                        optimizedConsumption.AddOrUpdate(nextDayTimestamp, consumptionToMove, (key, oldValue) => oldValue + consumptionToMove);

                        
                    }
                    else
                    {
                        _logger.LogWarning("Attempted to move consumption beyond the end date. Skipping move for timestamp {timestamp}.", timestamp);
                    }
                }
            }

            _logger.LogInformation("Consumption optimized successfully.");
            return optimizedConsumption;
        }


        private PriceOption DetermineCheaperOption(decimal totalSpotPrice, decimal totalFixedPrice, decimal? fixedPrice)
        {
            try
            {
                if (totalFixedPrice == 0 || totalSpotPrice == 0)
                {
                    _logger.LogWarning("Either total fixed price or total spot price is zero, cannot determine the cheaper option.");
                    return PriceOption.Error;
                }

                var cheaperOption = fixedPrice.HasValue && totalFixedPrice < totalSpotPrice ? PriceOption.FixedPrice : PriceOption.SpotPrice;
                _logger.LogInformation("Cheaper option determined: {cheaperOption}", cheaperOption);
                return cheaperOption;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining the cheaper option.");
                return PriceOption.Error;
            }
        }

        private void CalculateMonthlyData(Dictionary<(int Month, int Year), MonthlyConsumptionData> monthlyData, DateTime timestamp, decimal consumption, decimal spotPrice, decimal fixedCost)
        {
            var key = (timestamp.Month, timestamp.Year);
            if (!monthlyData.TryGetValue(key, out var monthly))
            {
                monthly = new MonthlyConsumptionData { Month = key.Month, Year = key.Year };
                monthlyData[key] = monthly;
            }
            monthly.Consumption += consumption;
            monthly.SpotPrice += spotPrice / 100;
            monthly.FixedPrice += fixedCost / 100;
        }

        private void CalculateWeeklyData(Dictionary<(int Week, int Year), WeeklyConsumptionData> weeklyData, DateTime timestamp, decimal consumption, decimal spotPrice, decimal fixedCost)
        {
            var firstDayOfWeek = timestamp.AddDays(-(int)timestamp.DayOfWeek + 1);
            var week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(firstDayOfWeek, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            var key = (week, firstDayOfWeek.Year);
            if (!weeklyData.TryGetValue(key, out var weekly))
            {
                weekly = new WeeklyConsumptionData { Week = week, Year = firstDayOfWeek.Year };
                weeklyData[key] = weekly;
            }
            weekly.Consumption += consumption;
            weekly.SpotPrice += spotPrice / 100;
            weekly.FixedPrice += fixedCost / 100;
        }

        private void CalculateDailyData(Dictionary<DateTime, DailyConsumptionData> dailyData, DateTime timestamp, decimal consumption, decimal spotPrice, decimal fixedCost)
        {
            var day = timestamp.Date;
            if (!dailyData.TryGetValue(day, out var daily))
            {
                daily = new DailyConsumptionData { Day = day.ToString("d.M.yyyy", CultureInfo.InvariantCulture) };
                dailyData[day] = daily;
            }
            daily.Consumption += consumption;
            daily.SpotPrice += spotPrice / 100;
            daily.FixedPrice += fixedCost / 100;
        }

        private static bool IsValidFilePath(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }

        private static (decimal totalSpotPrice, decimal totalFixedPrice, string cheaperOption, decimal totalConsumption, decimal priceDifference, decimal equivalentFixedPrice, decimal totalOptimizedSpotPrice, List<MonthlyConsumptionData> monthlyData, List<WeeklyConsumptionData> weeklyData, List<DailyConsumptionData> dailyData, DateTime startDate, DateTime endDate) GetDefaultResult()
        {
            return (0, 0, "Error calculating data, or no data were found", 0, 0, 0, 0,
                new List<MonthlyConsumptionData>(), new List<WeeklyConsumptionData>(), new List<DailyConsumptionData>(), default, default);
        }

        public static List<MonthlyConsumptionData> FormatMonthlyData(Dictionary<(int Month, int Year), MonthlyConsumptionData> monthlyData)
        {
            return monthlyData
        .OrderBy(entry => entry.Key.Year)
        .ThenBy(entry => entry.Key.Month)
        .Select(entry => entry.Value)
        .ToList();
        }

        public static List<WeeklyConsumptionData> FormatWeeklyData(Dictionary<(int Week, int Year), WeeklyConsumptionData> weeklyData)
        {
            return weeklyData
        .OrderBy(entry => entry.Key.Year)
        .ThenBy(entry => entry.Key.Week)
        .Select(entry => entry.Value)
        .ToList();
        }

        public static List<DailyConsumptionData> FormatDailyData(Dictionary<DateTime, DailyConsumptionData> dailyData)
        {
            return dailyData
        .OrderBy(entry => entry.Key)
        .Select(entry => entry.Value)
        .ToList();
        }
    
    }
}