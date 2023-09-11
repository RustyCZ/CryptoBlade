using System.Text.Json;
using Bybit.Net.Clients;
using CryptoBlade.BackTesting;
using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Services;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer
{
    public class OptimizerBacktestExecutor : IBacktestExecutor
    {
        private readonly IHistoricalDataStorage m_historicalDataStorage;

        public OptimizerBacktestExecutor(IHistoricalDataStorage historicalDataStorage)
        {
            m_historicalDataStorage = historicalDataStorage;
        }

        public async Task<BacktestPerformanceResult> ExecuteAsync(IOptions<TradingBotOptions> options, CancellationToken cancel)
        {
            const string historicalDataDirectory = ConfigConstants.DefaultHistoricalDataDirectory;
            IOptions<BackTestExchangeOptions> backTestExchangeOptions = Options.Create(new BackTestExchangeOptions
            {
                Symbols = options.Value.Whitelist,
                Start = options.Value.BackTest.Start,
                End = options.Value.BackTest.End,
                InitialBalance = options.Value.BackTest.InitialBalance,
                StartupCandleData = options.Value.BackTest.StartupCandleData,
                MakerFeeRate = options.Value.MakerFeeRate,
                TakerFeeRate = options.Value.TakerFeeRate,
                HistoricalDataDirectory = historicalDataDirectory,
            });
            IBackTestDataDownloader backTestDataDownloader = new OptimizerBacktestDataDownloader();
            IOptions<BybitCbFuturesRestClientOptions> bybitCbFuturesRestClientOptions = Options.Create(new BybitCbFuturesRestClientOptions
            {
                PlaceOrderAttempts = options.Value.PlaceOrderAttempts,
            });
            BybitCbFuturesRestClient bybitCbFuturesRestClient = new BybitCbFuturesRestClient(bybitCbFuturesRestClientOptions,
                new BybitRestClient(),
                ApplicationLogging.CreateLogger<BybitCbFuturesRestClient>());
            BackTestExchange backTestExchange = new BackTestExchange(
                backTestExchangeOptions,
                backTestDataDownloader,
                m_historicalDataStorage,
                bybitCbFuturesRestClient);
            WalletManager walletManager = new WalletManager(ApplicationLogging.CreateLogger<WalletManager>(), backTestExchange, backTestExchange);
            TradingStrategyFactory tradingStrategyFactory = new TradingStrategyFactory(walletManager, backTestExchange);
            OptimizerApplicationHostApplicationLifetime backtestLifeTime = new OptimizerApplicationHostApplicationLifetime(cancel);
            TaskCompletionSource<bool> backtestDone = new TaskCompletionSource<bool>();
            backtestLifeTime.ApplicationStoppedEvent += _ => backtestDone.TrySetResult(true);
            BackTestDynamicTradingStrategyManager dynamicTradingStrategyManager = new BackTestDynamicTradingStrategyManager(
                options,
                ApplicationLogging.CreateLogger<DynamicTradingStrategyManager>(),
                backTestExchange,
                tradingStrategyFactory,
                walletManager,
                backtestLifeTime);
            IOptions<BackTestPerformanceTrackerOptions> backTestPerformanceTrackerOptions = Options.Create(new BackTestPerformanceTrackerOptions
            {
                BackTestsDirectory = ConfigConstants.BackTestsDirectory,
            });
            ExternalBackTestIdProvider externalBackTestIdProvider = new ExternalBackTestIdProvider(options.Value.CalculateMd5());
            BackTestPerformanceTracker backTestPerformanceTracker = new BackTestPerformanceTracker(
                backTestPerformanceTrackerOptions, options,
                backTestExchange,
                backtestLifeTime,
                externalBackTestIdProvider,
                ApplicationLogging.CreateLogger<BackTestPerformanceTracker>());
            TradingHostedService tradingHostedService =
                new TradingHostedService(dynamicTradingStrategyManager, walletManager);
            await backTestPerformanceTracker.StartAsync(cancel);
            await tradingHostedService.StartAsync(cancel);
            await backtestDone.Task;
            cancel.ThrowIfCancellationRequested(); // we don't want to save the result if the backtest was cancelled
            await backTestPerformanceTracker.StopAsync(cancel);
            await tradingHostedService.StopAsync(cancel);
            var result = backTestPerformanceTracker.Result;
            return result;
        }
    }
}