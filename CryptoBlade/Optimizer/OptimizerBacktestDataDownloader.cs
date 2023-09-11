using CryptoBlade.BackTesting;

namespace CryptoBlade.Optimizer
{
    /// <summary>
    /// Special implementation of <see cref="IBackTestDataDownloader"/> that does nothing."/> because we want to download data at the beginning not during optimization so it is not slowed down.
    /// </summary>
    public class OptimizerBacktestDataDownloader : IBackTestDataDownloader
    {
        public Task DownloadDataForBackTestAsync(string[] symbols, DateTime start, DateTime end, CancellationToken cancel = default)
        {
            return Task.CompletedTask;
        }
    }
}