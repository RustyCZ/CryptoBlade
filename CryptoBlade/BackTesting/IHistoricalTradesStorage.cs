using CryptoBlade.BackTesting.Model;

namespace CryptoBlade.BackTesting
{
    public interface IHistoricalTradesStorage
    {
        Task StoreAsync(string symbol, DateTime day, Trade[] trades, bool flush);
        Task<DateTime[]> FindMissingDaysAsync(string symbol, DateTime start, DateTime end);
    }
}