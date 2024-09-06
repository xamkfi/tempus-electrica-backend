using ApplicationLayer.Dto;
using ApplicationLayer.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DatabaseMicroService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class ElectricityPriceDataController : ControllerBase
    {
        private readonly ILogger<ElectricityPriceDataController> _logger;
        private readonly ISaveHistoryDataService _saveHistoryDataService;
        private readonly IDateRangeDataService _dateRangeDataService;

        public ElectricityPriceDataController(
            ILogger<ElectricityPriceDataController> logger,
            ISaveHistoryDataService saveHistoryDataService,
            IDateRangeDataService dateRangeDataService)
        {
            _logger = logger;
            _saveHistoryDataService = saveHistoryDataService;
            _dateRangeDataService = dateRangeDataService;
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

        private void LogRequestDetails(string action)
        {
            _logger.LogInformation($"Received request to {action}.");
            _logger.LogInformation("Request received: {Method} {Path}", HttpContext.Request.Method, HttpContext.Request.Path);
            _logger.LogInformation("Query Parameters: {Query}", HttpContext.Request.QueryString);
            _logger.LogInformation("Headers: {Headers}", JsonSerializer.Serialize(HttpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())));
        }
    }
}
