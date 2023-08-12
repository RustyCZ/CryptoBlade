using Microsoft.Extensions.Options;

namespace CryptoBlade.BackTesting
{
    public class BackTestDataDownloader : IBackTestDataDownloader
    {
        private readonly IOptions<BackTestDataDownloaderOptions> m_options;
        private readonly IHistoricalTradesDownloader m_historicalTradesDownloader;

        public BackTestDataDownloader(IOptions<BackTestDataDownloaderOptions> options,
            IHistoricalTradesDownloader historicalTradesDownloader)
        {
            m_historicalTradesDownloader = historicalTradesDownloader;
            m_options = options;
        }

        public async Task DownloadDataForBackTestAsync(CancellationToken cancel = default)
        {
            foreach (string symbol in m_options.Value.Symbols)
            {
                if(cancel.IsCancellationRequested)
                    break;
                await m_historicalTradesDownloader.DownloadRangeAsync(symbol, m_options.Value.Start, m_options.Value.End);
            }
        }
    }
}