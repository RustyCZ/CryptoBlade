using System.Reflection;
using Bybit.Net;
using CryptoBlade.BackTesting;
using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.HealthChecks;
using CryptoBlade.Helpers;
using CryptoBlade.Services;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Wallet;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Options;
using Bybit.Net.Clients;
using CryptoBlade.BackTesting.Bybit;

namespace CryptoBlade
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // hedge insane funding rates
            // better rate limiting
            // executed orders page
            // list of open orders page
            // bybit server time component, check against candle data in periodic verification check
            // Special readonly manager
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables("CB_");
            var debugView = builder.Configuration.GetDebugView();
            string[] debugViewLines = debugView.Split(Environment.NewLine)
                .Where(x => !x.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
                && !x.Contains("ApiSecret", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            debugView = string.Join(Environment.NewLine, debugViewLines);

            var tradingBotOptions = builder.Configuration.GetSection("TradingBot").Get<TradingBotOptions>();
            TradingMode tradingMode = tradingBotOptions!.TradingMode;
            var exchangeAccount =
                tradingBotOptions.Accounts.FirstOrDefault(x => string.Equals(x.Name, tradingBotOptions.AccountName, StringComparison.Ordinal));
            string apiKey = exchangeAccount?.ApiKey ?? string.Empty;
            string apiSecret = exchangeAccount?.ApiSecret ?? string.Empty;
            bool hasApiCredentials = !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddHealthChecks()
                .AddCheck<TradeExecutionHealthCheck>("TradeExecution");
            builder.Services.AddHostedService<TradingHostedService>();
            builder.Services.Configure<TradingBotOptions>(builder.Configuration.GetSection("TradingBot"));
            builder.Services.AddSingleton<ITradingStrategyFactory, TradingStrategyFactory>();

            bool isBackTest = tradingBotOptions.IsBackTest();

            if (isBackTest)
            {
                AddBackTestDependencies(builder);
            }
            else
            {
                AddLiveDependencies(builder);
            }

            var app = builder.Build();
            var lf = app.Services.GetRequiredService<ILoggerFactory>();
            ApplicationLogging.LoggerFactory = lf;
            var logger = ApplicationLogging.LoggerFactory.CreateLogger("Startup");
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            logger.LogInformation($"CryptoBlade v{version}");
            logger.LogInformation(debugView);

            if (!hasApiCredentials)
                app.Logger.LogWarning("No API credentials found!.");

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();
            app.MapHealthChecks("/healthz");
            app.MapRazorPages();
            app.Run();
        }

        private static void AddBackTestDependencies(WebApplicationBuilder builder)
        {
            var tradingBotOptions = builder.Configuration.GetSection("TradingBot").Get<TradingBotOptions>();
            TradingMode tradingMode = tradingBotOptions!.TradingMode;

            if (tradingMode == TradingMode.DynamicBackTest)
                builder.Services.AddSingleton<ITradeStrategyManager, BackTestDynamicTradingStrategyManager>();

            builder.Services.AddSingleton<IWalletManager, WalletManager>();
            builder.Services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<BackTestExchangeOptions>>();
                var backtestDownloader = sp.GetRequiredService<IBackTestDataDownloader>();
                var historicalDataStorage = sp.GetRequiredService<IHistoricalDataStorage>();
                var cbRestClient = CreateUnauthorizedBybitClient();

                var exchange = new BackTestExchange(
                    options, 
                    backtestDownloader, 
                    historicalDataStorage,
                    cbRestClient);
                return exchange;
            });
            builder.Services.AddOptions<BackTestExchangeOptions>().Configure(x =>
            {
                x.Start = tradingBotOptions.BackTest.Start;
                x.End = tradingBotOptions.BackTest.End;
                x.InitialBalance = tradingBotOptions.BackTest.InitialBalance;
                x.StartupCandleData = tradingBotOptions.BackTest.StartupCandleData;
                x.Symbols = tradingBotOptions.Whitelist;
                x.MakerFeeRate = tradingBotOptions.MakerFeeRate;
                x.TakerFeeRate = tradingBotOptions.TakerFeeRate;
                x.OptimisticFill = tradingBotOptions.BackTest.OptimisticFill;
            });
            builder.Services.AddSingleton<IBackTestDataDownloader, BackTestDataDownloader>();
            builder.Services.AddSingleton<IHistoricalDataDownloader>(provider =>
            {
                var historicalDataStorage = provider.GetRequiredService<IHistoricalDataStorage>();
                var logger = ApplicationLogging.CreateLogger<BybitHistoricalDataDownloader>();
                var cbRestClient = CreateUnauthorizedBybitClient();
                BybitHistoricalDataDownloader downloader = new BybitHistoricalDataDownloader(
                    historicalDataStorage,
                    logger,
                    cbRestClient);
                return downloader;
            });
            builder.Services.AddSingleton<IHistoricalDataStorage, HistoricalDataStorage>();
            builder.Services.AddOptions<HistoricalTradesStorageOptions>().Configure(x =>
            {
                x.Directory = "HistoricalData";
            });
            builder.Services.AddSingleton<ICbFuturesRestClient>(sp => sp.GetRequiredService<BackTestExchange>());
            builder.Services.AddSingleton<ICbFuturesSocketClient>(sp => sp.GetRequiredService<BackTestExchange>());
            builder.Services.AddSingleton<IBackTestRunner>(sp => sp.GetRequiredService<BackTestExchange>());
            builder.Services.AddHostedService<BackTestPerformanceTracker>();
            builder.Services.AddOptions<BackTestPerformanceTrackerOptions>().Configure(x =>
            {
                x.BackTestsDirectory = "BackTests";
            });
        }

        private static BybitCbFuturesRestClient CreateUnauthorizedBybitClient()
        {
            var bybit = new BybitRestClient();
            var cbRestClientOptions = Options.Create(new BybitCbFuturesRestClientOptions
            {
                PlaceOrderAttempts = 5
            });
            var cbRestClient = new BybitCbFuturesRestClient(cbRestClientOptions,
                bybit,
                ApplicationLogging.CreateLogger<BybitCbFuturesRestClient>());

            return cbRestClient;
        }

        private static void AddLiveDependencies(WebApplicationBuilder builder)
        {
            var tradingBotOptions = builder.Configuration.GetSection("TradingBot").Get<TradingBotOptions>();
            TradingMode tradingMode = tradingBotOptions!.TradingMode;

            if (tradingMode == TradingMode.Readonly || tradingMode == TradingMode.Dynamic)
            {
                builder.Services.AddSingleton<ITradeStrategyManager, DynamicTradingStrategyManager>();
            }
            else if (tradingMode == TradingMode.Normal)
            {
                builder.Services.AddSingleton<ITradeStrategyManager, DefaultTradingStrategyManager>();
            }

            var exchangeAccount =
                tradingBotOptions.Accounts.FirstOrDefault(x => string.Equals(x.Name, tradingBotOptions.AccountName, StringComparison.Ordinal));
            string apiKey = exchangeAccount?.ApiKey ?? string.Empty;
            string apiSecret = exchangeAccount?.ApiSecret ?? string.Empty;
            bool hasApiCredentials = !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret);

            if (tradingMode == TradingMode.Readonly || tradingMode == TradingMode.Dynamic)
            {
                builder.Services.AddSingleton<ITradeStrategyManager, DynamicTradingStrategyManager>();
            }
            else if (tradingMode == TradingMode.Normal)
            {
                builder.Services.AddSingleton<ITradeStrategyManager, DefaultTradingStrategyManager>();
            }

            if (hasApiCredentials)
                builder.Services.AddSingleton<IWalletManager, WalletManager>();
            else
                builder.Services.AddSingleton<IWalletManager, NullWalletManager>();

            builder.Services.AddBybit(
                    restOptions =>
                    {
                        restOptions.V5Options.RateLimitingBehaviour = RateLimitingBehaviour.Wait;
                        if (hasApiCredentials)
                            restOptions.V5Options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                        restOptions.ReceiveWindow = TimeSpan.FromSeconds(10);
                        restOptions.AutoTimestamp = true;
                        restOptions.TimestampRecalculationInterval = TimeSpan.FromSeconds(10);
                    },
                    socketClientOptions =>
                    {
                        if (hasApiCredentials)
                            socketClientOptions.V5Options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                    })
                .AddLogging(options =>
                {
                    options.AddSimpleConsole(o =>
                    {
                        o.UseUtcTimestamp = true;
                        o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                    });
                });

            builder.Services.AddSingleton<ICbFuturesRestClient, BybitCbFuturesRestClient>();
            builder.Services.AddOptions<BybitCbFuturesRestClientOptions>().Configure(options =>
            {
                options.PlaceOrderAttempts = tradingBotOptions.PlaceOrderAttempts;
            });
            builder.Services.AddSingleton<ICbFuturesSocketClient, BybitCbFuturesSocketClient>();
        }
    }
}