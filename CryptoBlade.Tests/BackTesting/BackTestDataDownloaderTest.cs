using Binance.Net.Clients;
using Bybit.Net.Clients;
using CryptoBlade.BackTesting.Bybit;
using CryptoBlade.BackTesting;
using CryptoBlade.BackTesting.Binance;
using CryptoBlade.Configuration;
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

        private BybitHistoricalDataDownloader CreateBybitHistoricalDataDownloader(IHistoricalDataStorage storage)
        {
            var bybit = new BybitRestClient();
            var cbRestClientOptions = Options.Create(new BybitCbFuturesRestClientOptions
            {
                PlaceOrderAttempts = 5
            });
            var cbRestClient = new BybitCbFuturesRestClient(cbRestClientOptions,
                bybit,
                ApplicationLogging.CreateLogger<BybitCbFuturesRestClient>());
            var downloader = new BybitHistoricalDataDownloader(
                storage,
                ApplicationLogging.CreateLogger<BybitHistoricalDataDownloader>(),
                cbRestClient);

            return downloader;
        }

        private BinanceHistoricalDataDownloader CreateBinanceHistoricalDataDownloader(IHistoricalDataStorage storage)
        {
            var binance = new BinanceRestClient();
            var cbRestClient = new BinanceCbFuturesRestClient(
                ApplicationLogging.CreateLogger<BinanceCbFuturesRestClient>(),
                binance);
            var downloader = new BinanceHistoricalDataDownloader(
                storage,
                ApplicationLogging.CreateLogger<BinanceHistoricalDataDownloader>(),
                cbRestClient);

            return downloader;
        }

        [Fact]
        public async Task AllBackTestDataShouldBeDownloaded()
        {
            var dataSource = DataSource.Binance;
            var options = Options.Create(new ProtoHistoricalDataStorageOptions
            {
                Directory = ConfigConstants.DefaultHistoricalDataDirectory,
            });
            var storage = new ProtoHistoricalDataStorage(options);
            IHistoricalDataDownloader downloader;
            // ReSharper disable UnreachableSwitchCaseDueToIntegerAnalysis
            switch (dataSource)
            {
                case DataSource.Bybit:
                    downloader = CreateBybitHistoricalDataDownloader(storage);
                    break;
                case DataSource.Binance: 
                    downloader = CreateBinanceHistoricalDataDownloader(storage);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            // ReSharper restore UnreachableSwitchCaseDueToIntegerAnalysis
            var start = new DateTime(2023, 9, 1);
            var end = new DateTime(2023, 10, 6);
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
