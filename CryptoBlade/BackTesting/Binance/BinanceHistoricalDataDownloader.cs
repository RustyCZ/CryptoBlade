using CryptoBlade.Exchanges;
using CryptoBlade.Models;
using Nito.AsyncEx;
using CryptoBlade.BackTesting.Model;
using System.Net;
using System.IO.Compression;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration;

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

        public async Task DownloadRangeAsync(string symbol, HistoricalDataInclude dataInclude, DateTime from,
            DateTime to, CancellationToken cancel = default)
        {
            bool hasInconsistentData = true;
            int remainingTries = 2;
            bool preferDaily = false;
            while (hasInconsistentData && remainingTries > 0)
            {
                
                hasInconsistentData = await DownloadRangeInternalAsync(symbol, from, to, preferDaily, cancel);
                m_logger.LogInformation($"Downloaded {symbol} from {from} to {to} has inconsistent data {hasInconsistentData}");
                preferDaily = hasInconsistentData;
                remainingTries--;
            }
        }

        private async Task<bool> DownloadRangeInternalAsync(string symbol, 
            DateTime from,
            DateTime to, 
            bool preferDaily,
            CancellationToken cancel = default)
        {
            bool hasInconsistentData = false;
            DateTime startTime = DateTime.MaxValue;
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
                    var tasks = new List<Task>();
                    using var semaphore = new SemaphoreSlim(12);
                    var asyncLock = new AsyncLock();
                    var dataSources = ToDataUrls(symbol, missingDays, preferDaily);

                    HttpClient httpClient = new HttpClient();
                    foreach (var dataSource in dataSources)
                    {
                        await semaphore.WaitAsync(cancel);
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                List<Candle> candles = new List<Candle>();
                                FundingRate[] fundingRates = Array.Empty<FundingRate>();
                                var downloadedCandles = await DownloadDataAsync<Candle, BinanceCandle>(
                                    httpClient, 
                                    dataSource.OneMinute.Source, 
                                    candle => ToCandle(dataSource.OneMinute.TimeFrame, candle), 
                                    HasKlinesHeaderAsync,
                                    cancel);
                                if (downloadedCandles.Any())
                                {
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataAsync<Candle, BinanceCandle>(
                                        httpClient,
                                        dataSource.FiveMinutes.Source,
                                        candle => ToCandle(dataSource.FiveMinutes.TimeFrame, candle),
                                        HasKlinesHeaderAsync,
                                        cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataAsync<Candle, BinanceCandle>(
                                        httpClient,
                                        dataSource.FifteenMinutes.Source,
                                        candle => ToCandle(dataSource.FifteenMinutes.TimeFrame, candle),
                                        HasKlinesHeaderAsync,
                                        cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataAsync<Candle, BinanceCandle>(
                                        httpClient,
                                        dataSource.ThirtyMinutes.Source,
                                        candle => ToCandle(dataSource.ThirtyMinutes.TimeFrame, candle),
                                        HasKlinesHeaderAsync,
                                        cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataAsync<Candle, BinanceCandle>(
                                        httpClient,
                                        dataSource.OneHour.Source,
                                        candle => ToCandle(dataSource.OneHour.TimeFrame, candle),
                                        HasKlinesHeaderAsync,
                                        cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataAsync<Candle, BinanceCandle>(
                                        httpClient,
                                        dataSource.FourHours.Source,
                                        candle => ToCandle(dataSource.FourHours.TimeFrame, candle),
                                        HasKlinesHeaderAsync,
                                        cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataAsync<Candle, BinanceCandle>(
                                        httpClient,
                                        dataSource.OneDay.Source,
                                        candle => ToCandle(dataSource.OneDay.TimeFrame, candle),
                                        HasKlinesHeaderAsync,
                                        cancel);
                                    candles.AddRange(downloadedCandles);

                                    if (dataSource.FundingRateUrl != null)
                                    {
                                        fundingRates = await DownloadDataAsync<FundingRate, BinanceFundingRate>(
                                            httpClient,
                                            dataSource.FundingRateUrl,
                                            ToFundingRate,
                                            HasFundingRateHeaderAsync,
                                            cancel);
                                        fundingRates = fundingRates
                                            .DistinctBy(x => x.Time)
                                            .OrderBy(x => x.Time).ToArray();
                                    }
                                    else
                                    {
                                        DateTime min = candles.Min(x => x.StartTime);
                                        DateTime max = candles.Max(x => x.StartTime);
                                        fundingRates = (await m_cbFuturesRestClient.GetFundingRatesAsync(symbol, min, max, cancel))
                                            .OrderBy(x => x.Time).ToArray();
                                    }
                                }

                                candles = candles.OrderBy(c => c.StartTime).ToList();
                                var dayCandles = SplitToDailyData(candles, fundingRates);

                                using var l = await asyncLock.LockAsync();
                                foreach (DailyData dayCandleData in dayCandles)
                                {
                                    if (startTime > dayCandleData.Date)
                                        startTime = dayCandleData.Date;
                                    const int candlesInDay = 1903;
                                    if (dayCandleData.Candles.Length != candlesInDay)
                                    {
                                        hasInconsistentData = true;
                                        m_logger.LogWarning($"Inconsistent data for {symbol} {dayCandleData.Date:yyyy-MM-dd} {dayCandles.Length} candles");
                                        continue;
                                    }

                                    if (!dayCandleData.FundingRates.Any())
                                    {
                                        m_logger.LogWarning($"No funding rates for {symbol} {dayCandleData.Date:yyyy-MM-dd}");
                                    }

                                    m_logger.LogInformation($"Downloaded {symbol} {dayCandleData.Date:yyyy-MM-dd}");
                                    HistoricalDayData historicalDayData = new HistoricalDayData
                                    {
                                        Candles = dayCandleData.Candles,
                                        Day = dayCandleData.Date,
                                        Trades = Array.Empty<Trade>(),
                                        FundingRates = dayCandleData.FundingRates,
                                    };
                                    await m_historicalDataStorage.StoreAsync(symbol, historicalDayData, true, cancel);
                                    missingDaysSet.Remove(dayCandleData.Date);
                                }
                            }
                            catch
                            {
                                await Task.Delay(TimeSpan.FromMinutes(1), cancel);
                                throw;
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, cancel));
                    }

                    await Task.WhenAll(tasks);
                    using var l = await asyncLock.LockAsync();
                    foreach (DateTime missingDay in missingDaysSet)
                    {
                        if (missingDay > startTime)
                        {
                            hasInconsistentData = true;
                            m_logger.LogWarning($"Inconsistent data for {symbol} {missingDay:yyyy-MM-dd} missing day");
                            continue;
                        }
                        HistoricalDayData historicalDayData = new HistoricalDayData
                        {
                            Candles = Array.Empty<Candle>(),
                            Day = missingDay,
                            Trades = Array.Empty<Trade>(),
                            FundingRates = Array.Empty<FundingRate>(),
                        };
                        await m_historicalDataStorage.StoreAsync(symbol, historicalDayData, true, cancel);
                    }
                    break;
                }
                catch (Exception e)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancel);
                    m_logger.LogError(e, $"Failed to download {symbol} from {from} to {to}");
                }
            }

            return hasInconsistentData;
        }

        private DailyData[] SplitToDailyData(IReadOnlyList<Candle> candles, IReadOnlyList<FundingRate> fundingRates)
        {
            if(!candles.Any())
                return Array.Empty<DailyData>();

            var dayCandles = new List<DailyData>();
            var currentDay = candles[0].StartTime.Date;
            var currentDayCandles = new List<Candle>();
            var fundingRatesMap = ToFundingRatesMap(fundingRates);
            foreach (var candle in candles)
            {
                if (candle.StartTime.Date != currentDay)
                {
                    if (!fundingRatesMap.TryGetValue(currentDay, out FundingRate[]? dailyFundingRates))
                        dailyFundingRates = Array.Empty<FundingRate>();
                    dayCandles.Add(new DailyData(currentDay, currentDayCandles.ToArray(), dailyFundingRates));
                    currentDay = candle.StartTime.Date;
                    currentDayCandles.Clear();
                }

                currentDayCandles.Add(candle);
            }

            if (!fundingRatesMap.TryGetValue(currentDay, out FundingRate[]? currentDayFundingRates))
                currentDayFundingRates = Array.Empty<FundingRate>();
            dayCandles.Add(new DailyData(currentDay, currentDayCandles.ToArray(), currentDayFundingRates));
            
            return dayCandles.ToArray();
        }

        private Dictionary<DateTime, FundingRate[]> ToFundingRatesMap(IReadOnlyList<FundingRate> fundingRates)
        {
            var fundingRatesMap = new Dictionary<DateTime, FundingRate[]>();
            var currentDay = fundingRates[0].Time.Date;
            var currentDayFundingRates = new List<FundingRate>();
            foreach (var fundingRate in fundingRates)
            {
                if (fundingRate.Time.Date != currentDay)
                {
                    fundingRatesMap[currentDay] = currentDayFundingRates.OrderBy(x => x.Time).ToArray();
                    currentDay = fundingRate.Time.Date;
                    currentDayFundingRates.Clear();
                }

                currentDayFundingRates.Add(fundingRate);
            }

            fundingRatesMap.Add(currentDay, currentDayFundingRates.OrderBy(x => x.Time).ToArray());
            
            return fundingRatesMap;
        }

        private static Candle ToCandle(TimeFrame timeFrame, BinanceCandle record)
        {
            return new Candle
            {
                Close = record.Close,
                High = record.High,
                Low = record.Low,
                Open = record.Open,
                StartTime = record.Timestamp,
                Volume = record.Volume,
                TimeFrame = timeFrame,
            };
        }

        private static FundingRate ToFundingRate(BinanceFundingRate record)
        {
            return new FundingRate
            {
                Rate = (decimal)record.LastFundingRate,
                Time = record.CalcTimestamp,
            };
        }

        private async Task<TResult[]> DownloadDataAsync<TResult, TSource>(HttpClient client, 
            string dataSource, 
            Func<TSource, TResult> converter,
            Func<ZipArchiveEntry, CancellationToken, Task<bool>> hasHeader,
            CancellationToken cancel) 
            where TSource : class 
            where TResult : class
        {
            m_logger.LogInformation($"Downloading {dataSource}");
            var response = await client.GetAsync(dataSource, cancel);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                m_logger.LogInformation($"No data for {dataSource}");
                return Array.Empty<TResult>();
            }
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to download {dataSource} status code {response.StatusCode}");
            byte[] data = await response.Content.ReadAsByteArrayAsync(cancel);
            using var ms = new MemoryStream(data);
            ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Read, true);
            List<TResult> resultData = new List<TResult>();
            if (zip.Entries.Count > 1)
                throw new InvalidOperationException("Invalid data");
            foreach (var zipArchiveEntry in zip.Entries)
            {
                bool hasCsvHeader = await hasHeader(zipArchiveEntry, cancel);
                await using var entryStream = zipArchiveEntry.Open();
                using var reader = new StreamReader(entryStream);
                using var csv = new CsvReader(reader,
                    new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = hasCsvHeader,
                    });
                var records = csv.GetRecords<TSource>();
                foreach (var record in records)
                    resultData.Add(converter(record));
            }
            m_logger.LogInformation($"Downloaded {resultData.Count} entries from {dataSource}");

            return resultData.ToArray();
        }

        private async Task<bool> HasKlinesHeaderAsync(ZipArchiveEntry entry, CancellationToken cancel)
        {
            await using var entryStream = entry.Open();
            using var headerReader = new StreamReader(entryStream);
            var firstLine = await headerReader.ReadLineAsync(cancel);
            bool hasHeader = firstLine != null && firstLine.StartsWith("open_time");
            return hasHeader;
        }

        private async Task<bool> HasFundingRateHeaderAsync(ZipArchiveEntry entry, CancellationToken cancel)
        {
            await using var entryStream = entry.Open();
            using var headerReader = new StreamReader(entryStream);
            var firstLine = await headerReader.ReadLineAsync(cancel);
            bool hasHeader = firstLine != null && firstLine.StartsWith("calc_time");
            return hasHeader;
        }

        private List<DataSources> ToDataUrls(string symbol, IList<DateTime> missingDays, bool preferDaily)
        {
            HashSet<DataSources> processedDataSources = new HashSet<DataSources>();

            foreach (DateTime missingDay in missingDays)
            {
                DataSources dataSources = new DataSources
                {
                    OneMinute = GetKlinesDataUrl(symbol, missingDay, TimeFrame.OneMinute, preferDaily),
                    FiveMinutes = GetKlinesDataUrl(symbol, missingDay, TimeFrame.FiveMinutes, preferDaily),
                    FifteenMinutes = GetKlinesDataUrl(symbol, missingDay, TimeFrame.FifteenMinutes, preferDaily),
                    ThirtyMinutes = GetKlinesDataUrl(symbol, missingDay, TimeFrame.ThirtyMinutes, preferDaily),
                    OneHour = GetKlinesDataUrl(symbol, missingDay, TimeFrame.OneHour, preferDaily),
                    FourHours = GetKlinesDataUrl(symbol, missingDay, TimeFrame.FourHours, preferDaily),
                    OneDay = GetKlinesDataUrl(symbol, missingDay, TimeFrame.OneDay, preferDaily),
                    FundingRateUrl = GetFundingRateUrlAsync(symbol, missingDay),
                };
                processedDataSources.Add(dataSources);
            }

            return processedDataSources.ToList();
        }

        private TimeFrameSource GetKlinesDataUrl(string symbol, DateTime day, TimeFrame timeFrame, bool preferDaily)
        {
            var utcNow = DateTime.UtcNow;
            bool isCurrentMonth = day.Year == utcNow.Year && day.Month == utcNow.Month;
            string timeFrameStr = timeFrame switch
            {
                TimeFrame.OneMinute => "1m",
                TimeFrame.FiveMinutes => "5m",
                TimeFrame.FifteenMinutes => "15m",
                TimeFrame.ThirtyMinutes => "30m",
                TimeFrame.OneHour => "1h",
                TimeFrame.FourHours => "4h",
                TimeFrame.OneDay => "1d",
                _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null)
            };

            if (isCurrentMonth || preferDaily)
                return
                    new TimeFrameSource(timeFrame,
                        $"https://data.binance.vision/data/futures/um/daily/klines/{symbol}/{timeFrameStr}/{symbol}-{timeFrameStr}-{day:yyyy}-{day:MM}-{day:dd}.zip");

            return
                new TimeFrameSource(timeFrame,
                    $"https://data.binance.vision/data/futures/um/monthly/klines/{symbol}/{timeFrameStr}/{symbol}-{timeFrameStr}-{day:yyyy}-{day:MM}.zip");
        }

        private string? GetFundingRateUrlAsync(string symbol, DateTime day)
        {
            var utcNow = DateTime.UtcNow;
            bool isCurrentMonth = day.Year == utcNow.Year && day.Month == utcNow.Month;
            if (isCurrentMonth)
                return null;

            return $"https://data.binance.vision/data/futures/um/monthly/fundingRate/{symbol}/{symbol}-fundingRate-{day:yyyy}-{day:MM}.zip";
        }

        private readonly record struct DataSources(
            TimeFrameSource OneMinute,
            TimeFrameSource FiveMinutes,
            TimeFrameSource FifteenMinutes,
            TimeFrameSource ThirtyMinutes,
            TimeFrameSource OneHour,
            TimeFrameSource FourHours,
            TimeFrameSource OneDay,
            string? FundingRateUrl);

        private readonly record struct TimeFrameSource(TimeFrame TimeFrame, string Source);

        private record DailyData(DateTime Date, Candle[] Candles, FundingRate[] FundingRates);
    }
}