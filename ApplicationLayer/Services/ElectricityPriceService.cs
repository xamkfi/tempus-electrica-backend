using ApplicationLayer.Dto;
using ApplicationLayer.Dto.Consumption;
using ApplicationLayer.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ApplicationLayer.Services
{
    public class ElectricityPriceService : IElectricityPriceService
    {
        private readonly IElectricityRepository _electricityRepository;
        private readonly ILogger<ElectricityPriceService> _logger;
        private readonly ConsumptionSettings _consumptionSettings;

        public ElectricityPriceService(
            IElectricityRepository electricityRepository,
            ILogger<ElectricityPriceService> logger,
            IOptions<ConsumptionSettings> consumptionSettings)
        {
            _electricityRepository = electricityRepository ?? throw new ArgumentNullException(nameof(electricityRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _consumptionSettings = consumptionSettings?.Value ?? throw new ArgumentNullException(nameof(consumptionSettings));
        }

        public async Task<List<ElectricityPriceData>> GetElectricityPricesAsync(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("Fetching electricity prices from {startDate} to {endDate}", startDate, endDate);

            var prices = await _electricityRepository.GetPricesForPeriodAsync(startDate, endDate).ConfigureAwait(false);

            if (prices != null && prices.Any())
            {
                _logger.LogInformation("Retrieved {count} electricity price records.", prices.Count());
                return prices.ToList();
            }
            else
            {
                _logger.LogWarning("No electricity prices found for the given period.");
                return new List<ElectricityPriceData>();
            }
        }

        public async Task<ElectricityPriceResultDto> GetElectricityPriceDataAsync(CombinedRequestDtoIn request)
        {
            _logger.LogInformation("Start processing GetElectricityPriceDataAsync for year {Year}", request.Year);
            ValidateRequest(request);

            var startDate = new DateTime(request.Year, 1, 1);
            var endDate = DetermineEndDate(request.Year); // This now considers if the year is current or not

            _logger.LogInformation("Fetching electricity price data from {StartDate} to {EndDate}", startDate, endDate);
            var electricityPriceData = await FetchElectricityPriceDataAsync(startDate, endDate);

            // Calculate total consumption
            var consumptionResult = await Task.Run(() => CalculateTotalConsumption(request));

            // Calculate costs
            var totalFixedPriceCost = CalculateYearlyCost(request.FixedPrice, consumptionResult.AverageConsumption);
            var totalSpotPriceCost = CalculateYearlySpotPrice(electricityPriceData, consumptionResult.AverageConsumption);

            // Calculate costs for min and max consumption
            var minFixedPriceCost = CalculateYearlyCost(request.FixedPrice, consumptionResult.MinConsumption);
            var maxFixedPriceCost = CalculateYearlyCost(request.FixedPrice, consumptionResult.MaxConsumption);
            var minSpotPriceCost = CalculateYearlySpotPrice(electricityPriceData, consumptionResult.MinConsumption);
            var maxSpotPriceCost = CalculateYearlySpotPrice(electricityPriceData, consumptionResult.MaxConsumption);

            // Calculate monthly data
            int lastMonthToConsider = endDate.Month; // Get the last month based on endDate
            var monthlyData = await CalculateMonthlyDataAsync(consumptionResult.AverageConsumption, electricityPriceData, request.FixedPrice, request.Year, endDate);

            var costDifference = Math.Round(Math.Abs(totalFixedPriceCost - totalSpotPriceCost), 2);
            var cheaperOption = totalFixedPriceCost < totalSpotPriceCost ? "Fixed price" : "Spot price";
            var averageHourlySpotPrice = CalculateAverageYearlySpotPrice(electricityPriceData);

            var result = new ElectricityPriceResultDto
            {
                TotalFixedPriceCost = Math.Round(totalFixedPriceCost, 2),
                TotalSpotPriceCost = Math.Round(totalSpotPriceCost, 2),
                CostDifference = costDifference,
                CheaperOption = cheaperOption,
                AverageConsumption = Math.Round(consumptionResult.AverageConsumption, 2),
                MinConsumption = Math.Round(consumptionResult.MinConsumption, 2),
                MaxConsumption = Math.Round(consumptionResult.MaxConsumption, 2),
                AverageHourlySpotPrice = Math.Round(averageHourlySpotPrice, 2),
                MonthlyData = monthlyData,
                MinFixedPriceCost = Math.Round(minFixedPriceCost, 2),
                MaxFixedPriceCost = Math.Round(maxFixedPriceCost, 2),
                MinSpotPriceCost = Math.Round(minSpotPriceCost, 2),
                MaxSpotPriceCost = Math.Round(maxSpotPriceCost, 2)
            };

            _logger.LogInformation("Completed processing GetElectricityPriceDataAsync for year {Year}", request.Year);
            return result;
        }


        #region Validation and Data Retrieval

        private void ValidateRequest(CombinedRequestDtoIn request)
        {
            if (request == null)
            {
                _logger.LogError("Request is null");
                throw new ArgumentNullException(nameof(request));
            }

            // Add additional validation as needed
            request.ValidateModel();
            _logger.LogInformation("Request validated successfully");
        }

        private async Task<List<ElectricityPriceData>> FetchElectricityPriceDataAsync(DateTime startDate, DateTime endDate)
        {
            var electricityPriceData = await _electricityRepository.GetPricesForPeriodAsync(startDate, endDate);

            if (electricityPriceData == null || !electricityPriceData.Any())
            {
                _logger.LogError("No electricity price data found for the specified date range");
                throw new InvalidOperationException("No electricity price data found for the specified date range.");
            }

            _logger.LogInformation("Fetched {Count} electricity price data records", electricityPriceData.Count());
            return electricityPriceData.ToList();
        }

        private DateTime DetermineEndDate(int year)
        {
            DateTime endDate;

            if (year == DateTime.Now.Year)
            {
                // The end date is for the current year, so we check if we need to move one month back
                endDate = DateTime.Now.Date.AddMonths(-1); // Move one month back
                                                           // Set the end date to the last day of that month
                endDate = new DateTime(endDate.Year, endDate.Month, DateTime.DaysInMonth(endDate.Year, endDate.Month));
            }
            else
            {
                // For past years, use the full year, i.e., December 31st of the selected year
                endDate = new DateTime(year, 12, 31);
            }

            _logger.LogInformation("Determined end date as {EndDate} for year {Year}", endDate, year);
            return endDate;
        }



        #endregion

        #region Consumption Calculations

        private ConsumptionResult CalculateTotalConsumption(CombinedRequestDtoIn request)
        {
            var housingConsumption = CalculateHousingConsumption(request.HouseType, request.SquareMeters);
            var workShiftConsumption = CalculateWorkShiftConsumption(request.WorkShiftType);
            var saunaConsumption = CalculateSaunaConsumption(request.HasSauna, request.SaunaHeatingFrequency);
            var fireplaceSavings = CalculateFireplaceSavings(request.HasFireplace, request.FireplaceFrequency);
            var electricCarConsumption = CalculateElectricCarConsumption(request);
            var residentConsumption = CalculateResidentConsumption(request.NumberOfResidents, request.HouseType);
            var heatingConsumption = CalculateHeatingConsumption(request);
            var solarPanelSavings = CalculateSolarPanelSavings(request);
            var floorHeatingConsumption = CalculateFloorHeatingConsumption(request);

            decimal minConsumption = housingConsumption.Min + workShiftConsumption * 365 + saunaConsumption
                                     + electricCarConsumption + residentConsumption.Min + heatingConsumption.Min
                                     + floorHeatingConsumption - fireplaceSavings - solarPanelSavings;

            decimal averageConsumption = housingConsumption.Average + workShiftConsumption * 365 + saunaConsumption
                                         + electricCarConsumption + residentConsumption.Average + heatingConsumption.Average
                                         + floorHeatingConsumption - fireplaceSavings - solarPanelSavings;

            decimal maxConsumption = housingConsumption.Max + workShiftConsumption * 365 + saunaConsumption
                                     + electricCarConsumption + residentConsumption.Max + heatingConsumption.Max
                                     + floorHeatingConsumption - fireplaceSavings - solarPanelSavings;

            _logger.LogInformation("Total consumption calculated: Min={Min}, Average={Average}, Max={Max}",
                minConsumption, averageConsumption, maxConsumption);

            return new ConsumptionResult
            {
                MinConsumption = minConsumption,
                AverageConsumption = averageConsumption,
                MaxConsumption = maxConsumption
            };
        }

        private (decimal Min, decimal Average, decimal Max) CalculateHousingConsumption(HouseType houseType, decimal squareMeters)
        {
            if (squareMeters <= 0)
            {
                _logger.LogError("Square meters must be a positive number");
                throw new ArgumentException("Square meters must be a positive number.");
            }

            return houseType switch
            {
                HouseType.ApartmentHouse => (squareMeters * 14, squareMeters * 20, squareMeters * 28),
                HouseType.TerracedHouse => (squareMeters * 100, squareMeters * 110, squareMeters * 120),
                HouseType.DetachedHouse => (squareMeters * 115, squareMeters * 130, squareMeters * 145),
                HouseType.Cottage => (squareMeters * 110, squareMeters * 120, squareMeters * 130),
                _ => throw new ArgumentException("Invalid house type")
            };
        }

        private decimal CalculateWorkShiftConsumption(WorkShiftType workShiftType)
        {
            return workShiftType switch
            {
                WorkShiftType.DayWorker => 0.3M,
                WorkShiftType.ShiftWorker => 0.4M,
                WorkShiftType.RemoteWorker => 0.5M,
                _ => throw new ArgumentException("Invalid work shift type")
            };
        }

        private decimal CalculateSaunaConsumption(bool hasSauna, int? saunaHeatingFrequency)
        {
            if (!hasSauna || saunaHeatingFrequency == null || saunaHeatingFrequency <= 0)
            {
                return 0;
            }

            return saunaHeatingFrequency.Value * 52 * 13;
        }

        private decimal CalculateFireplaceSavings(bool hasFireplace, int? fireplaceFrequency)
        {
            if (!hasFireplace || fireplaceFrequency == null || fireplaceFrequency <= 0)
            {
                return 0;
            }

            var savingsPerUse = _consumptionSettings.SavingsPerFireplaceUse;
            return fireplaceFrequency.Value * 52 * savingsPerUse;
        }

        private decimal CalculateElectricCarConsumption(CombinedRequestDtoIn request)
        {
            if (!request.HasElectricCar)
            {
                return 0;
            }

            if (request.NumberOfCars == null || request.NumberOfCars <= 0)
            {
                _logger.LogError("Number of cars must be a positive number when HasElectricCar is true");
                throw new ArgumentException("Number of cars must be a positive number when HasElectricCar is true.");
            }

            if (request.ElectricCarkWhUsagePerYear == null || request.ElectricCarkWhUsagePerYear <= 0)
            {
                _logger.LogError("Electric car kWh usage per year must be a positive number");
                throw new ArgumentException("Electric car kWh usage per year must be a positive number.");
            }

            return request.NumberOfCars.Value * request.ElectricCarkWhUsagePerYear.Value;
        }

        private (decimal Min, decimal Average, decimal Max) CalculateResidentConsumption(int numberOfResidents, HouseType houseType)
        {
            if (numberOfResidents <= 0)
            {
                _logger.LogError("Number of residents must be a positive number");
                throw new ArgumentException("Number of residents must be a positive number.");
            }

            return houseType switch
            {
                HouseType.ApartmentHouse => (numberOfResidents * 300, numberOfResidents * 400, numberOfResidents * 500),
                HouseType.TerracedHouse => (numberOfResidents * 500, numberOfResidents * 600, numberOfResidents * 700),
                HouseType.DetachedHouse => (numberOfResidents * 500, numberOfResidents * 600, numberOfResidents * 700),
                _ => throw new ArgumentException("Invalid house type")
            };
        }

        private (decimal Min, decimal Average, decimal Max) CalculateHeatingConsumption(CombinedRequestDtoIn request)
        {
            if (request.HouseType == HouseType.ApartmentHouse)
            {
                return (0, 0, 0);
            }

            return request.HeatingType switch
            {
                HeatingType.ElectricHeating => (700, 1000, 1300),
                HeatingType.DistrictHeating => (650, 800, 950),
                HeatingType.GeothermalHeating => (400, 500, 600),
                HeatingType.OilHeating => (400, 500, 600),
                _ => throw new ArgumentException("Invalid heating type")
            };
        }

        private decimal CalculateSolarPanelSavings(CombinedRequestDtoIn request)
        {
            if (!request.HasSolarPanel || request.SolarPanel == null || request.SolarPanel <= 0)
            {
                return 0;
            }

            var monthlyProduction = CalculateMonthlySolarPanelProduction(request.SolarPanel.Value);
            return monthlyProduction.Values.Sum();
        }

        private Dictionary<int, decimal> CalculateMonthlySolarPanelProduction(int numberOfPanels)
        {
            return _consumptionSettings.MonthlyProductionPerPanel
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value * numberOfPanels);
        }

        private decimal CalculateFloorHeatingConsumption(CombinedRequestDtoIn request)
        {
            if (!request.HasFloorHeating || request.FloorSquareMeters == null || request.FloorSquareMeters <= 0)
            {
                return 0;
            }

            var consumptionPerSquareMeter = _consumptionSettings.FloorHeatingConsumptionPerSquareMeter;
            return request.FloorSquareMeters.Value * consumptionPerSquareMeter;
        }

        #endregion

        #region Cost Calculations

        private decimal CalculateYearlyCost(decimal fixedPrice, decimal totalConsumption)
        {
            if (fixedPrice <= 0 || totalConsumption <= 0)
            {
                _logger.LogError("Fixed price and total consumption must be positive numbers");
                throw new ArgumentException("Fixed price and total consumption must be positive numbers.");
            }

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

        private decimal CalculateAverageYearlySpotPrice(List<ElectricityPriceData> electricityPriceData)
        {
            var totalHours = electricityPriceData.Sum(data => (decimal)(data.EndDate - data.StartDate).TotalHours);
            var totalSpotPrice = electricityPriceData.Sum(data => data.Price * (decimal)(data.EndDate - data.StartDate).TotalHours);

            return totalSpotPrice / totalHours;
        }

        #endregion

        #region Monthly Data Calculations

        private async Task<List<MonthlyData>> CalculateMonthlyDataAsync(decimal totalAverageConsumption, List<ElectricityPriceData> electricityPriceData, decimal fixedPrice, int year, DateTime endDate)
        {
            var monthlyData = new ConcurrentBag<MonthlyData>();

            // Use the endDate's month directly for current year
            int endMonth = endDate.Month;

            // If the endDate is December or full year past is selected, we consider all months of the specified year
            if (endDate.Year > year)
            {
                endMonth = 12; // When data spills into the next year, we consider full 12 months for the selected year
            }

            var tasks = Enumerable.Range(1, endMonth).Select(month => Task.Run(() =>
            {
                var consumption = totalAverageConsumption * _consumptionSettings.MonthlyWeights[month];
                var spotPriceAverageOfMonth = CalculateMonthlyAverageHourlySpotPrice(electricityPriceData, month);

                var fixedPriceTotal = Math.Round(consumption * fixedPrice, 2) / 100;

                var monthlySpotPriceCost = CalculateMonthlySpotPrice(electricityPriceData, consumption, month);

                var totalHoursInMonth = CalculateTotalHoursInMonth(year, month);
                var averageConsumptionPerHour = Math.Round(consumption / totalHoursInMonth, 2);

                var data = new MonthlyData
                {
                    Month = month,
                    Consumption = Math.Round(consumption, 2),
                    SpotPriceAverageOfMonth = Math.Round(spotPriceAverageOfMonth, 2),
                    FixedPriceAverageOfMonth = fixedPrice,
                    FixedPriceTotal = Math.Round(fixedPriceTotal, 2),
                    SpotPriceTotal = Math.Round(monthlySpotPriceCost, 2),
                    AverageConsumptionPerHour = Math.Round(averageConsumptionPerHour, 2)
                };

                monthlyData.Add(data);
            }));

            await Task.WhenAll(tasks);

            return monthlyData.OrderBy(md => md.Month).ToList();
        }


        private decimal CalculateMonthlyAverageHourlySpotPrice(List<ElectricityPriceData> electricityPriceData, int month)
        {
            var monthData = electricityPriceData.Where(x => x.StartDate.Month == month);
            if (!monthData.Any()) return 0;

            var totalHours = monthData.Sum(data => (decimal)(data.EndDate - data.StartDate).TotalHours);
            var totalSpotPrice = monthData.Sum(data => data.Price * (decimal)(data.EndDate - data.StartDate).TotalHours);

            return totalSpotPrice / totalHours;
        }

        private decimal CalculateMonthlySpotPrice(IEnumerable<ElectricityPriceData> electricityPriceData, decimal monthlyConsumption, int month)
        {
            // Filter the data for the specific month
            var monthData = electricityPriceData.Where(x => x.StartDate.Month == month && x.Price > 0);

            decimal totalSpotPriceCost = monthData.Sum(data => data.Price * (decimal)(data.EndDate - data.StartDate).TotalHours);

            int totalHours = (int)monthData.Sum(data => (data.EndDate - data.StartDate).TotalHours);

            // Check if totalHours is zero to avoid divide by zero exception
            if (totalHours == 0)
            {
                _logger.LogWarning("Total hours for month {Month} is zero. Returning zero for monthly spot price.", month);
                return 0; // or handle this case according to your business logic
            }

            decimal averageSpotPricePerHour = totalSpotPriceCost / totalHours;

            return averageSpotPricePerHour * monthlyConsumption / 100;
        }


        private decimal CalculateTotalHoursInMonth(int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            return daysInMonth * 24;
        }

        #endregion
    }
}