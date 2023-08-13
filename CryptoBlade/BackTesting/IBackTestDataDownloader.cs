namespace CryptoBlade.BackTesting
{
    public interface IBackTestDataDownloader
    {
        Task DownloadDataForBackTestAsync(string[] symbols, DateTime start, DateTime end, CancellationToken  cancel = default);
    }
}