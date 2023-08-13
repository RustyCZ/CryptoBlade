using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Models;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Services
{
    public class DefaultTradingStrategyManager : TradeStrategyManagerBase
    {
        private readonly ILogger<DefaultTradingStrategyManager> m_logger;
        private readonly IOptions<TradingBotOptions> m_options;

        public DefaultTradingStrategyManager(IOptions<TradingBotOptions> options, 
            ILogger<DefaultTradingStrategyManager> logger, 
            ITradingStrategyFactory strategyFactory,
            ICbFuturesRestClient restClient,
            ICbFuturesSocketClient socketClient, 
            IWalletManager walletManager) 
            : base(options, logger, strategyFactory, restClient, socketClient, walletManager)
        {
            m_options = options;
            m_logger = logger;
        }

        protected override async Task StrategyExecutionAsync(CancellationToken cancel)
        {
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessStrategyDataAsync(cancel);

                        var hasInconsistent = Strategies.Values.Any(x => !x.ConsistentData);
                        if (hasInconsistent)
                        {
                            m_logger.LogWarning("Some strategies have inconsistent data. Reinitialize.");
                            await ReInitializeStrategies(cancel);
                        }

                        var strategyState = await UpdateTradingStatesAsync(cancel);
                        m_logger.LogDebug(
                            "Total long exposure: {LongExposure}, total short exposure: {ShortExposure}, long WE: {LongWE}, short WE: {ShortWE}",
                            strategyState.TotalLongExposure,
                            strategyState.TotalShortExposure,
                            strategyState.TotalWalletLongExposure,
                            strategyState.TotalWalletShortExposure);


                        List<string> symbolsToProcess = new List<string>();

                        using (await Lock.LockAsync())
                        {
                            while (StrategyExecutionChannel.Reader.TryRead(out var symbol))
                                symbolsToProcess.Add(symbol);

                            var inTradeSymbols = symbolsToProcess
                                .Where(x => Strategies.TryGetValue(x, out var strategy) && strategy.IsInTrade)
                                .Distinct()
                                .ToArray();

                            int remainingSlots = m_options.Value.MaxRunningStrategies -
                                                 Strategies.Values.Count(x => x.IsInTrade);
                            var strategiesWithMostVolume = Strategies.Select(x => new
                                {
                                    Strategy = x,
                                    x.Value.Indicators
                                })
                                .Where(x => x.Indicators.Any(i =>
                                    i.Name == nameof(IndicatorType.MainTimeFrameVolume) && i.Value is decimal))
                                .Where(x => !x.Strategy.Value.IsInTrade &&
                                            (x.Strategy.Value.HasBuySignal && x.Strategy.Value.DynamicQtyLong.HasValue) 
                                            || (x.Strategy.Value.HasSellSignal && x.Strategy.Value.DynamicQtyShort.HasValue))
                                .Select(x => new
                                {
                                    Strategy = x,
                                    MainTimeFrameVolume = (decimal)x.Indicators
                                        .First(i => i.Name == nameof(IndicatorType.MainTimeFrameVolume)).Value
                                })
                                .OrderByDescending(x => x.MainTimeFrameVolume)
                                .Take(remainingSlots)
                                .Select(x => x.Strategy.Strategy.Value.Symbol)
                                .ToArray();

                            List<Task> executionTasks = new List<Task>();
                            if (inTradeSymbols.Any())
                            {
                                var executeParams =
                                    inTradeSymbols.ToDictionary(x => x, _ => new ExecuteParams(true, true));
                                await PrepareStrategyExecutionAsync(executionTasks, inTradeSymbols, executeParams,
                                    cancel);
                            }


                            if (strategiesWithMostVolume.Any())
                            {
                                var executeParams =
                                    strategiesWithMostVolume.ToDictionary(x => x, _ => new ExecuteParams(true, true));
                                await PrepareStrategyExecutionAsync(executionTasks, strategiesWithMostVolume,
                                    executeParams, cancel);
                            }

                            LogRemainingSlots(remainingSlots);
                            await Task.WhenAll(executionTasks);
                            DateTime utcNow = DateTime.UtcNow;
                            Interlocked.Exchange(ref m_lastExecutionTimestamp, utcNow.Ticks);
                        }
                    }
                    catch (Exception e)
                    {
                        m_logger.LogError(e, "Error while executing strategies");
                    }
                    finally
                    {
                        // wait a little bit so we are not rate limited
                        await StrategyExecutionNextCycleDelayAsync(cancel);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                m_logger.LogInformation("Strategy execution cancelled.");
            }
        }
    }
}
