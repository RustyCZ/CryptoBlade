using Bybit.Net.Clients;
using CryptoBlade.BackTesting;
using CryptoBlade.BackTesting.Bybit;
using CryptoBlade.Exchanges;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace CryptoBlade.Tests.BackTesting.Bybit
{
    public class BybitHistoricalTradesDownloaderTest : TestBase
    {
        private readonly ILoggerFactory m_loggerFactory;

        public BybitHistoricalTradesDownloaderTest(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
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
            var options = Options.Create(new ProtoHistoricalDataStorageOptions
            {
                Directory = "HistoricalData",
            });
            var storage = new ProtoHistoricalDataStorage(options);
            var bybit = new BybitRestClient();
            var cbRestClientOptions = Options.Create(new BybitCbFuturesRestClientOptions
            {
                PlaceOrderAttempts = 5
            });
            var cbRestClient = new BybitCbFuturesRestClient(cbRestClientOptions,
                bybit,
                m_loggerFactory.CreateLogger<BybitCbFuturesRestClient>());
            var downloader = new BybitHistoricalDataDownloader(storage, 
                m_loggerFactory.CreateLogger<BybitHistoricalDataDownloader>(),
                cbRestClient);
            var start = new DateTime(2023, 8, 1);
            var end = new DateTime(2023, 8, 12);
            const string symbol = "SOLUSDT";
            await downloader.DownloadRangeAsync(symbol, new HistoricalDataInclude(false, true), start, end);
            var missingDays = await storage.FindMissingDaysAsync(symbol, start, end);
            Assert.Empty(missingDays);
            var dayData = await storage.ReadAsync(symbol, start);
            Assert.NotEmpty(dayData.Candles);
            Assert.All(dayData.Candles, t => Assert.True(t.StartTime >= start && t.StartTime < start.AddDays(1)));
        }
    }
}