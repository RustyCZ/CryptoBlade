using Bybit.Net.Clients;
using CryptoBlade.Exchanges;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace CryptoBlade.Tests.Exchanges
{
    public class BybitCbFuturesRestClientTest : TestBase
    {
        private readonly ILoggerFactory m_loggerFactory;

        public BybitCbFuturesRestClientTest(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
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
        public async Task ShouldGetFundingRates()
        {
            var bybit = new BybitRestClient();
            var cbRestClientOptions = Options.Create(new BybitCbFuturesRestClientOptions
            {
                PlaceOrderAttempts = 5
            });
            var cbRestClient = new BybitCbFuturesRestClient(cbRestClientOptions,
                bybit,
                m_loggerFactory.CreateLogger<BybitCbFuturesRestClient>());
            var start = DateTime.UtcNow.Date.AddDays(-1);
            var end = start.AddDays(1);
            var rates = await cbRestClient.GetFundingRatesAsync("BTCUSDT", start, end);
            Assert.NotEmpty(rates);
        }
    }
}