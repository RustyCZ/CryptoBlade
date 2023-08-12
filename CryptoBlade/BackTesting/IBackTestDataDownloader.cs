namespace CryptoBlade.BackTesting
{
    public interface IBackTestDataDownloader
    {
        Task DownloadDataForBackTestAsync(CancellationToken  cancel = default);
    }
}