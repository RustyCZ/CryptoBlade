using Bybit.Net.Clients.V5;
using CryptoBlade.BackTesting.Model;
using CryptoBlade.Models;

namespace CryptoBlade.BackTesting
{
    public interface IHistoricalDataStorage
    {
        Task<HistoricalDayData> ReadAsync(string symbol, DateTime day, CancellationToken cancel = default);
        Task StoreAsync(string symbol, HistoricalDayData dayData, bool flush, CancellationToken cancel = default);
        Task<DateTime[]> FindMissingDaysAsync(string symbol, DateTime start, DateTime end, CancellationToken cancel = default);
    }
}