namespace CryptoBlade.BackTesting
{
    public interface IHistoricalTradesDownloader
    {
        Task DownloadRangeAsync(string symbol, DateTime from, DateTime to);
    }
}