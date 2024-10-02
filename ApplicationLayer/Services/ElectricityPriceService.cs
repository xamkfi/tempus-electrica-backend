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

        public async Task<(decimal totalFixedPriceCost, decimal totalSpotPriceCost,
            decimal costDifference, string cheaperOption, decimal totalAverageConsumption,
            decimal totalMinConsumption, decimal totalMaxConsumption,
            decimal averageHourlySpotPrice, List<MonthlyData> monthlyData)>
            GetElectricityPriceDataAsync(CombinedRequestDtoIn request)
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

            // Calculate the yearly fixed price cost
            var totalFixedPriceCost = CalculateYearlyCost(request.FixedPrice, totalAverageConsumption);

            // Calculate the yearly spot price cost using the load profile
            var totalSpotPriceCost = CalculateYearlySpotPrice(electricityPriceData, totalAverageConsumption, request.WorkShiftType);

            var monthlyData = CalculateMonthlyDataAsync(totalAverageConsumption, electricityPriceData, request.FixedPrice, totalSpotPriceCost, request.Year, request.WorkShiftType);
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
            // Adjusted savings per fireplace use to a realistic value
            const decimal savingsPerUse = 8M; // Average savings per fireplace use in kWh
            return hasFireplace ? (fireplaceFrequency ?? 0) * 52 * savingsPerUse : 0;
        }

        private decimal CalculateElectricCarConsumption(bool hasElectricCar, int numberOfCars, int? electricCarkWhUsagePerYear)
        {
            if (!hasElectricCar)
                return 0;

            return numberOfCars * (electricCarkWhUsagePerYear ?? 0);
        }

        private (decimal min, decimal average, decimal max) GetHousingConsumption(HouseType houseType, int squareMeters)
        {
            // Adjusted housing consumption to more realistic values
            return houseType switch
            {
                HouseType.ApartmentHouse => (squareMeters * 20, squareMeters * 25, squareMeters * 30),
                HouseType.TerracedHouse => (squareMeters * 30, squareMeters * 35, squareMeters * 40),
                HouseType.DetachedHouse => (squareMeters * 40, squareMeters * 45, squareMeters * 50),
                HouseType.Cottage => (squareMeters * 35, squareMeters * 40, squareMeters * 45),
                _ => (0, 0, 0)
            };
        }

        private decimal GetWorkShiftConsumption(WorkShiftType workShiftType)
        {
            // Adjusted daily consumption values for work shifts
            return workShiftType switch
            {
                WorkShiftType.DayWorker => 2M,      // 2 kWh/day
                WorkShiftType.ShiftWorker => 3M,    // 3 kWh/day
                WorkShiftType.RemoteWorker => 5M,   // 5 kWh/day
                _ => 2M
            };
        }

        // Updated hourly weights for load profiles based on work shift type
        private readonly Dictionary<WorkShiftType, Dictionary<int, decimal>> _workShiftWeights = new()
        {
            { WorkShiftType.DayWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.02M }, { 1, 0.02M }, { 2, 0.02M }, { 3, 0.02M }, { 4, 0.02M },
                    { 5, 0.02M }, { 6, 0.03M }, { 7, 0.03M }, { 8, 0.03M }, { 9, 0.03M },
                    { 10, 0.03M }, { 11, 0.03M }, { 12, 0.03M }, { 13, 0.03M }, { 14, 0.03M },
                    { 15, 0.03M }, { 16, 0.03M }, { 17, 0.05M }, { 18, 0.07M }, { 19, 0.08M },
                    { 20, 0.08M }, { 21, 0.07M }, { 22, 0.05M }, { 23, 0.04M }
                }
            },
            { WorkShiftType.RemoteWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.02M }, { 1, 0.02M }, { 2, 0.02M }, { 3, 0.02M }, { 4, 0.02M },
                    { 5, 0.03M }, { 6, 0.04M }, { 7, 0.05M }, { 8, 0.06M }, { 9, 0.07M },
                    { 10, 0.07M }, { 11, 0.07M }, { 12, 0.07M }, { 13, 0.07M }, { 14, 0.07M },
                    { 15, 0.07M }, { 16, 0.07M }, { 17, 0.06M }, { 18, 0.05M }, { 19, 0.04M },
                    { 20, 0.03M }, { 21, 0.03M }, { 22, 0.02M }, { 23, 0.02M }
                }
            },
            { WorkShiftType.ShiftWorker, new Dictionary<int, decimal>
                {
                    { 0, 0.04M }, { 1, 0.04M }, { 2, 0.04M }, { 3, 0.04M }, { 4, 0.04M },
                    { 5, 0.05M }, { 6, 0.05M }, { 7, 0.05M }, { 8, 0.05M }, { 9, 0.05M },
                    { 10, 0.05M }, { 11, 0.05M }, { 12, 0.05M }, { 13, 0.05M }, { 14, 0.05M },
                    { 15, 0.05M }, { 16, 0.05M }, { 17, 0.05M }, { 18, 0.05M }, { 19, 0.05M },
                    { 20, 0.05M }, { 21, 0.05M }, { 22, 0.05M }, { 23, 0.05M }
                }
            }
        };

        private (decimal min, decimal average, decimal max) GetHeatingConsumption(HeatingType heatingType)
        {
            // Adjusted heating consumption values to reflect realistic figures
            return heatingType switch
            {
                HeatingType.ElectricHeating => (10000, 15000, 20000),
                HeatingType.DistrictHeating => (8000, 12000, 16000),
                HeatingType.GeothermalHeating => (5000, 7500, 10000),
                HeatingType.OilHeating => (8000, 12000, 16000),
                _ => (0, 0, 0)
            };
        }

        private (decimal min, decimal average, decimal max) GetResidentConsumption(int numberOfResidents, HouseType houseType)
        {
            // Adjusted per-resident consumption values
            return houseType switch
            {
                HouseType.ApartmentHouse => (numberOfResidents * 1000, numberOfResidents * 1500, numberOfResidents * 2000),
                HouseType.TerracedHouse => (numberOfResidents * 1200, numberOfResidents * 1700, numberOfResidents * 2200),
                HouseType.DetachedHouse => (numberOfResidents * 1500, numberOfResidents * 2000, numberOfResidents * 2500),
                _ => (0, 0, 0)
            };
        }

        private decimal CalculateYearlyCost(decimal fixedPrice, decimal totalConsumption)
        {
            return fixedPrice * totalConsumption / 100;
        }

        private decimal CalculateYearlySpotPrice(IEnumerable<ElectricityPriceData> electricityPriceData, decimal totalConsumption, WorkShiftType workShiftType)
        {
            // Use load profiles to distribute consumption across hours
            var hourlyWeights = _workShiftWeights[workShiftType];
            decimal totalWeight = hourlyWeights.Sum(x => x.Value);

            // Create a dictionary to hold hourly consumption
            var hourlyConsumption = new Dictionary<int, decimal>();
            foreach (var kvp in hourlyWeights)
            {
                hourlyConsumption[kvp.Key] = (totalConsumption * kvp.Value) / totalWeight;
            }

            decimal totalSpotPriceCost = 0;

            // Calculate the cost based on actual spot prices and hourly consumption
            foreach (var data in electricityPriceData)
            {
                int hour = data.StartDate.Hour;
                if (hourlyConsumption.ContainsKey(hour))
                {
                    decimal consumption = hourlyConsumption[hour];
                    totalSpotPriceCost += (data.Price * consumption) / 100;
                }
            }

            return totalSpotPriceCost;
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

        private List<MonthlyData> CalculateMonthlyDataAsync(decimal totalAverageConsumption, IEnumerable<ElectricityPriceData> electricityPriceData, decimal fixedPrice, decimal yearlySpotPriceCost, int year, WorkShiftType workShiftType)
        {
            // Adjusted monthly weights based on typical consumption patterns in Finland
            var monthlyWeights = new Dictionary<int, decimal>
            {
                { 1, 0.12M }, { 2, 0.10M }, { 3, 0.09M }, { 4, 0.08M },
                { 5, 0.07M }, { 6, 0.06M }, { 7, 0.06M }, { 8, 0.06M },
                { 9, 0.07M }, { 10, 0.09M }, { 11, 0.10M }, { 12, 0.10M }
            };

            var monthlyData = new List<MonthlyData>();

            foreach (var month in Enumerable.Range(1, 12))
            {
                var consumption = totalAverageConsumption * monthlyWeights[month];
                var spotPriceAverageOfMonth = CalculateMonthlyAverageHourlySpotPrice(electricityPriceData, month);

                var fixedPriceTotal = Math.Round(consumption * fixedPrice, 2) / 100;

                var monthlySpotPriceCost = CalculateMonthlySpotPrice(electricityPriceData, consumption, month, workShiftType);

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

        private decimal CalculateMonthlySpotPrice(IEnumerable<ElectricityPriceData> electricityPriceData, decimal monthlyConsumption, int month, WorkShiftType workShiftType)
        {
            // Filter data for the specific month
            var monthData = electricityPriceData.Where(x => x.StartDate.Month == month);

            // Use load profiles to distribute consumption across hours
            var hourlyWeights = _workShiftWeights[workShiftType];
            decimal totalWeight = hourlyWeights.Sum(x => x.Value);

            // Create a dictionary to hold hourly consumption
            var hourlyConsumption = new Dictionary<int, decimal>();
            foreach (var kvp in hourlyWeights)
            {
                hourlyConsumption[kvp.Key] = (monthlyConsumption * kvp.Value) / totalWeight;
            }

            decimal monthlySpotPriceCost = 0;

            // Calculate the cost based on actual spot prices and hourly consumption
            foreach (var data in monthData)
            {
                int hour = data.StartDate.Hour;
                if (hourlyConsumption.ContainsKey(hour))
                {
                    decimal consumption = hourlyConsumption[hour];
                    monthlySpotPriceCost += (data.Price * consumption) / 100;
                }
            }

            return monthlySpotPriceCost;
        }
    }
}
