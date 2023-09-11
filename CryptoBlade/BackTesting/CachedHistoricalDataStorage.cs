using System.Collections.Concurrent;

namespace CryptoBlade.BackTesting
{
    public class CachedHistoricalDataStorage : IHistoricalDataStorage
    {
        private readonly IHistoricalDataStorage m_historicalDataStorage;
        private readonly ConcurrentDictionary<CachedDayDataKey, HistoricalDayData> m_cachedDayData;

        public CachedHistoricalDataStorage(IHistoricalDataStorage historicalDataStorage)
        {
            m_cachedDayData = new ConcurrentDictionary<CachedDayDataKey, HistoricalDayData>();
            m_historicalDataStorage = historicalDataStorage;
        }

        public async Task<HistoricalDayData> ReadAsync(string symbol, DateTime day, CancellationToken cancel = default)
        {
            var key = new CachedDayDataKey(symbol, day);
            if (m_cachedDayData.TryGetValue(key, out var dayData))
                return dayData;
            dayData = await m_historicalDataStorage.ReadAsync(symbol, day, cancel);
            m_cachedDayData.TryAdd(key, dayData);
            return dayData;
        }

        public async Task StoreAsync(string symbol, HistoricalDayData dayData, bool flush, CancellationToken cancel = default)
        {
            await m_historicalDataStorage.StoreAsync(symbol, dayData, flush, cancel);
        }

        public async Task<DateTime[]> FindMissingDaysAsync(string symbol, DateTime start, DateTime end, CancellationToken cancel = default)
        {
            var missingDays = await m_historicalDataStorage.FindMissingDaysAsync(symbol, start, end, cancel);
            return missingDays;
        }

        private readonly record struct CachedDayDataKey(string Symbol, DateTime Day);
    }
}