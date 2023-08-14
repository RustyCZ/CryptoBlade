using Bert.RateLimiters;
using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using System.Linq;

namespace CryptoBlade.Services
{
    public class DynamicTradingStrategyManager : TradeStrategyManagerBase
    {
        private readonly record struct UnstuckingSymbols(HashSet<string> LongUnstucking, HashSet<string> ShortUnstucking);
        private readonly ILogger<DynamicTradingStrategyManager> m_logger;
        private readonly IOptions<TradingBotOptions> m_options;
        private readonly RollingWindowThrottler m_strategyShortThrottler;
        private readonly RollingWindowThrottler m_strategyLongThrottler;

        public DynamicTradingStrategyManager(IOptions<TradingBotOptions> options, 
            ILogger<DynamicTradingStrategyManager> logger, 
            ITradingStrategyFactory strategyFactory, 
            ICbFuturesRestClient restClient,
            ICbFuturesSocketClient socketClient, 
            IWalletManager walletManager) 
            : base(options, logger, strategyFactory, restClient, socketClient, walletManager)
        {
            m_options = options;
            m_logger = logger;
            DynamicBotCount dynamicBotCount = m_options.Value.DynamicBotCount;
            m_strategyShortThrottler = new RollingWindowThrottler(m_options.Value.DynamicBotCount.MaxDynamicStrategyOpenPerStep,
                dynamicBotCount.Step);
            m_strategyLongThrottler = new RollingWindowThrottler(m_options.Value.DynamicBotCount.MaxDynamicStrategyOpenPerStep,
                dynamicBotCount.Step);
        }

        protected virtual Task<bool> ShouldShortThrottleAsync(CancellationToken cancel)
        {
            bool shouldThrottle = m_strategyShortThrottler.ShouldThrottle(1, out _);
            return Task.FromResult(shouldThrottle);
        }

        protected virtual Task<bool> ShouldLongThrottleAsync(CancellationToken cancel)
        {
            bool shouldThrottle = m_strategyLongThrottler.ShouldThrottle(1, out _);
            return Task.FromResult(shouldThrottle);
        }

        private async Task<UnstuckingSymbols> ExecutePriorityUnstuckAsync(StrategyState strategyState, CancellationToken cancel)
        {
            Dictionary<string, ExecuteUnstuckParams> executeUnstuckParams = new Dictionary<string, ExecuteUnstuckParams>();
            Unstucking unstucking = m_options.Value.Unstucking;
            if (strategyState.UnrealizedPnlPercent.HasValue &&
                strategyState.UnrealizedPnlPercent.Value < unstucking.ForceUnstuckThresholdPercent)
            {
                var strategiesWithShortLoss = Strategies.Values
                    .Where(x => x.IsInTrade && x.UnrealizedShortPnlPercent.HasValue &&
                                x.UnrealizedShortPnlPercent.Value < unstucking.ForceUnstuckPositionThresholdPercent)
                    .Select(x => x.Symbol);
                foreach (string s in strategiesWithShortLoss)
                {
                    executeUnstuckParams.TryGetValue(s, out var unstuckArgs);
                    executeUnstuckParams[s] = unstuckArgs with { UnstuckShort = true, ForceUnstuckShort = true };
                }

                var strategiesWithLongLoss = Strategies.Values
                    .Where(x => x.IsInTrade && x.UnrealizedLongPnlPercent.HasValue &&
                                x.UnrealizedLongPnlPercent.Value < unstucking.ForceUnstuckPositionThresholdPercent)
                    .Select(x => x.Symbol);
                foreach (string s in strategiesWithLongLoss)
                {
                    executeUnstuckParams.TryGetValue(s, out var unstuckArgs);
                    executeUnstuckParams[s] = unstuckArgs with { UnstuckLong = true, ForceUnstuckLong = true };
                }
            }
            else if (strategyState.UnrealizedPnlPercent.HasValue &&
                     strategyState.UnrealizedPnlPercent.Value < unstucking.SlowUnstuckThresholdPercent)
            {
                var strategiesWithShortLoss = Strategies.Values
                    .Where(x => x.IsInTrade && x.UnrealizedShortPnlPercent.HasValue &&
                                x.UnrealizedShortPnlPercent.Value < unstucking.SlowUnstuckPositionThresholdPercent)
                    .Select(x => x.Symbol);
                foreach (string s in strategiesWithShortLoss)
                {
                    executeUnstuckParams.TryGetValue(s, out var unstuckArgs);
                    executeUnstuckParams[s] = unstuckArgs with { UnstuckShort = true };
                }

                var strategiesWithLongLoss = Strategies.Values
                    .Where(x => x.IsInTrade 
                                && x.UnrealizedLongPnlPercent.HasValue 
                                && x.UnrealizedLongPnlPercent.Value < unstucking.SlowUnstuckPositionThresholdPercent)
                    .Select(x => x.Symbol);
                foreach (string s in strategiesWithLongLoss)
                {
                    executeUnstuckParams.TryGetValue(s, out var unstuckArgs);
                    executeUnstuckParams[s] = unstuckArgs with { UnstuckLong = true };
                }
            }

            try
            {
                List<Task> executionTasks = new List<Task>();
                await PrepareStrategyUnstuckExecutionAsync(executionTasks, executeUnstuckParams.Keys.ToArray(), executeUnstuckParams,
                    cancel);
                await Task.WhenAll(executionTasks);
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Error executing unstuck");
            }

            HashSet<string> shortUnstucking = new HashSet<string>(
                executeUnstuckParams
                    .Where(x => x.Value.UnstuckShort || x.Value.ForceUnstuckShort).Select(x => x.Key));
            HashSet<string> longUnstucking = new HashSet<string>(executeUnstuckParams
                .Where(x => x.Value.UnstuckLong || x.Value.ForceUnstuckLong).Select(x => x.Key));
            UnstuckingSymbols unstuckingSymbols = new UnstuckingSymbols(longUnstucking, shortUnstucking);

            return unstuckingSymbols;
        }

