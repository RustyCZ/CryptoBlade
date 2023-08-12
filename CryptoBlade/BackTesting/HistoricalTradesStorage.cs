using CryptoBlade.BackTesting.Model;
using Microsoft.Extensions.Options;

namespace CryptoBlade.BackTesting
{
    public class HistoricalTradesStorage : IHistoricalTradesStorage, IDisposable
    {
        private readonly IOptions<HistoricalTradesStorageOptions> m_options;
        private readonly Dictionary<string, HistoricalSymbolStorage> m_symbolStorages;
        private bool m_disposed;

        public HistoricalTradesStorage(IOptions<HistoricalTradesStorageOptions> options)
        {
            m_options = options;
            m_symbolStorages = new Dictionary<string, HistoricalSymbolStorage>();
        }

        public async Task StoreAsync(string symbol, DateTime day, Trade[] trades, bool flush)
        {
            if (!m_symbolStorages.TryGetValue(symbol, out var storage))
            { 
                storage = new HistoricalSymbolStorage(symbol, m_options.Value.Directory);
                m_symbolStorages.Add(symbol, storage);
            }

            await storage.StoreAsync(day, trades, flush);
        }

        public async Task<Trade[]> ReadAsync(string symbol, DateTime day)
        {
            if (!m_symbolStorages.TryGetValue(symbol, out var storage))
            {
                storage = new HistoricalSymbolStorage(symbol, m_options.Value.Directory);
                m_symbolStorages.Add(symbol, storage);
            }
            return await storage.ReadAsync(day);
        }

        public async Task<DateTime[]> FindMissingDaysAsync(string symbol, DateTime start, DateTime end)
        {
            if (!m_symbolStorages.TryGetValue(symbol, out var storage))
            {
                storage = new HistoricalSymbolStorage(symbol, m_options.Value.Directory);
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