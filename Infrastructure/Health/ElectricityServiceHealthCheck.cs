using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace Infrastructure.Health
{
    public class ElectricityServiceHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ElectricityServiceHealthCheck> _logger;
        private readonly IConfiguration _configuration;

        public ElectricityServiceHealthCheck(IHttpClientFactory httpClientFactory, ILogger<ElectricityServiceHealthCheck> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                var url = _configuration["HealthChecks:ElectricityService:Url"];
                var jwtKey = _configuration["Jwt:Key"];
                var issuer = _configuration["Jwt:Issuer"];
                var audience = _configuration["Jwt:Audience"];

                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogError("Health check URL is not configured.");
                    return HealthCheckResult.Unhealthy("Health check URL is not configured.");
                }
                if (string.IsNullOrEmpty(jwtKey))
                {
                    _logger.LogError("JTW key is not configured.");
                    return HealthCheckResult.Unhealthy("JWT key is not configured.");
                }

                var token = GenerateJwtToken(jwtKey, issuer, audience);
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                _logger.LogInformation("JWT Token: {Token}", token);

                var sampleData = new { prices = new[] { new { price = 0, startDate = "2010-01-01T10:10:00.365Z", endDate = "2010-10-11T10:11:00.365Z" } } };

                var content = new StringContent(JsonSerializer.Serialize(sampleData), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("ElectricityPriceData endpoint is healthy.");
                    return HealthCheckResult.Healthy("ElectricityPriceData endpoint is healthy.");
                }

                _logger.LogWarning($"ElectricityPriceData endpoint is unhealhty. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
                return HealthCheckResult.Unhealthy($"ElectricityPriceData endpoint is unhealhty. Status code: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Exception occurred while checking ElectricityPriceData endpoint health.");
                return HealthCheckResult.Unhealthy("Exception occurred while checking ElectricityPriceData endpoint health.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timed out while checking ElectricityPriceData endpoint health.");
                return HealthCheckResult.Unhealthy("Request timed out while checking ElectricityPriceData endpoint health.");
            }
        }

        private string GenerateJwtToken(string key, string issuer, string audience)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                expires: DateTime.Now.AddMinutes(10),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
