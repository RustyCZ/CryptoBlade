using CryptoBlade.BackTesting;
using CryptoBlade.BackTesting.Bybit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace CryptoBlade.Tests.BackTesting.Bybit
{
    public class BybitHistoricalTradesDownloaderTest
    {
        private readonly ILoggerFactory m_loggerFactory;

        public BybitHistoricalTradesDownloaderTest(ITestOutputHelper testOutputHelper)
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
        public async Task WhenSolTradesAreDownloadedTheyShouldBeStored()
        {
            var options = Options.Create(new HistoricalTradesStorageOptions
            {
                Directory = "HistoricalData",
            });
            using var storage = new HistoricalTradesStorage(options);
            var downloader = new BybitHistoricalTradesDownloader(storage, m_loggerFactory.CreateLogger<BybitHistoricalTradesDownloader>());
            var start = new DateTime(2023, 7, 1);
            var end = new DateTime(2023, 8, 11);
            const string symbol = "SOLUSDT";
            await downloader.DownloadRangeAsync(symbol, start, end);
            var missingDays = await storage.FindMissingDaysAsync(symbol, start, end);
            Assert.Empty(missingDays);
            var trades = await storage.ReadAsync(symbol, new DateTime(2023, 7, 1));
            Assert.NotEmpty(trades);
            Assert.All(trades, t => Assert.True(t.TimestampDateTime >= new DateTime(2023, 7, 1) && t.TimestampDateTime < new DateTime(2023, 1, 2)));
        }
    }
}