using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace ApplicationLayer.Services
{


    public class VaultSecret
    {
        public string? DbConnectionString { get; set; }

    }
    public interface IKeyVaultSecretManager
    {
        Task<VaultSecret?> GetSecretAsync();
        Task RefreshSecretsAsync();
    }

    public class KeyVaultSecretManager : IKeyVaultSecretManager
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<KeyVaultSecretManager> _logger;
        private readonly SecretClient _secretClient;
        private static readonly ConcurrentDictionary<string, string> _secretsCache = new ConcurrentDictionary<string, string>();

        public KeyVaultSecretManager(IConfiguration configuration, ILogger<KeyVaultSecretManager> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var keyVaultEndpoint = _configuration["KeyVault:BaseUrl"];

            if (string.IsNullOrWhiteSpace(keyVaultEndpoint))
            {
                throw new ArgumentException("KeyVault:BaseUrl configuration is missing or empty.");
            }

            _secretClient = new SecretClient(new Uri(keyVaultEndpoint), new DefaultAzureCredential());
        }

        public async Task<VaultSecret?> GetSecretAsync()
        {
            const string secretName = "DbConnectionString";

            if (_secretsCache.TryGetValue(secretName, out string? cachedSecret))
            {
                return new VaultSecret { DbConnectionString = cachedSecret };
            }

            var fetchedSecret = await FetchAndCacheSecretAsync(secretName);
            return new VaultSecret { DbConnectionString = fetchedSecret };
        }

        public async Task RefreshSecretsAsync()
        {
            try
            {
                var secretName = "DbConnectionString";
                await FetchAndCacheSecretAsync(secretName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while refreshing the DbAddress secret from Key Vault.");
            }
        }

        private async Task<string> FetchAndCacheSecretAsync(string secretName)
        {
            try
            {
                var response = await _secretClient.GetSecretAsync(secretName);
                var secretValue = response.Value.Value;
                _secretsCache.AddOrUpdate(secretName, secretValue, (key, oldValue) => secretValue);
                return secretValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to fetch secret {secretName} from Key Vault.");
                return "Failed to fetch secret";
            }
        }
    }
}
