using Bybit.Net.Clients;
using CryptoBlade.BackTesting.Bybit;
using CryptoBlade.BackTesting;
using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace CryptoBlade.Tests.BackTesting
{
    public class BackTestDataDownloaderTest : TestBase
    {
        public BackTestDataDownloaderTest(ITestOutputHelper testOutputHelper) 
            : base(testOutputHelper)
        {
        }

        [Fact]
        public async Task AllBackTestDataShouldBeDownloaded()
        {
            var options = Options.Create(new ProtoHistoricalDataStorageOptions
            {
                Directory = "HistoricalData",
            });
            var bybit = new BybitRestClient();
            var cbRestClientOptions = Options.Create(new BybitCbFuturesRestClientOptions
            {
                PlaceOrderAttempts = 5
            });
            var cbRestClient = new BybitCbFuturesRestClient(cbRestClientOptions, 
                bybit,
                ApplicationLogging.CreateLogger<BybitCbFuturesRestClient>());
            var storage = new ProtoHistoricalDataStorage(options);
            var downloader = new BybitHistoricalDataDownloader(
                storage, 
                ApplicationLogging.CreateLogger<BybitHistoricalDataDownloader>(), 
                cbRestClient);
            var start = new DateTime(2023, 8, 1);
            var end = new DateTime(2023, 8, 11);
            var symbols = new[]
            {
                "SOLUSDT",
                "SUIUSDT"
            };
            BackTestDataDownloader backTestDataDownloader = new BackTestDataDownloader(downloader);
            await backTestDataDownloader.DownloadDataForBackTestAsync(symbols, start, end, CancellationToken.None);
        }
    }
}
