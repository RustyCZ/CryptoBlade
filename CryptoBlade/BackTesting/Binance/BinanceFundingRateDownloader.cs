﻿using CryptoBlade.Exchanges;
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
    public class BinanceFundingRateDownloader : IHistoricalDataDownloader
    {
        private readonly IHistoricalDataStorage m_historicalDataStorage;
        private readonly ILogger<BinanceFundingRateDownloader> m_logger;

        public BinanceFundingRateDownloader(IHistoricalDataStorage historicalDataStorage,
            ILogger<BinanceFundingRateDownloader> logger)
        {
            m_historicalDataStorage = historicalDataStorage;
            m_logger = logger;
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
                                var downloadedCandles = await DownloadDataSourceAsync(httpClient, dataSource.OneMinute, cancel);
                                if (downloadedCandles.Any())
                                {
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataSourceAsync(httpClient, dataSource.FiveMinutes, cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles =
                                        await DownloadDataSourceAsync(httpClient, dataSource.FifteenMinutes, cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataSourceAsync(httpClient, dataSource.ThirtyMinutes, cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataSourceAsync(httpClient, dataSource.OneHour, cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataSourceAsync(httpClient, dataSource.FourHours, cancel);
                                    candles.AddRange(downloadedCandles);
                                    downloadedCandles = await DownloadDataSourceAsync(httpClient, dataSource.OneDay, cancel);
                                    candles.AddRange(downloadedCandles);
                                }

                                candles = candles.OrderBy(c => c.StartTime).ToList();
                                var dayCandles = SplitToDayCandles(candles);

                                using var l = await asyncLock.LockAsync();
                                foreach (DayCandles dayCandleData in dayCandles)
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
                                    m_logger.LogInformation($"Downloaded {symbol} {dayCandleData.Date:yyyy-MM-dd}");
                                    HistoricalDayData historicalDayData = new HistoricalDayData
                                    {
                                        Candles = dayCandleData.Candles,
                                        Day = dayCandleData.Date,
                                        Trades = Array.Empty<Trade>(),
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

        private DayCandles[] SplitToDayCandles(IReadOnlyList<Candle> candles)
        {
            if(!candles.Any())
                return Array.Empty<DayCandles>();

            var dayCandles = new List<DayCandles>();
            var currentDay = candles[0].StartTime.Date;
            var currentDayCandles = new List<Candle>();
            foreach (var candle in candles)
            {
                if (candle.StartTime.Date != currentDay)
                {
                    dayCandles.Add(new DayCandles(currentDay, currentDayCandles.ToArray()));
                    currentDay = candle.StartTime.Date;
                    currentDayCandles.Clear();
                }

                currentDayCandles.Add(candle);
            }

            dayCandles.Add(new DayCandles(currentDay, currentDayCandles.ToArray()));
            
            return dayCandles.ToArray();
        }

        private async Task<Candle[]> DownloadDataSourceAsync(HttpClient client, TimeFrameSource dataSource, CancellationToken cancel)
        {
            m_logger.LogInformation($"Downloading {dataSource.Source}");
            var response = await client.GetAsync(dataSource.Source, cancel);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                m_logger.LogInformation($"No data for {dataSource.Source}");
                return Array.Empty<Candle>();
            }
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to download {dataSource} status code {response.StatusCode}");
            byte[] data = await response.Content.ReadAsByteArrayAsync(cancel);
            using var ms = new MemoryStream(data);
            ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Read, true);
            List<Candle> candles = new List<Candle>();
            if (zip.Entries.Count > 1)
                throw new InvalidOperationException("Invalid data");
            foreach (var zipArchiveEntry in zip.Entries)
            {
                bool hasCsvHeader = await HasCsvHeaderAsync(zipArchiveEntry, cancel);
                await using var entryStream = zipArchiveEntry.Open();
                using var reader = new StreamReader(entryStream);
                using var csv = new CsvReader(reader, 
                    new CsvConfiguration(CultureInfo.InvariantCulture) 
                    {
                        HasHeaderRecord = hasCsvHeader,
                    });
                var records = csv.GetRecords<BinanceCandle>();
                foreach (var record in records)
                {
                    candles.Add(new Candle
                    {
                        Close = record.Close,
                        High = record.High,
                        Low = record.Low,
                        Open = record.Open,
                        StartTime = record.Timestamp,
                        Volume = record.Volume,
                        TimeFrame = dataSource.TimeFrame,
                    });
                }
            }
            m_logger.LogInformation($"Downloaded {candles.Count} candles from {dataSource.Source}");

            return candles.ToArray();
        }

        private async Task<bool> HasCsvHeaderAsync(ZipArchiveEntry entry, CancellationToken cancel)
        {
            await using var entryStream = entry.Open();
            using var headerReader = new StreamReader(entryStream);
            var firstLine = await headerReader.ReadLineAsync(cancel);
            bool hasHeader = firstLine != null && firstLine.StartsWith("open_time");
            return hasHeader;
        }

        private List<DataSources> ToDataUrls(string symbol, IList<DateTime> missingDays, bool preferDaily)
        {
            HashSet<DataSources> processedDataSources = new HashSet<DataSources>();

            foreach (DateTime missingDay in missingDays)
            {
                DataSources dataSources = new DataSources
                {
                    OneMinute = GetDataUrl(symbol, missingDay, TimeFrame.OneMinute, preferDaily),
                    FiveMinutes = GetDataUrl(symbol, missingDay, TimeFrame.FiveMinutes, preferDaily),
                    FifteenMinutes = GetDataUrl(symbol, missingDay, TimeFrame.FifteenMinutes, preferDaily),
                    ThirtyMinutes = GetDataUrl(symbol, missingDay, TimeFrame.ThirtyMinutes, preferDaily),
                    OneHour = GetDataUrl(symbol, missingDay, TimeFrame.OneHour, preferDaily),
                    FourHours = GetDataUrl(symbol, missingDay, TimeFrame.FourHours, preferDaily),
                    OneDay = GetDataUrl(symbol, missingDay, TimeFrame.OneDay, preferDaily),
                };
                processedDataSources.Add(dataSources);
            }

            return processedDataSources.ToList();
        }

        private TimeFrameSource GetDataUrl(string symbol, DateTime day, TimeFrame timeFrame, bool preferDaily)
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

            // TODO daily not available at all
            if (isCurrentMonth || preferDaily)
                return
                    new TimeFrameSource(timeFrame,
                        $"https://data.binance.vision/data/futures/um/daily/fundingRate/{symbol}/{symbol}-{timeFrameStr}-{day:yyyy}-{day:MM}-{day:dd}.zip");

            return
                new TimeFrameSource(timeFrame,
                    $"https://data.binance.vision/data/futures/um/monthly/fundingRate/{symbol}/{symbol}-fundingRate-{day:yyyy}-{day:MM}.zip");
        }

        private readonly record struct DataSources(
            TimeFrameSource OneMinute,
            TimeFrameSource FiveMinutes,
            TimeFrameSource FifteenMinutes,
            TimeFrameSource ThirtyMinutes,
            TimeFrameSource OneHour,
            TimeFrameSource FourHours,
            TimeFrameSource OneDay);

        private readonly record struct TimeFrameSource(TimeFrame TimeFrame, string Source);

        private record DayCandles(DateTime Date, Candle[] Candles);
    }
}