using ApplicationLayer.Dto.Consumption.Consumption;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Services
{
    public static class DataFormatter
    {
        public static List<MonthlyConsumptionData> FormatMonthlyData(
            Dictionary<(int Month, int Year), MonthlyConsumptionData> monthlyData)
        {
            return monthlyData
                .OrderBy(entry => entry.Key.Year)
                .ThenBy(entry => entry.Key.Month)
                .Select(entry => entry.Value)
                .ToList();
        }

        public static List<WeeklyConsumptionData> FormatWeeklyData(
            Dictionary<(int Week, int Year), WeeklyConsumptionData> weeklyData)
        {
            return weeklyData
                .OrderBy(entry => entry.Key.Year)
                .ThenBy(entry => entry.Key.Week)
                .Select(entry => entry.Value)
                .ToList();
        }

        public static List<DailyConsumptionData> FormatDailyData(
            Dictionary<DateTime, DailyConsumptionData> dailyData)
        {
            return dailyData
                .OrderBy(entry => entry.Key)
                .Select(entry => entry.Value)
                .ToList();
        }
    }
}
