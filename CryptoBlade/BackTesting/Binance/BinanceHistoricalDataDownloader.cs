using CryptoBlade.Exchanges;
using CryptoBlade.Models;
using Nito.AsyncEx;
using CryptoBlade.BackTesting.Model;

namespace CryptoBlade.BackTesting.Binance
{
    public class BinanceHistoricalDataDownloader : IHistoricalDataDownloader
    {
        private readonly IHistoricalDataStorage m_historicalDataStorage;
        private readonly ILogger<BinanceHistoricalDataDownloader> m_logger;
        private readonly ICbFuturesRestClient m_cbFuturesRestClient;

        public BinanceHistoricalDataDownloader(IHistoricalDataStorage historicalDataStorage,
            ILogger<BinanceHistoricalDataDownloader> logger,
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

                    // run 8 days in parallel use semaphore
                    var tasks = new List<Task>();
                    using var semaphore = new SemaphoreSlim(8);
                    var asyncLock = new AsyncLock();
                    foreach (var day in missingDaysSet)
                    {
                        await semaphore.WaitAsync(cancel);
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                m_logger.LogInformation($"Downloading {symbol} {day:yyyy-MM-dd}");
                                Trade[] trades = Array.Empty<Trade>();
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
                                        var klines1 = await m_cbFuturesRestClient.GetKlinesAsync(symbol, timeFrame, day,
                                            firstHalfDay.AddMinutes(-1), cancel);
                                        candles.AddRange(klines1);
                                        var klines2 = await m_cbFuturesRestClient.GetKlinesAsync(symbol, timeFrame,
                                            firstHalfDay, day.AddDays(1).AddMinutes(-1), cancel);
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
                                await Task.Delay(TimeSpan.FromSeconds(10), cancel);
                            }
                            catch
                            {
                                await Task.Delay(TimeSpan.FromMinutes(1), cancel);
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
                    await Task.Delay(TimeSpan.FromMinutes(1), cancel);
                    m_logger.LogError(e, $"Failed to download {symbol} from {from} to {to}");
                }
            }
        }
    }
}
