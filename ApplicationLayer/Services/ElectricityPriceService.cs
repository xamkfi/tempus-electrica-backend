using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services
{
    public class ElectricityPriceService : IElectricityPriceService
    {
        private readonly IElectricityRepository _electricityRepository;
        private readonly ILogger<ElectricityPriceService> _logger;

        public ElectricityPriceService(IElectricityRepository electricityRepository, ILogger<ElectricityPriceService> logger)
        {
            _electricityRepository = electricityRepository ?? throw new ArgumentNullException(nameof(electricityRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<(decimal totalFixedPriceCost, decimal totalSpotPriceCost, decimal costDifference, string cheaperOption, decimal totalAverageConsumption, decimal totalMinConsumption, decimal totalMaxConsumption, decimal averageHourlySpotPrice, List<MonthlyData> monthlyData)> GetElectricityPriceDataAsync(CombinedRequestDtoIn request)
        {
            _logger.LogInformation("Start processing GetElectricityPriceDataAsync for year {Year}", request.Year);
            request.ValidateModel();

            var startDate = new DateTime(request.Year, 1, 1);
            var endDate = DetermineEndDate(request.Year, _logger);

            _logger.LogInformation("Fetching electricity price data from {StartDate} to {EndDate}", startDate.AddYears(-1), startDate);
            var electricityPriceData = (await _electricityRepository.GetPricesForPeriodAsync(startDate.AddYears(-1), startDate))?.ToList() ?? new List<ElectricityPriceData>();
            ValidateElectricityPriceData(electricityPriceData, startDate, _logger);

            // Get min, average, and max values from CalculateDirectiveConsumption
            var (totalMinConsumption, totalAverageConsumption, totalMaxConsumption) = CalculateDirectiveConsumption(request);


            var totalFixedPriceCost = CalculateYearlyCost(request.FixedPrice, totalAverageConsumption);
            var totalSpotPriceCost = CalculateYearlySpotPrice(electricityPriceData, totalAverageConsumption);
            var monthlyData = CalculateMonthlyDataAsync(totalAverageConsumption, electricityPriceData, request.FixedPrice, totalSpotPriceCost, request.Year);
            var costDifference = Math.Round(Math.Abs(totalFixedPriceCost - totalSpotPriceCost), 2);

            var averageHourlySpotPrice = CalculateAverageYearlySpotPrice(electricityPriceData);

            var result = (
                Math.Round(totalFixedPriceCost, 2),
                Math.Round(totalSpotPriceCost, 2),
                costDifference,
                totalFixedPriceCost < totalSpotPriceCost ? "Fixed price" : "Spot price",
                Math.Round(totalAverageConsumption, 2),
                Math.Round(totalMinConsumption, 2),
                Math.Round(totalMaxConsumption, 2),
                Math.Round(averageHourlySpotPrice, 2),
                monthlyData
            );

            _logger.LogInformation("Completed processing GetElectricityPriceDataAsync for year {Year}", request.Year);
            return result;
        }


        private static DateTime DetermineEndDate(int year, ILogger<ElectricityPriceService> logger)
        {
            var endDate = year == DateTime.Now.Year ? DateTime.Now.Date : new DateTime(year, 12, 31);
            logger.LogInformation("Determined end date as {EndDate} for year {Year}", endDate, year);
            return endDate;
        }

        private static void ValidateElectricityPriceData(IEnumerable<ElectricityPriceData> electricityPriceData, DateTime startDate, ILogger<ElectricityPriceService> logger)
        {
            if (electricityPriceData == null || !electricityPriceData.Any())
            {
                logger.LogError("No data found for the specified date range starting from {StartDate}", startDate.AddYears(-1));
                throw new InvalidOperationException($"No data found for the specified date range starting from {startDate.AddYears(-1)}.");
            }
            logger.LogInformation("Validated electricity price data for the specified date range starting from {StartDate}", startDate.AddYears(-1));
        }

        public class Consumption
        {
            public decimal Total { get; set; }
            public decimal HousingConsumption { get; set; }
            public decimal WorkShiftConsumption { get; set; }
            public decimal HeatingConsumption { get; set; }
            public decimal SaunaConsumption { get; set; }
            public decimal FireplaceSavings { get; set; }
            public decimal ElectricCarConsumption { get; set; }
            public decimal ResidentConsumption { get; set; }
            public decimal FloorHeatingConsumption { get; set; }
            public decimal SolarPanelSavings { get; set; }
            public WorkShiftType WorkShiftType { get; set; }
            public decimal MinConsumption { get; set; }
            public decimal MaxConsumption { get; set; }
        }

        public class MonthlyData
        {
            public int Month { get; set; }
            public decimal Consumption { get; set; }
            public decimal SpotPriceAverageOfMonth { get; set; }
            public decimal FixedPriceAverageOfMonth { get; set; }
            public decimal FixedPriceTotal { get; set; }
            public decimal SpotPriceTotal { get; set; }
            public decimal AverageConsumptionPerHour { get; set; }
        }

        private (decimal totalMinConsumption, decimal totalAverageConsumption, decimal totalMaxConsumption) CalculateDirectiveConsumption(CombinedRequestDtoIn request)
        {
            _logger.LogInformation("Calculating directive consumption");

            var (housingMin, housingAverage, housingMax) = GetHousingConsumption(request.HouseType, request.SquareMeters);
            var workShiftConsumption = GetWorkShiftConsumption(request.WorkShiftType) * 365;
            var saunaConsumption = CalculateSaunaConsumption(request.HasSauna, request.SaunaHeatingFrequency);
            var fireplaceSavings = CalculateFireplaceSavings(request.HasFireplace, request.FireplaceFrequency);

            int numberOfCars = request.NumberOfCars ?? 0;
            var electricCarConsumption = CalculateElectricCarConsumption(request.HasElectricCar, numberOfCars, request.ElectricCarkWhUsagePerYear);
            var (residentMin, residentAverage, residentMax) = GetResidentConsumption(request.NumberOfResidents, request.HouseType);

            decimal heatingMin = 0, heatingAverage = 0, heatingMax = 0;
            if (request.HouseType != HouseType.ApartmentHouse)
            {
                (heatingMin, heatingAverage, heatingMax) = GetHeatingConsumption(request.HeatingType);
            }

            decimal solarPanelSavings = 0;
            if ((request.HouseType == HouseType.DetachedHouse || request.HouseType == HouseType.Cottage) && request.HasSolarPanel && request.SolarPanel.HasValue)
            {
                solarPanelSavings = CalculateSolarPanelSavings(request.SolarPanel.Value);
            }

            decimal floorHeatingConsumption = 0;
            if ((request.HouseType == HouseType.ApartmentHouse || request.HouseType == HouseType.TerracedHouse) && request.HasFloorHeating && request.FloorSquareMeters.HasValue)
            {
                floorHeatingConsumption = CalculateFloorHeatingConsumption(request.FloorSquareMeters.Value);
            }

            decimal totalMinConsumption = housingMin + workShiftConsumption + heatingMin + electricCarConsumption + residentMin + saunaConsumption + floorHeatingConsumption;
            decimal totalAverageConsumption = housingAverage + workShiftConsumption + heatingAverage + electricCarConsumption + residentAverage + saunaConsumption + floorHeatingConsumption;
            decimal totalMaxConsumption = housingMax + workShiftConsumption + heatingMax + electricCarConsumption + residentMax + saunaConsumption + floorHeatingConsumption;

            totalMinConsumption -= (fireplaceSavings + solarPanelSavings);
            totalAverageConsumption -= (fireplaceSavings + solarPanelSavings);
            totalMaxConsumption -= (fireplaceSavings + solarPanelSavings);

            _logger.LogInformation("Calculated total directive consumption: Min {MinConsumption}, Average {AverageConsumption}, Max {MaxConsumption}",
                totalMinConsumption, totalAverageConsumption, totalMaxConsumption);

            return (totalMinConsumption, totalAverageConsumption, totalMaxConsumption);
        }


        private decimal CalculateSolarPanelSavings(int? numberOfPanels)
        {
            if (!numberOfPanels.HasValue)
            {
                return 0;
            }

            var monthlyProduction = CalculateMonthlySolarPanelProduction(numberOfPanels.Value);
            return monthlyProduction.Values.Sum();
        }

        private static readonly Dictionary<int, decimal> MonthlyProductionPerPanel = new()
        {
            { 1, 6.3M },
            { 2, 15.5M },
            { 3, 33.6M },
            { 4, 41.7M },
            { 5, 51.3M },
            { 6, 49.5M },
            { 7, 49.1M },
            { 8, 42.7M },
            { 9, 29.6M },
            { 10, 18.0M },
            { 11, 6.4M },
            { 12, 3.1M }
        };

        private static Dictionary<int, decimal> CalculateMonthlySolarPanelProduction(int numberOfPanels)
        {
            return MonthlyProductionPerPanel
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value * numberOfPanels);
        }

        private decimal CalculateFloorHeatingConsumption(int floorSquareMeters)
        {
            var consumptionPerSquareMeter = 100;
            return floorSquareMeters * consumptionPerSquareMeter;
        }

        private decimal CalculateSaunaConsumption(bool hasSauna, int? saunaHeatingFrequency)
        {
            return hasSauna ? (saunaHeatingFrequency ?? 0) * 52 * 13 : 0;
        }

        private decimal CalculateFireplaceSavings(bool hasFireplace, int? fireplaceFrequency)
        {
            return hasFireplace ? (fireplaceFrequency ?? 0) * 720 : 0;
        }

        private decimal CalculateElectricCarConsumption(bool hasElectricCar, int numberOfCars, int? electricCarkWhUsagePerYear)
        {
            if (!hasElectricCar)
                return 0;

            return numberOfCars * (electricCarkWhUsagePerYear ?? 0);
        }

        private (decimal min, decimal average, decimal max) GetHousingConsumption(HouseType houseType, int squareMeters)
        {
            return houseType switch
            {
                HouseType.ApartmentHouse => (squareMeters * 20, squareMeters * 25, squareMeters * 30),
                HouseType.TerracedHouse => (squareMeters * 100, squareMeters * 110, squareMeters * 120),
                HouseType.DetachedHouse => (squareMeters * 115, squareMeters * 130, squareMeters * 145),
                HouseType.Cottage => (squareMeters * 110, squareMeters * 120, squareMeters * 130),
                _ => (0, 0, 0)
            };
        }

        private decimal GetWorkShiftConsumption(WorkShiftType workShiftType)
        {
            return workShiftType switch
            {
                WorkShiftType.DayWorker => 0.3M,
                WorkShiftType.ShiftWorker => 0.4M,
                WorkShiftType.RemoteWorker => 0.5M,
                _ => 0.3M
            };
        }

        private readonly Dictionary<WorkShiftType, Dictionary<int, decimal>> _workShiftWeights = new()
        {
            { WorkShiftType.DayWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.05M }, { 1, 0.05M }, { 2, 0.05M }, { 3, 0.05M }, { 4, 0.05M },
                    { 5, 0.05M }, { 6, 0.05M }, { 7, 0.05M }, { 8, 0.01M }, { 9, 0.01M },
                    { 10, 0.01M }, { 11, 0.01M }, { 12, 0.01M }, { 13, 0.01M }, { 14, 0.01M },
                    { 15, 0.01M }, { 16, 0.01M }, { 17, 0.01M }, { 18, 0.05M }, { 19, 0.05M },
                    { 20, 0.05M }, { 21, 0.05M }, { 22, 0.05M }, { 23, 0.05M }
                }
            },
            { WorkShiftType.RemoteWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.10M }, { 1, 0.10M }, { 2, 0.10M }, { 3, 0.10M }, { 4, 0.10M },
                    { 5, 0.10M }, { 6, 0.10M }, { 7, 0.10M }, { 8, 0.10M }, { 9, 0.10M },
                    { 10, 0.10M }, { 11, 0.10M }, { 12, 0.10M }, { 13, 0.10M }, { 14, 0.10M },
                    { 15, 0.10M }, { 16, 0.10M }, { 17, 0.10M }, { 18, 0.10M }, { 19, 0.10M },
                    { 20, 0.10M }, { 21, 0.10M }, { 22, 0.10M }, { 23, 0.10M }
                }
            },
            { WorkShiftType.ShiftWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.07M }, { 1, 0.07M }, { 2, 0.07M }, { 3, 0.07M }, { 4, 0.07M },
                    { 5, 0.07M }, { 6, 0.07M }, { 7, 0.07M }, { 8, 0.07M }, { 9, 0.07M },
                    { 10, 0.07M }, { 11, 0.07M }, { 12, 0.07M }, { 13, 0.07M }, { 14, 0.07M },
                    { 15, 0.07M }, { 16, 0.07M }, { 17, 0.07M }, { 18, 0.07M }, { 19, 0.07M },
                    { 20, 0.07M }, { 21, 0.07M }, { 22, 0.07M }, { 23, 0.07M }
                }
            }
        };

        private (decimal min, decimal average, decimal max) GetHeatingConsumption(HeatingType heatingType)
        {
            return heatingType switch
            {
                HeatingType.ElectricHeating => (850, 1000, 1150),
                HeatingType.DistrictHeating => (650, 800, 950),
                HeatingType.GeothermalHeating => (400, 500, 600),
                HeatingType.OilHeating => (400, 500, 600),
                _ => (0, 0, 0)
            };
        }

        private (decimal min, decimal average, decimal max) GetResidentConsumption(int numberOfResidents, HouseType houseType)
        {
            return houseType switch
            {
                HouseType.ApartmentHouse => (numberOfResidents * 300, numberOfResidents * 400, numberOfResidents * 500),
                HouseType.TerracedHouse => (numberOfResidents * 500, numberOfResidents * 600, numberOfResidents * 700),
                HouseType.DetachedHouse => (numberOfResidents * 500, numberOfResidents * 600,numberOfResidents * 700),
                _ => (0, 0, 0)
            };
        }

        private decimal CalculateYearlyCost(decimal fixedPrice, decimal totalConsumption)
        {
            return fixedPrice * totalConsumption / 100;
        }

        private decimal CalculateYearlySpotPrice(IEnumerable<ElectricityPriceData> electricityPriceData, decimal totalConsumption)
        {

            decimal totalSpotPriceCost = electricityPriceData.Where(data => data.Price > 0)
                                                             .Sum(data => data.Price * (decimal)(data.EndDate - data.StartDate).TotalHours);

            int totalHours = (int)electricityPriceData.Sum(data => (data.EndDate - data.StartDate).TotalHours);
            decimal averageSpotPricePerHour = totalSpotPriceCost / totalHours;

            return averageSpotPricePerHour * totalConsumption / 100;
        }

        private decimal CalculateMonthlyAverageHourlySpotPrice(IEnumerable<ElectricityPriceData> electricityPriceData, int month)
        {
            var monthData = electricityPriceData.Where(x => x.StartDate.Month == month);
            if (!monthData.Any()) return 0;

            var totalHours = monthData.Sum(data => (decimal)(data.EndDate - data.StartDate).TotalHours);
            var totalSpotPrice = monthData.Sum(data => data.Price * (decimal)(data.EndDate - data.StartDate).TotalHours);

            return totalSpotPrice / totalHours;
        }

        private decimal CalculateTotalHoursInMonth(int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            return daysInMonth * 24;
        }

        private decimal CalculateAverageYearlySpotPrice(IEnumerable<ElectricityPriceData> electricityPriceData)
        {
            var totalHours = electricityPriceData.Sum(data => (decimal)(data.EndDate - data.StartDate).TotalHours);
            var totalSpotPrice = electricityPriceData.Sum(data => data.Price * (decimal)(data.EndDate - data.StartDate).TotalHours);

            return totalSpotPrice / totalHours;
        }

        private List<MonthlyData> CalculateMonthlyDataAsync(decimal totalAverageConsumption, IEnumerable<ElectricityPriceData> electricityPriceData, decimal fixedPrice, decimal yearlySpotPriceCost, int year)
        {
            var monthlyWeights = new Dictionary<int, decimal>
            {
                { 1, 0.16M }, { 2, 0.14M }, { 3, 0.12M }, { 4, 0.10M },
                { 5, 0.06M }, { 6, 0.04M }, { 7, 0.03M }, { 8, 0.04M },
                { 9, 0.05M }, { 10, 0.08M }, { 11, 0.10M }, { 12, 0.08M }
            };

            var monthlyData = new List<MonthlyData>();

            foreach (var month in Enumerable.Range(1, 12))
            {
                var consumption = totalAverageConsumption * monthlyWeights[month];
                var spotPriceAverageOfMonth = CalculateMonthlyAverageHourlySpotPrice(electricityPriceData, month);

                var fixedPriceTotal = Math.Round(consumption * fixedPrice, 2) / 100;

                var monthlySpotPriceCost = yearlySpotPriceCost * (consumption / totalAverageConsumption);

                var totalHoursInMonth = CalculateTotalHoursInMonth(year, month);
                var averageConsumptionPerHour = Math.Round(consumption / totalHoursInMonth, 2);

                monthlyData.Add(new MonthlyData
                {
                    Month = month,
                    Consumption = Math.Round(consumption, 2),
                    SpotPriceAverageOfMonth = Math.Round(spotPriceAverageOfMonth, 2),
                    FixedPriceAverageOfMonth = fixedPrice,
                    FixedPriceTotal = Math.Round(fixedPriceTotal, 2),
                    SpotPriceTotal = Math.Round(monthlySpotPriceCost, 2),
                    AverageConsumptionPerHour = Math.Round(averageConsumptionPerHour, 2)
                });
            }

            return monthlyData;
        }
    }
}
