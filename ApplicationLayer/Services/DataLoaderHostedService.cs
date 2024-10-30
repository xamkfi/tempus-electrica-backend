using ApplicationLayer.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApplicationLayer.Services
{
    public class DataLoaderHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        public DataLoaderHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dataLoaderService = scope.ServiceProvider.GetRequiredService<ISaveHistoryDataService>();
                await dataLoaderService.LoadDataAsync();
            }
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // No action needed on stop
            return Task.CompletedTask;
        }
    }
}

