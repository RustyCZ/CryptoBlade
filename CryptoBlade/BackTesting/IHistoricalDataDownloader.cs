namespace CryptoBlade.BackTesting
{
    public readonly record struct HistoricalDataInclude(bool IncludeTrades, bool IncludeCandles);

    public interface IHistoricalDataDownloader
    {
        Task DownloadRangeAsync(string symbol, HistoricalDataInclude dataInclude, DateTime from, DateTime to, CancellationToken cancel = default);
    }
}