namespace CryptoBlade.BackTesting
{
    public class BackTestDataDownloader : IBackTestDataDownloader
    {
        private readonly IHistoricalDataDownloader m_historicalDataDownloader;

        public BackTestDataDownloader(IHistoricalDataDownloader historicalDataDownloader)
        {
            m_historicalDataDownloader = historicalDataDownloader;
        }

        public async Task DownloadDataForBackTestAsync(string[] symbols, DateTime start, DateTime end, CancellationToken cancel = default)
        {
            foreach (string symbol in symbols)
            {
                if(cancel.IsCancellationRequested)
                    break;
                await m_historicalDataDownloader.DownloadRangeAsync(symbol, new HistoricalDataInclude(false, true, true), start, end, cancel);
            }
        }
    }
}