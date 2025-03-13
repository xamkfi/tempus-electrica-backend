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
        public async Task<DateTime> GetOldestStartDateAsync()
        {
            return await _context.ElectricityPriceDatas
                .MinAsync(e => (DateTime?)e.StartDate) ?? DateTime.MaxValue;
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
            // Initial validation
            if (startDate > endDate)
            {
                _logger.LogError($"Invalid date range: StartDate ({startDate}) is after EndDate ({endDate})");
                return Enumerable.Empty<ElectricityPriceData>();
            }
            else if (startDate == endDate)
            {

                endDate = endDate.AddHours(23).AddMinutes(59).AddSeconds(59);
            }

            var cachedRanges = _cache.Get<List<(DateTime CacheStartDate, DateTime CacheEndDate, string CacheKey)>>("CachedRanges")
                                ?? new List<(DateTime, DateTime, string)>();
            var result = new List<ElectricityPriceData>();
            var datesToFetch = new List<(DateTime start, DateTime end)> { (startDate, endDate) };
            
            //add missing dates and remove overlapping dates
            foreach (var range in cachedRanges)
            {
                if (_cache.TryGetValue(range.CacheKey, out IEnumerable<ElectricityPriceData>? cachedData) && cachedData != null)
                {
                    var overlappingRanges = datesToFetch.ToList();
                    foreach (var dateRange in overlappingRanges)
                    {
                        if (range.CacheStartDate <= dateRange.end && range.CacheEndDate >= dateRange.start)
                        {
                            result.AddRange(cachedData.Where(epd =>
                                epd.StartDate >= dateRange.start &&
                                epd.EndDate <= dateRange.end));

                            datesToFetch.Remove(dateRange);

                            // Validate date ranges before adding them
                            if (dateRange.start < range.CacheStartDate)
                            {
                                var newStart = dateRange.start;
                                var newEnd = range.CacheStartDate.AddDays(-1);
                                if (newStart <= newEnd) // Add validation here
                                {
                                    datesToFetch.Add((newStart, newEnd));
                                }
                            }
                            if (dateRange.end > range.CacheEndDate)
                            {
                                var newStart = range.CacheEndDate.AddDays(1);
                                var newEnd = dateRange.end;
                                if (newStart <= newEnd) // Add validation here
                                {
                                    datesToFetch.Add((newStart, newEnd));
                                }
                            }
                        }
                    }
                }
            }

            if (result.Any())
            {
                _logger.LogInformation("Found cached data for period");
            }
            // Fetch missing data for dates
            foreach (var dateRange in datesToFetch)
            {
                // Additional validation before fetching
                if (dateRange.start > dateRange.end)
                {
                    _logger.LogError($"Skipping invalid date range: StartDate ({dateRange.start}) is after EndDate ({dateRange.end})");
                    continue;
                }

                _logger.LogInformation($"Fetching missing data for period: StartDate = {dateRange.start}, EndDate = {dateRange.end}");
                var dbData = await _context.ElectricityPriceDatas
                                           .Where(epd => epd.StartDate >= dateRange.start && epd.EndDate <= dateRange.end)
                                           .ToListAsync();
                result.AddRange(dbData);

                string newCacheKey = $"GetPricesForPeriod_{dateRange.start:yyyyMMdd}_{dateRange.end:yyyyMMdd}";
                var cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30));
                _cache.Set(newCacheKey, dbData, cacheOptions);
                cachedRanges.Add((dateRange.start, dateRange.end, newCacheKey));
            }

            if (datesToFetch.Any())
            {
                _cache.Set("CachedRanges", cachedRanges, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));
            }
            //API thought dates should be out of order for ungodly reasons
            return result.OrderBy(x => x.StartDate);
        }
    }
}
