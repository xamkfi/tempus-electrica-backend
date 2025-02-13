using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DatabaseMicroService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ElectricityPriceDataController : ControllerBase
    {
        private readonly ILogger<ElectricityPriceDataController> _logger;
        private readonly ISaveHistoryDataService _saveHistoryDataService;
        private readonly IDateRangeDataService _dateRangeDataService;
        private readonly IElectricityPriceService _electricityService;
        private readonly ICalculateFingridConsumptionPrice _calculateFinGridConsumptionPrice;

        public ElectricityPriceDataController(
            ILogger<ElectricityPriceDataController> logger,
            ISaveHistoryDataService saveHistoryDataService,
            IDateRangeDataService dateRangeDataService,
            IElectricityPriceService electricityService,
            ICalculateFingridConsumptionPrice calculateFingridConsumptionPrice)
        {
            _logger = logger;
            _saveHistoryDataService = saveHistoryDataService;
            _dateRangeDataService = dateRangeDataService;
            _electricityService = electricityService;
            _calculateFinGridConsumptionPrice = calculateFingridConsumptionPrice;
        }



        [HttpGet("GetPricesForPeriod")]
        public async Task<IActionResult> GetPricesForPeriod([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            LogRequestDetails($"get prices for the period: StartDate = {startDate}, EndDate = {endDate}");

            if (startDate == null || endDate == null)
                return BadRequest("Missing date values");
            else if (startDate == endDate)
            {
                endDate = endDate.Value.AddHours(24);
            }

            try
            {
                var prices = await _dateRangeDataService.GetPricesForPeriodAsync(startDate.Value, endDate.Value);
                _logger.LogInformation($"Successfully retrieved {prices.Count()} prices for the specified period.");
                return Ok(prices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while retrieving prices for the specified period.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error getting electricity prices for the period.");
            }
        }

        [HttpPost("UploadFinGridConsumptionFile")]
        public async Task<IActionResult> UploadCsv(IFormFile file, [FromQuery] decimal? fixedPrice, decimal? marginal)
        {
            // Start a stopwatch to measure response time
            var stopwatch = Stopwatch.StartNew();

            LogRequestDetails("upload Fingrid consumption file");

            if (file == null || file.Length == 0)
                return BadRequest("File not provided.");

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), file.FileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                if (!fixedPrice.HasValue)
                {
                    return BadRequest("Fixed price not received");
                }

                var result = await _calculateFinGridConsumptionPrice.CalculateTotalConsumptionPricesAsync(filePath, fixedPrice, marginal);

                // Stop the stopwatch and log the response time and status code
                stopwatch.Stop();
                _logger.LogInformation("Response Time: {Time} ms", stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("Response Status Code: {StatusCode}", StatusCodes.Status200OK);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error processing file.");
            }
            finally
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
        }

        private void LogRequestDetails(string action)
        {
            _logger.LogInformation($"Received request to {action}.");
            _logger.LogInformation("Request received: {Method} {Path}", HttpContext.Request.Method, HttpContext.Request.Path);
            _logger.LogInformation("Query Parameters: {Query}", HttpContext.Request.QueryString);
            _logger.LogInformation("Headers: {Headers}", JsonSerializer.Serialize(HttpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())));
        }

        [HttpPost("CalculatePriceAndConsumption")]
        public async Task<ActionResult> GetElectricityPricesData([FromQuery] CombinedRequestDtoIn request)
        {
            // Start a stopwatch to measure response time
            var stopwatch = Stopwatch.StartNew();

            LogRequestDetails("calculate price and consumption");
            _logger.LogInformation("Request Body: {Request}", JsonSerializer.Serialize(request));

            try
            {
                // Updated to match the new method signature
                var result = await _electricityService.GetElectricityPriceDataAsync(request);

                var calculationYears = $"{request.Year} - {request.Year + 1}";

                var response = new
                {
                    TotalFixedPriceCost = result.TotalFixedPriceCost,
                    TotalSpotPriceCost = result.TotalSpotPriceCost,
                    TotalDirectiveConsumption = result.AverageConsumption,
                    EstimatedMinConsumption = result.MinConsumption,
                    EstimatedMaxConsumption = result.MaxConsumption,
                    MinFixedPriceCost = result.MinFixedPriceCost,
                    MaxFixedPriceCost = result.MaxFixedPriceCost,
                    MinSpotPriceCost = result.MinSpotPriceCost,
                    MaxSpotPriceCost = result.MaxSpotPriceCost,
                    CalculationYears = calculationYears,

                    CheaperOption = result.CheaperOption,
                    CostDifference = result.CostDifference,
                    AverageHourlySpotPrice = result.AverageHourlySpotPrice,
                    MonthlyData = result.MonthlyData,


                };

                var options = new JsonSerializerOptions
                {
                    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                    WriteIndented = true
                };

                var jsonResponse = JsonSerializer.Serialize(response, options);

                // Stop the stopwatch and log the response time and status code
                stopwatch.Stop();
                _logger.LogInformation("Response Time: {Time} ms", stopwatch.ElapsedMilliseconds);
                _logger.LogInformation("Response Status Code: {StatusCode}", StatusCodes.Status200OK);

                return Ok(jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while calculating price and consumption.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while calculating price and consumption.");
            }
        }
    }
}
