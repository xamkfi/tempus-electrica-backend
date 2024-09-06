using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;


namespace Infrastructure.Repositories
{
    public class ElectricityRepository : IElectricityRepository
    {
        private readonly ElectricityDbContext _context;
        private readonly ILogger<ElectricityRepository> _logger;
        private readonly IMemoryCache _cache;

        public ElectricityRepository(ElectricityDbContext context, ILogger<ElectricityRepository> logger, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<bool> AddRangeElectricityPricesAsync(IEnumerable<ElectricityPriceData> electricityPriceDataDtoIn)
        {
            if (electricityPriceDataDtoIn == null || !electricityPriceDataDtoIn.Any())
            {
                _logger.LogWarning("Empty or null data provided to AddRangeElectricityPricesAsync.");
                return false;
            }

            try
            {
                await _context.ElectricityPriceDatas.AddRangeAsync(electricityPriceDataDtoIn);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Electricity price data successfully added to the database.");
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error occurred while adding electricity price data to the database.");
                return false;
            }
        }

        public async Task<bool> IsDuplicateAsync(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation($"Checking for duplicates between {startDate} and {endDate}.");
            bool isDuplicate = await _context.ElectricityPriceDatas
                .AnyAsync(epd => epd.StartDate == startDate && epd.EndDate == endDate);
            _logger.LogInformation($"Duplicate check result: {isDuplicate}");
            return isDuplicate;
        }

        public async Task<List<ElectricityPriceData>> GetDuplicatesAsync(List<DateTime> startDates, List<DateTime> endDates)
        {
            if (startDates == null || endDates == null)
            {
                _logger.LogWarning("Null startDates or endDates provided to GetDuplicatesAsync.");
                return new List<ElectricityPriceData>();
            }

            _logger.LogInformation("Fetching duplicate electricity price data based on provided date ranges.");
            return await _context.ElectricityPriceDatas
                .Where(e => startDates.Contains(e.StartDate) && endDates.Contains(e.EndDate))
                .ToListAsync();
        }

        public async Task<bool> AddBatchElectricityPricesAsync(IEnumerable<ElectricityPriceData> electricityPriceData)
        {
            if (electricityPriceData == null || !electricityPriceData.Any())
            {
                _logger.LogWarning("Empty or null data provided to AddBatchElectricityPricesAsync.");
                return false;
            }

            try
            {
                await _context.ElectricityPriceDatas.AddRangeAsync(electricityPriceData);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Batch electricity price data successfully added to the database.");
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error occurred while adding batch electricity price data to the database.");
                return false;
            }
        }

        public async Task<IEnumerable<ElectricityPriceData>> GetPricesForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            // List to accumulate cached data
            var cachedDataList = new List<ElectricityPriceData>();
            bool isCompleteDataFromCache = true;

            // Retrieve the list of cached ranges
            var cachedRanges = _cache.Get<List<(DateTime CacheStartDate, DateTime CacheEndDate, string CacheKey)>>("CachedRanges")
                                ?? new List<(DateTime, DateTime, string)>();

            foreach (var range in cachedRanges)
            {
                if (startDate >= range.CacheStartDate && endDate <= range.CacheEndDate)
                {
                    if (_cache.TryGetValue(range.CacheKey, out IEnumerable<ElectricityPriceData>? cachedData))
                    {
                        if (cachedData != null)
                        {
                            cachedDataList.AddRange(cachedData.Where(epd => epd.StartDate >= startDate && epd.EndDate <= endDate));
                        }
                    }
                }
                else
                {
                    isCompleteDataFromCache = false;
                }
            }

            if (!isCompleteDataFromCache || !cachedDataList.Any())
            {
                _logger.LogInformation($"Cache miss. Fetching electricity prices for the period: StartDate = {startDate}, EndDate = {endDate}");

                // Fetch data from the database for the missing period
                var dbData = await _context.ElectricityPriceDatas
                                           .Where(epd => epd.StartDate >= startDate && epd.EndDate <= endDate)
                                           .ToListAsync();

                cachedDataList.AddRange(dbData);

                // Cache the new data range
                string newCacheKey = $"GetPricesForPeriod_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
                var cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30));

                _cache.Set(newCacheKey, dbData, cacheOptions);

                // Update the cached ranges list
                cachedRanges.Add((startDate, endDate, newCacheKey));
                _cache.Set("CachedRanges", cachedRanges, cacheOptions);
            }
            else
            {
                _logger.LogInformation($"Cache hit. Returning cached electricity prices for the period: StartDate = {startDate}, EndDate = {endDate}");
            }

            return cachedDataList;
        }
    }
}
