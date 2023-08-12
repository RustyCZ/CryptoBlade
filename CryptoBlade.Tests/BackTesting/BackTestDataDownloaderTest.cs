using CryptoBlade.BackTesting.Bybit;
using CryptoBlade.BackTesting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CryptoBlade.Tests.BackTesting
{
    public class BackTestDataDownloaderTest
    {
        private readonly ILoggerFactory m_loggerFactory;
        
        public BackTestDataDownloaderTest(ITestOutputHelper testOutputHelper)
        {
            m_loggerFactory = LoggerFactory
                .Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                    builder.AddXunit(testOutputHelper);
                    builder.AddSimpleConsole(o =>
                    {
                        o.UseUtcTimestamp = true;
                        o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                    });
                });
        }

        [Fact]
        public async Task AllBackTestDataShouldBeDownloaded()
        {
            var options = Options.Create(new HistoricalTradesStorageOptions
            {
                Directory = "HistoricalData",
            });
            using var storage = new HistoricalTradesStorage(options);
            var downloader = new BybitHistoricalTradesDownloader(storage, m_loggerFactory.CreateLogger<BybitHistoricalTradesDownloader>());
            var backtestDataDownloaderOptions = Options.Create(new BackTestDataDownloaderOptions
            {
                Start = new DateTime(2023, 8, 1),
                End = new DateTime(2023, 8, 11),
                Symbols = new[]
                {
                    "SOLUSDT",
                    "SUIUSDT"
                },
            });
            BackTestDataDownloader backTestDataDownloader = new BackTestDataDownloader(backtestDataDownloaderOptions, downloader);
            await backTestDataDownloader.DownloadDataForBackTestAsync(CancellationToken.None);
        }
    }
}
