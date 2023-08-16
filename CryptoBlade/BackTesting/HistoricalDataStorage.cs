using CryptoBlade.BackTesting.Model;
using CryptoBlade.Models;
using Microsoft.Extensions.Options;

namespace CryptoBlade.BackTesting
{
    public class HistoricalDataStorage : IHistoricalDataStorage, IDisposable
    {
        private readonly IOptions<HistoricalTradesStorageOptions> m_options;
        private readonly Dictionary<string, HistoricalSymbolStorage> m_symbolStorages;
        private bool m_disposed;

        public HistoricalDataStorage(IOptions<HistoricalTradesStorageOptions> options)
        {
            m_options = options;
            m_symbolStorages = new Dictionary<string, HistoricalSymbolStorage>();
        }

        public async Task StoreAsync(string symbol, HistoricalDayData dayData, bool flush, CancellationToken cancel = default)
        {
            if (!m_symbolStorages.TryGetValue(symbol, out var storage))
            { 
                storage = new HistoricalSymbolStorage(symbol, m_options.Value.Directory, m_options.Value.MemorySizePerSymbolMB);
                m_symbolStorages.Add(symbol, storage);
            }

            await storage.StoreAsync(dayData, flush);
        }

        public async Task<HistoricalDayData> ReadAsync(string symbol, DateTime day, CancellationToken cancel = default)
        {
            if (!m_symbolStorages.TryGetValue(symbol, out var storage))
            {
                storage = new HistoricalSymbolStorage(symbol, m_options.Value.Directory, m_options.Value.MemorySizePerSymbolMB);
                m_symbolStorages.Add(symbol, storage);
            }
            return await storage.ReadAsync(day);
        }

        public async Task<DateTime[]> FindMissingDaysAsync(string symbol, DateTime start, DateTime end, CancellationToken cancel = default)
        {
            if (!m_symbolStorages.TryGetValue(symbol, out var storage))
            {
                storage = new HistoricalSymbolStorage(symbol, m_options.Value.Directory, m_options.Value.MemorySizePerSymbolMB);
                m_symbolStorages.Add(symbol, storage);
            }
            return await storage.FindMissingDaysAsync(start, end);
        }

        public void Dispose()
        {
            if (m_disposed)
                return;
            foreach (var storage in m_symbolStorages.Values)
                storage.Dispose();
            m_symbolStorages.Clear();
            m_disposed = true;
        }
    }
}