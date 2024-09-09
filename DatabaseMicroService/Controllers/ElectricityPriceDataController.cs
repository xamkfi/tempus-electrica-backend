using Application.Dto;
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

        

        [HttpPost("UploadHistoryData")]
        [Authorize]
        public async Task<IActionResult> SaveElectricityPrices(IFormFile file)
        {
            LogRequestDetails("upload historical electricity price data");

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file received in the request.");
                return BadRequest("File not received");
            }

            try
            {
                await _saveHistoryDataService.ProcessCsvFileAsync(file);
                _logger.LogInformation("Historical data successfully processed and saved to the database.");
                return Ok("Data received and saved to the database.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while processing historical data file.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Something went wrong.");
            }
        }

        [HttpGet("GetPricesForPeriod")]
        public async Task<IActionResult> GetPricesForPeriod([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            LogRequestDetails($"get prices for the period: StartDate = {startDate}, EndDate = {endDate}");

            if (startDate == null || endDate == null)
                return BadRequest("Missing date values");

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
        public async Task<IActionResult> UploadCsv(IFormFile file, [FromQuery] decimal? fixedPrice)
        {
            // Start a stopwatch to measure response time
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Request received: {Method} {Path}", HttpContext.Request.Method, HttpContext.Request.Path);
            _logger.LogInformation("Query Parameters: {Query}", HttpContext.Request.QueryString);
            _logger.LogInformation("Headers: {Headers}", JsonSerializer.Serialize(HttpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())));

            if (HttpContext.Request.Method == HttpMethods.Post || HttpContext.Request.Method == HttpMethods.Put)
            {
                using (var reader = new StreamReader(HttpContext.Request.Body))
                {
                    var body = await reader.ReadToEndAsync();
                    _logger.LogInformation("Request Body: {Body}", body);
                }
            }

            if (file == null || file.Length == 0)
                return BadRequest("File not provided.");

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), file.FileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            if (!fixedPrice.HasValue)
            {
                return BadRequest("Fixed price not received");
            }

            try
            {
                var (totalSpotPrice, totalFixedPrice, cheaperOption, totalConsumption, priceDifference, equivalentFixedPrice, monthlyData, weeklyData, dailyData, startDate, endDate) = await _calculateFinGridConsumptionPrice.CalculateTotalConsumptionPricesAsync(filePath, fixedPrice);

                var result = new
                {
                    TotalSpotPrice = totalSpotPrice,
                    TotalFixedPrice = totalFixedPrice,
                    CheaperOption = cheaperOption,
                    PriceDifference = priceDifference,
                    TotalConsumption = totalConsumption,
                    EquivalentFixedPrice = equivalentFixedPrice,
                    MonthlyData = monthlyData,
                    WeeklyData = weeklyData,
                    DailyData = dailyData,
                    StartDate = startDate,
                    EndDate = endDate,

                };

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

            _logger.LogInformation("Request received: {Method} {Path}", HttpContext.Request.Method, HttpContext.Request.Path);
            _logger.LogInformation("Query Parameters: {Query}", HttpContext.Request.QueryString);
            _logger.LogInformation("Headers: {Headers}", JsonSerializer.Serialize(HttpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())));
            _logger.LogInformation("Request Body: {Request}", JsonSerializer.Serialize(request));

            try
            {
                var (totalFixedPriceCost, totalSpotPriceCost, costDifference, cheaperOption, totalConsumption, averageHourlySpotPrice, monthlyData) =
                    await _electricityService.GetElectricityPriceDataAsync(request);

                var calculationYears = $"{request.Year - 1} - {request.Year}";

                var response = new
                {
                    TotalFixedPriceCost = totalFixedPriceCost,
                    TotalSpotPriceCost = totalSpotPriceCost,
                    TotalDirectiveConsumption = totalConsumption,
                    CheaperOption = cheaperOption,
                    CostDifference = costDifference,
                    AverageHourlySpotPrice = averageHourlySpotPrice,
                    MonthlyData = monthlyData,
                    CalculationYears = calculationYears
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
