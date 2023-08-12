using System.Globalization;
using System.IO.Compression;
using System.Net;
using CryptoBlade.BackTesting.Model;
using CsvHelper;
using Nito.AsyncEx;

namespace CryptoBlade.BackTesting.Bybit
{
    public class BybitHistoricalTradesDownloader : IHistoricalTradesDownloader
    {
        private readonly IHistoricalTradesStorage m_historicalTradesStorage;
        private readonly ILogger<BybitHistoricalTradesDownloader> m_logger;

        public BybitHistoricalTradesDownloader(IHistoricalTradesStorage historicalTradesStorage, 
            ILogger<BybitHistoricalTradesDownloader> logger)
        {
            m_historicalTradesStorage = historicalTradesStorage;
            m_logger = logger;
        }

        public async Task DownloadRangeAsync(string symbol, DateTime from, DateTime to)
        {
            var maxTo = DateTime.UtcNow.Date.AddDays(-1);
            if (to > maxTo)
            {
                m_logger.LogWarning($"To date {to} is greater than max allowed {maxTo}, setting to {maxTo}");
                to = maxTo;
            }

            var missingDays = await m_historicalTradesStorage.FindMissingDaysAsync(symbol, from, to);
            var missingDaysSet = new HashSet<DateTime>(missingDays);

            // run 4 days in parallel use semaphore
            var tasks = new List<Task>();
            using var semaphore = new SemaphoreSlim(4);
            var asyncLock = new AsyncLock();
            foreach (var day in missingDaysSet)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        m_logger.LogInformation($"Downloading {symbol} {day:yyyy-MM-dd}");
                        var trades = await DownloadDayAsync(symbol, day);
                        using var l = await asyncLock.LockAsync();
                        missingDaysSet.Remove(day);
                        bool flush = missingDaysSet.Count == 0;
                        await m_historicalTradesStorage.StoreAsync(symbol, day, trades, flush);
                        m_logger.LogInformation($"Downloaded {symbol} {day:yyyy-MM-dd}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
            await Task.WhenAll(tasks);
        }

        private async Task<Trade[]> DownloadDayAsync(string symbol, DateTime current)
        {
            string url = $"https://public.bybit.com/trading/{symbol}/{symbol}{current:yyyy-MM-dd}.csv.gz";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "CryptoBlade");
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                var records = csv.GetRecords<BybitHistoricalTick>();
                var result = records
                    .Select(x => new Trade
                    {
                        TimestampDateTime = x.TimestampDateTime,
                        Size = x.Size,
                        Price = x.Price
                    }).ToArray();
                return result;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<Trade>();
            }

            throw new Exception($"Failed to download {url} {response.StatusCode}");
        }
    }
}