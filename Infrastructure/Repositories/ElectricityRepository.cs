using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;


namespace Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing electricity price data with an in-memory caching layer.
    /// </summary>
    public class ElectricityRepository : IElectricityRepository
    {
        private readonly ElectricityDbContext _context;
        private readonly ILogger<ElectricityRepository> _logger;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of <see cref="ElectricityRepository"/>.
        /// </summary>
        /// <param name="context">The database context for electricity price data.</param>
        /// <param name="logger">Logger instance for diagnostic output.</param>
        /// <param name="cache">In-memory cache used to reduce redundant database queries.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is <c>null</c>.</exception>
        public ElectricityRepository(ElectricityDbContext context, ILogger<ElectricityRepository> logger, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Returns the earliest <see cref="ElectricityPriceData.StartDate"/> stored in the database.
        /// </summary>
        /// <returns>
        /// The oldest start date, or <see cref="DateTime.MaxValue"/> if the table is empty.
        /// </returns>
        public async Task<DateTime> GetOldestStartDateAsync()
        {
            return await _context.ElectricityPriceDatas
                .MinAsync(e => (DateTime?)e.StartDate) ?? DateTime.MaxValue;
        }

        /// <summary>
        /// Persists a collection of electricity price records to the database.
        /// </summary>
        /// <param name="electricityPriceDataDtoIn">The records to insert.</param>
        /// <returns>
        /// <c>true</c> if the records were saved successfully; <c>false</c> if the input was
        /// empty/null or a <see cref="DbUpdateException"/> occurred.
        /// </returns>
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

        /// <summary>
        /// Checks whether a record with the given start and end date already exists.
        /// </summary>
        /// <param name="startDate">The start date to check.</param>
        /// <param name="endDate">The end date to check.</param>
        /// <returns><c>true</c> if a matching record exists; otherwise <c>false</c>.</returns>
        public async Task<bool> IsDuplicateAsync(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("Checking for duplicates between {StartDate} and {EndDate}.", startDate, endDate);
            bool isDuplicate = await _context.ElectricityPriceDatas
                .AnyAsync(epd => epd.StartDate == startDate && epd.EndDate == endDate);
            _logger.LogInformation("Duplicate check result: {IsDuplicate}", isDuplicate);
            return isDuplicate;
        }

        /// <summary>
        /// Returns all records whose <c>(StartDate, EndDate)</c> pair matches any of the
        /// corresponding pairs formed by zipping <paramref name="startDates"/> and
        /// <paramref name="endDates"/> together.
        /// </summary>
        /// <param name="startDates">Ordered list of start dates. Must be the same length as <paramref name="endDates"/>.</param>
        /// <param name="endDates">Ordered list of end dates. Must be the same length as <paramref name="startDates"/>.</param>
        /// <returns>
        /// A list of matching records, or an empty list if the inputs are null or mismatched in length.
        /// </returns>
        public async Task<List<ElectricityPriceData>> GetDuplicatesAsync(List<DateTime> startDates, List<DateTime> endDates)
        {
            if (startDates == null || endDates == null || startDates.Count != endDates.Count)
            {
                _logger.LogWarning("Invalid startDates or endDates provided to GetDuplicatesAsync.");
                return new List<ElectricityPriceData>();
            }

            _logger.LogInformation("Fetching duplicate electricity price data based on provided date ranges.");

            var pairs = startDates.Zip(endDates).ToHashSet();

            var candidates = await _context.ElectricityPriceDatas
                .Where(e => startDates.Contains(e.StartDate) && endDates.Contains(e.EndDate))
                .ToListAsync();

            return candidates
                .Where(e => pairs.Contains((e.StartDate, e.EndDate)))
                .ToList();
        }

        /// <inheritdoc cref="AddRangeElectricityPricesAsync"/>
        public async Task<bool> AddBatchElectricityPricesAsync(IEnumerable<ElectricityPriceData> electricityPriceData)
        {
            return await AddRangeElectricityPricesAsync(electricityPriceData);
        }

        /// <summary>
        /// Retrieves electricity price records that fall within the specified period.
        /// Results are served from an in-memory cache when possible; only uncovered
        /// sub-ranges are fetched from the database.
        /// </summary>
        /// <remarks>
        /// When <paramref name="startDate"/> equals <paramref name="endDate"/>, the range
        /// is automatically expanded to cover the full calendar day (00:00:00 to 23:59:59).
        /// Results are deduplicated by <see cref="ElectricityPriceData.Id"/> and returned
        /// in ascending <see cref="ElectricityPriceData.StartDate"/> order.
        /// </remarks>
        /// <param name="startDate">Inclusive start of the period.</param>
        /// <param name="endDate">Inclusive end of the period.</param>
        /// <returns>
        /// An ordered, deduplicated sequence of price records, or an empty sequence if
        /// the date range is invalid.
        /// </returns>
        public async Task<IEnumerable<ElectricityPriceData>> GetPricesForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            {
                _logger.LogError("Invalid date range: StartDate ({StartDate}) is after EndDate ({EndDate})", startDate, endDate);
                return Enumerable.Empty<ElectricityPriceData>();
            }

            if (startDate == endDate)
            {
                endDate = startDate.Date.AddDays(1).AddTicks(-1);
            }

            var exactCacheKey = $"GetPricesForPeriod_{startDate:yyyyMMddHHmmss}_{endDate:yyyyMMddHHmmss}";
            if (_cache.TryGetValue(exactCacheKey, out IEnumerable<ElectricityPriceData>? exactCached) && exactCached != null)
            {
                return exactCached;
            }

            var cachedRanges = _cache.Get<List<(DateTime CacheStartDate, DateTime CacheEndDate, string CacheKey)>>("CachedRanges")
                                ?? new List<(DateTime, DateTime, string)>();

            // Prune entries whose data cache has expired
            cachedRanges.RemoveAll(r => !_cache.TryGetValue(r.CacheKey, out _));

            var result = new List<ElectricityPriceData>();
            var uncoveredRanges = new List<(DateTime start, DateTime end)> { (startDate, endDate) };

            foreach (var range in cachedRanges)
            {
                if (!_cache.TryGetValue(range.CacheKey, out IEnumerable<ElectricityPriceData>? cachedData) || cachedData == null)
                    continue;

                var nextUncovered = new List<(DateTime start, DateTime end)>();

                foreach (var req in uncoveredRanges)
                {
                    if (range.CacheEndDate < req.start || range.CacheStartDate > req.end)
                    {
                        nextUncovered.Add(req);
                        continue;
                    }

                    result.AddRange(cachedData.Where(epd =>
                        epd.StartDate >= req.start && epd.EndDate <= req.end));

                    if (req.start < range.CacheStartDate)
                        nextUncovered.Add((req.start, range.CacheStartDate.AddTicks(-1)));

                    if (req.end > range.CacheEndDate)
                        nextUncovered.Add((range.CacheEndDate.AddTicks(1), req.end));
                }

                uncoveredRanges = nextUncovered;
            }

            foreach (var gap in uncoveredRanges)
            {
                _logger.LogInformation("Fetching data for uncovered period: {Start} to {End}", gap.start, gap.end);

                var dbData = await _context.ElectricityPriceDatas
                    .Where(epd => epd.StartDate >= gap.start && epd.EndDate <= gap.end)
                    .ToListAsync();

                result.AddRange(dbData);

                var gapCacheKey = $"GetPricesForPeriod_{gap.start:yyyyMMddHHmmss}_{gap.end:yyyyMMddHHmmss}";
                _cache.Set(gapCacheKey, dbData, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));
                cachedRanges.Add((gap.start, gap.end, gapCacheKey));
            }

            if (uncoveredRanges.Count > 0)
            {
                _cache.Set("CachedRanges", cachedRanges, new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));
            }

            var finalResult = result
                .DistinctBy(epd => epd.Id)
                .OrderBy(epd => epd.StartDate)
                .ToList();

            _cache.Set(exactCacheKey, (IEnumerable<ElectricityPriceData>)finalResult,
                new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

            return finalResult;
        }
    }
}