using System.Globalization;
using System.IO.Compression;
using System.Net;
using CryptoBlade.BackTesting.Model;
using CryptoBlade.Exchanges;
using CryptoBlade.Models;
using CsvHelper;
using Nito.AsyncEx;

namespace CryptoBlade.BackTesting.Bybit
{
    public class BybitHistoricalDataDownloader : IHistoricalDataDownloader
    {
        private readonly IHistoricalDataStorage m_historicalDataStorage;
        private readonly ILogger<BybitHistoricalDataDownloader> m_logger;
        private readonly ICbFuturesRestClient m_cbFuturesRestClient;

        public BybitHistoricalDataDownloader(IHistoricalDataStorage historicalDataStorage, 
            ILogger<BybitHistoricalDataDownloader> logger,
            ICbFuturesRestClient cbFuturesRestClient)
        {
            m_historicalDataStorage = historicalDataStorage;
            m_logger = logger;
            m_cbFuturesRestClient = cbFuturesRestClient;
        }

        public async Task DownloadRangeAsync(string symbol, HistoricalDataInclude dataInclude, DateTime from, DateTime to, CancellationToken cancel = default)
        {
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    var maxTo = DateTime.UtcNow.Date.AddDays(-1);
                    if (to > maxTo)
                    {
                        m_logger.LogWarning($"To date {to} is greater than max allowed {maxTo}, setting to {maxTo}");
                        to = maxTo;
                    }

                    var missingDays = await m_historicalDataStorage.FindMissingDaysAsync(symbol, from, to, cancel);
                    var missingDaysSet = new HashSet<DateTime>(missingDays);

                    // run 4 days in parallel use semaphore
                    var tasks = new List<Task>();
                    using var semaphore = new SemaphoreSlim(4);
                    var asyncLock = new AsyncLock();
                    foreach (var day in missingDaysSet)
                    {
                        await semaphore.WaitAsync(cancel);
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                m_logger.LogInformation($"Downloading {symbol} {day:yyyy-MM-dd}");
                                Trade[] trades;
                                if (dataInclude.IncludeTrades)
                                    trades = await DownloadDayAsync(symbol, day);
                                else
                                    trades = Array.Empty<Trade>();
                                var tfs = new[]
                                {
                                    TimeFrame.OneMinute,
                                    TimeFrame.FiveMinutes,
                                    TimeFrame.FifteenMinutes,
                                    TimeFrame.ThirtyMinutes,
                                    TimeFrame.OneHour,
                                    TimeFrame.FourHours,
                                    TimeFrame.OneDay,
                                };
                                var candles = new List<Candle>();
                                if (dataInclude.IncludeCandles)
                                {
                                    foreach (TimeFrame timeFrame in tfs)
                                    {
                                        var firstHalfDay = day.Date.AddHours(12);
                                        var klines1 = await m_cbFuturesRestClient.GetKlinesAsync(symbol, timeFrame, day, firstHalfDay.AddMinutes(-1), cancel);
                                        candles.AddRange(klines1);
                                        var klines2 = await m_cbFuturesRestClient.GetKlinesAsync(symbol, timeFrame, firstHalfDay, day.AddDays(1).AddMinutes(-1), cancel);
                                        candles.AddRange(klines2);
                                    }
                                }
                                
                                using var l = await asyncLock.LockAsync();
                                missingDaysSet.Remove(day);
                                bool flush = missingDaysSet.Count == 0;
                                var candlesArr = candles.OrderBy(x => x.StartTime).ToArray();
                                HistoricalDayData historicalDayData = new HistoricalDayData
                                {
                                    Candles = candlesArr,
                                    Day = day,
                                    Trades = trades,
                                };
                                await m_historicalDataStorage.StoreAsync(symbol, historicalDayData, flush, cancel);
                                m_logger.LogInformation($"Downloaded {symbol} {day:yyyy-MM-dd}");
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, cancel));
                    }
                    await Task.WhenAll(tasks);
                    break;
                }
                catch (Exception e)
                {
                    m_logger.LogError(e, $"Failed to download {symbol} from {from} to {to}");
                }
            }
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