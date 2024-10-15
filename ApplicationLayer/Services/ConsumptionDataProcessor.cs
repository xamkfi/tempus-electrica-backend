using ApplicationLayer.Dto;
using ApplicationLayer.Dto.Consumption.Consumption;
using ApplicationLayer.Interfaces;
using Domain.Entities;
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
    public class ConsumptionDataProcessor : IConsumptionDataProcessor
    {
        private readonly ILogger<ConsumptionDataProcessor> _logger;

        public ConsumptionDataProcessor(ILogger<ConsumptionDataProcessor> logger)
        {
            _logger = logger;
        }

        public ProcessedCsvDataResult ProcessConsumptionData(
            ConcurrentDictionary<DateTime, decimal> hourlyConsumption,
            List<ElectricityPriceData> electricityPrices,
            decimal? fixedPrice)
        {
            _logger.LogInformation("Processing consumption data.");

            var monthlyData = new Dictionary<(int Month, int Year), MonthlyConsumptionData>();
            var weeklyData = new Dictionary<(int Week, int Year), WeeklyConsumptionData>();
            var dailyData = new Dictionary<DateTime, DailyConsumptionData>();

            decimal totalSpotPrice = 0.0m;
            decimal totalFixedPrice = 0.0m;
            decimal totalConsumption = 0.0m;

            var priceLookup = electricityPrices.ToDictionary(p => p.StartDate);

            foreach (var (hourlyTimestamp, consumption) in hourlyConsumption)
            {
                var spotPrice = CalculatePricesForConsumption(hourlyTimestamp, consumption, priceLookup);
                totalSpotPrice += spotPrice;

                var fixedCost = fixedPrice.HasValue ? consumption * fixedPrice.Value : 0;
                totalFixedPrice += fixedCost;
                totalConsumption += consumption;

                CalculateMonthlyData(monthlyData, hourlyTimestamp, consumption, spotPrice, fixedCost);
                CalculateWeeklyData(weeklyData, hourlyTimestamp, consumption, spotPrice, fixedCost);
                CalculateDailyData(dailyData, hourlyTimestamp, consumption, spotPrice, fixedCost);
            }

            _logger.LogInformation("Consumption data processed successfully.");

            return new ProcessedCsvDataResult
            {
                TotalSpotPrice = totalSpotPrice,
                TotalFixedPrice = totalFixedPrice,
                TotalConsumption = totalConsumption,
                MonthlyData = monthlyData,
                WeeklyData = weeklyData,
                DailyData = dailyData
            };
        }

        private decimal CalculatePricesForConsumption(
            DateTime timestamp,
            decimal consumption,
            Dictionary<DateTime, ElectricityPriceData> priceLookup)
        {
            if (priceLookup.TryGetValue(timestamp, out var price))
            {
                return consumption * price.Price;
            }
            else
            {
                _logger.LogWarning("No price found for timestamp: {timestamp}", timestamp);
                return 0;
            }
        }

        private void CalculateMonthlyData(
            Dictionary<(int Month, int Year), MonthlyConsumptionData> monthlyData,
            DateTime timestamp,
            decimal consumption,
            decimal spotPrice,
            decimal fixedCost)
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

        private void CalculateWeeklyData(
            Dictionary<(int Week, int Year), WeeklyConsumptionData> weeklyData,
            DateTime timestamp,
            decimal consumption,
            decimal spotPrice,
            decimal fixedCost)
        {
            var calendar = CultureInfo.InvariantCulture.Calendar;
            var week = calendar.GetWeekOfYear(timestamp, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            var key = (week, timestamp.Year);

            if (!weeklyData.TryGetValue(key, out var weekly))
            {
                weekly = new WeeklyConsumptionData { Week = week, Year = timestamp.Year };
                weeklyData[key] = weekly;
            }
            weekly.Consumption += consumption;
            weekly.SpotPrice += spotPrice / 100;
            weekly.FixedPrice += fixedCost / 100;
        }

        private void CalculateDailyData(
            Dictionary<DateTime, DailyConsumptionData> dailyData,
            DateTime timestamp,
            decimal consumption,
            decimal spotPrice,
            decimal fixedCost)
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
    }
}
