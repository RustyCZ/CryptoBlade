namespace CryptoBlade.BackTesting
{
    public readonly record struct HistoricalDataInclude(bool IncludeTrades, bool IncludeCandles, bool IncludeFundingRates);

    public interface IHistoricalDataDownloader
    {
        Task DownloadRangeAsync(string symbol, HistoricalDataInclude dataInclude, DateTime from, DateTime to, CancellationToken cancel = default);
    }
}