        protected override async Task StrategyExecutionAsync(CancellationToken cancel)
        {
            try
            {
                DynamicBotCount dynamicBotCount = m_options.Value.DynamicBotCount;
                while (!cancel.IsCancellationRequested)
                {
                    try
                    {
                        // TODO unstuck strategy
                        // at 30% unrealized profit select strategy that have worst unrealized profit and have signal on that position
                        // at 50% unrealized profit - start unstucking everything
                        // for long position - there should be sell signal
                        // for short position - there should be buy signal
                        // strategies that are unstucking should not place order

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
                            HashSet<string> unstuckingSymbols = new HashSet<string>();
                            if (m_options.Value.Unstucking.Enabled)
                            {
                                var unstuckingSymbolsLocal = await ExecutePriorityUnstuckAsync(strategyState, cancel);
                                unstuckingSymbols =
                                    new HashSet<string>(
                                        unstuckingSymbolsLocal.LongUnstucking.Concat(unstuckingSymbolsLocal.ShortUnstucking));
                            }
                            
                            // exclude unstucking symbols from processing, it can probably be optimized to allow execution for some positions
                            var inTradeSymbols = symbolsToProcess
                                .Where(x => Strategies.TryGetValue(x, out var strategy) && strategy.IsInTrade && !unstuckingSymbols.Contains(strategy.Symbol))
                                .Distinct()
                                .ToArray();

                            // by default already trading strategies can only maintain existing positions
                            Dictionary<string, ExecuteParams> executeParams =
                                inTradeSymbols.ToDictionary(x => x, _ => new ExecuteParams(false, false));

                            var inLongTradeSymbols = Strategies.Values.Where(x => x.IsInLongTrade).ToArray();
                            var inShortTradeSymbols = Strategies.Values.Where(x => x.IsInShortTrade).ToArray();
                            m_logger.LogDebug(
                                "Long strategies: '{LongStrategies}', short strategies: '{ShortStrategies}'",
                                inLongTradeSymbols.Length, inShortTradeSymbols.Length);

                            int remainingLongSlots = dynamicBotCount.MaxLongStrategies - inLongTradeSymbols.Length;
                            LogRemainingLongSlots(remainingLongSlots);
                            int remainingShortSlots = dynamicBotCount.MaxShortStrategies - inShortTradeSymbols.Length;
                            LogRemainingShortSlots(remainingShortSlots);
                            bool canAddLongPositions = remainingLongSlots > 0
                                                       && strategyState.TotalWalletLongExposure.HasValue
                                                       && strategyState.TotalWalletLongExposure.Value <
                                                       dynamicBotCount.TargetLongExposure;
                            bool canAddShortPositions = remainingShortSlots > 0
                                                        && strategyState.TotalWalletShortExposure.HasValue
                                                        && strategyState.TotalWalletShortExposure.Value <
                                                        dynamicBotCount.TargetShortExposure;
                            m_logger.LogDebug(
                                "Can add long positions: '{CanAddLongPositions}', can add short positions: '{CanAddShortPositions}'.",
                                canAddLongPositions,
                                canAddShortPositions);
                            // we need to put it back to hashset, we might open opposite position on the same symbol
                            HashSet<string> tradeSymbols = new HashSet<string>();
                            foreach (string inTradeSymbol in inTradeSymbols)
                                tradeSymbols.Add(inTradeSymbol);

                            if (canAddLongPositions)
                            {
                                int longStrategiesPerStep = Math.Min(dynamicBotCount.MaxDynamicStrategyOpenPerStep,
                                    remainingLongSlots);
                                var longStrategyCandidates = Strategies.Select(x => new
                                    {
                                        Strategy = x,
                                        x.Value.Indicators
                                    })
                                    .Where(x => x.Indicators.Any(i =>
                                        i.Name == nameof(IndicatorType.MainTimeFrameVolume) && i.Value is decimal))
                                    .Where(x => !x.Strategy.Value.IsInLongTrade && x.Strategy.Value.HasBuySignal &&
                                                x.Strategy.Value.DynamicQtyLong.HasValue)
                                    .Select(x => new
                                    {
                                        Strategy = x,
                                        MainTimeFrameVolume = (decimal)x.Indicators
                                            .First(i => i.Name == nameof(IndicatorType.MainTimeFrameVolume)).Value
                                    })
                                    .OrderByDescending(x => x.MainTimeFrameVolume)
                                    .Take(longStrategiesPerStep)
                                    .Select(x => x.Strategy.Strategy.Value.Symbol)
                                    .ToArray();
                                foreach (string longStrategyCandidate in longStrategyCandidates)
                                {
                                    bool shouldThrottle = await ShouldLongThrottleAsync(cancel);
                                    if (shouldThrottle)
                                        break;
                                    m_logger.LogDebug("Adding long strategy '{LongStrategyCandidate}'",
                                        longStrategyCandidate);
                                    tradeSymbols.Add(longStrategyCandidate);
                                    executeParams.TryGetValue(longStrategyCandidate, out var existingParams);
                                    executeParams[longStrategyCandidate] = existingParams with { AllowLongOpen = true };
                                }
                            }

                            if (canAddShortPositions)
                            {
                                int shortStrategiesPerStep = Math.Min(dynamicBotCount.MaxDynamicStrategyOpenPerStep,
                                    remainingShortSlots);
                                var shortStrategyCandidates = Strategies.Select(x => new
                                    {
                                        Strategy = x,
                                        x.Value.Indicators
                                    })
                                    .Where(x => x.Indicators.Any(i =>
                                        i.Name == nameof(IndicatorType.MainTimeFrameVolume) && i.Value is decimal))
                                    .Where(x => !x.Strategy.Value.IsInShortTrade && x.Strategy.Value.HasSellSignal &&
                                                x.Strategy.Value.DynamicQtyShort.HasValue)
                                    .Select(x => new
                                    {
                                        Strategy = x,
                                        MainTimeFrameVolume = (decimal)x.Indicators
                                            .First(i => i.Name == nameof(IndicatorType.MainTimeFrameVolume)).Value
                                    })
                                    .OrderByDescending(x => x.MainTimeFrameVolume)
                                    .Take(shortStrategiesPerStep)
                                    .Select(x => x.Strategy.Strategy.Value.Symbol)
                                    .ToArray();
                                foreach (string shortStrategyCandidate in shortStrategyCandidates)
                                {
                                    bool shouldThrottle = await ShouldShortThrottleAsync(cancel);
                                    if (shouldThrottle)
                                        break;
                                    m_logger.LogDebug("Adding short strategy '{ShortStrategyCandidate}'",
                                        shortStrategyCandidate);
                                    tradeSymbols.Add(shortStrategyCandidate);
                                    executeParams.TryGetValue(shortStrategyCandidate, out var existingParams);
                                    executeParams[shortStrategyCandidate] =
                                        existingParams with { AllowShortOpen = true };
                                }
                            }

                            List<Task> executionTasks = new List<Task>();
                            await PrepareStrategyExecutionAsync(executionTasks, tradeSymbols.ToArray(), executeParams,
                                cancel);
                            await Task.WhenAll(executionTasks);
                            DateTime utcNow = DateTime.UtcNow;
                            Interlocked.Exchange(ref m_lastExecutionTimestamp, utcNow.Ticks);
                        }
                    }
                    catch (Exception e)
                    {
                        if(e is not OperationCanceledException)
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
