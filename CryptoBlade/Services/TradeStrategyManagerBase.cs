using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Enums.V5;
using Bybit.Net.Interfaces.Clients;
using Bybit.Net.Objects.Models.Socket.Derivatives;
using Bybit.Net.Objects.Models.V5;
using CryptoBlade.Configuration;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Policies;
using CryptoBlade.Strategies.Wallet;
using CryptoExchange.Net.Sockets;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using OrderStatus = CryptoBlade.Models.OrderStatus;
using PositionSide = CryptoBlade.Models.PositionSide;

namespace CryptoBlade.Services
{
    public abstract class TradeStrategyManagerBase : ITradeStrategyManager
    {
        private readonly ILogger<TradeStrategyManagerBase> m_logger;
        private readonly Dictionary<string, ITradingStrategy> m_strategies;
        private readonly ITradingStrategyFactory m_strategyFactory;
        private readonly IBybitRestClient m_bybitRestClient;
        private readonly IBybitSocketClient m_bybitSocketClient;
        private readonly IOptions<TradingBotOptions> m_options;
        private CancellationTokenSource? m_cancelSource;
        private readonly List<UpdateSubscription> m_subscriptions;
        private readonly Channel<string> m_strategyExecutionChannel;
        private readonly AsyncLock m_lock;
        private Task? m_initTask;
        private Task? m_strategyExecutionTask;
        private readonly IWalletManager m_walletManager;
        protected long m_lastExecutionTimestamp;

        protected TradeStrategyManagerBase(IOptions<TradingBotOptions> options,
            ILogger<TradeStrategyManagerBase> logger,
            ITradingStrategyFactory strategyFactory,
            IBybitRestClient bybitRestClient,
            IBybitSocketClient bybitSocketClient, 
            IWalletManager walletManager)
        {
            m_lock = new AsyncLock();
            m_options = options;
            m_strategyFactory = strategyFactory;
            m_bybitRestClient = bybitRestClient;
            m_bybitSocketClient = bybitSocketClient;
            m_walletManager = walletManager;
            m_logger = logger;
            m_strategies = new Dictionary<string, ITradingStrategy>();
            m_subscriptions = new List<UpdateSubscription>();
            m_strategyExecutionChannel = Channel.CreateUnbounded<string>();
            m_lastExecutionTimestamp = DateTime.UtcNow.Ticks;
        }

        protected Dictionary<string, ITradingStrategy> Strategies => m_strategies;
        protected Channel<string> StrategyExecutionChannel => m_strategyExecutionChannel;
        protected AsyncLock Lock => m_lock;

        public DateTime LastExecution
        {
            get
            {
                var last = Interlocked.Read(ref m_lastExecutionTimestamp);
                return new DateTime(last, DateTimeKind.Utc);
            }
        }

        public Task<ITradingStrategy[]> GetStrategiesAsync(CancellationToken cancel)
        {
            return Task.FromResult(m_strategies.Values.Select(x => x).ToArray());
        }

        public async Task StartStrategiesAsync(CancellationToken cancel)
        {
            await CreateStrategiesAsync();
            m_cancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            CancellationToken ctsCancel = m_cancelSource.Token;
            m_initTask = Task.Run(async () => await InitStrategiesAsync(ctsCancel), ctsCancel);
        }

        private async Task InitStrategiesAsync(CancellationToken cancel)
        {
            var symbolInfo = await GetSymbolInfoAsync(cancel);
            Dictionary<string, SymbolInfo> symbolInfoDict = symbolInfo
                .DistinctBy(x => x.Name)
                .ToDictionary(x => x.Name, x => x);
            List<string> missingSymbols = m_strategies.Select(x => x.Key)
                .Where(x => !symbolInfoDict.ContainsKey(x))
                .ToList();
            // log missing symbols
            foreach (var symbol in missingSymbols)
                m_logger.LogWarning($"Symbol {symbol} is missing from the exchange.");

            foreach (string missingSymbol in missingSymbols)
                m_strategies.Remove(missingSymbol);

            foreach (ITradingStrategy strategy in m_strategies.Values)
            {
                if (symbolInfoDict.TryGetValue(strategy.Symbol, out var info))
                    await strategy.SetupSymbolAsync(info, cancel);
            }
            
            var symbols = m_strategies.Values.Select(x => x.Symbol).ToArray();
            await UpdateTradingStatesAsync(cancel);

            var orderUpdateSubscription = await ExchangePolicies.RetryForever
                .ExecuteAsync(async () =>
                {
                    var subscriptionResult = await m_bybitSocketClient.V5PrivateApi.SubscribeToOrderUpdatesAsync(
                        OnOrderUpdate, cancel);
                    if (subscriptionResult.GetResultOrError(out var data, out var error))
                        return data;
                    throw new InvalidOperationException(error.Message);
                });
            orderUpdateSubscription.AutoReconnect(m_logger);
            m_subscriptions.Add(orderUpdateSubscription);

            var timeFrames = m_strategies.Values
                .SelectMany(x => x.RequiredTimeFrameWindows.Select(tfw => tfw.TimeFrame))
                .Distinct()
                .ToArray();

            foreach (TimeFrame timeFrame in timeFrames)
            {
                var klineUpdatesSubscription = await ExchangePolicies.RetryForever
                    .ExecuteAsync(async () =>
                    {
                        var subscriptionResult = await m_bybitSocketClient.V5LinearApi.SubscribeToKlineUpdatesAsync(
                            symbols,
                            timeFrame.ToKlineInterval(), OnKlineUpdate,
                            cancel);
                        if (subscriptionResult.GetResultOrError(out var data, out var error))
                            return data;
                        throw new InvalidOperationException(error.Message);
                    });
                klineUpdatesSubscription.AutoReconnect(m_logger);
                m_subscriptions.Add(klineUpdatesSubscription);
            }

            List<Task> initTasks = new();
            foreach (var strategy in m_strategies.Values)
            {
                var initTask = InitializeStrategy(strategy, cancel);
                initTasks.Add(initTask);
            }

            foreach (ITradingStrategy strategiesValue in m_strategies.Values)
            {
                if (strategiesValue.IsInTrade)
                {
                    m_logger.LogInformation(
                        $"Strategy {strategiesValue.Name}:{strategiesValue.Symbol} is in trade after initialization. Scheduling trade execution.");
                    await m_strategyExecutionChannel.Writer.WriteAsync(strategiesValue.Symbol, cancel);
                }
            }

            var tickerSubscription = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                var tickerSubscriptionResult = await m_bybitSocketClient.V5LinearApi
                    .SubscribeToTickerUpdatesAsync(symbols, OnTicker, cancel);
                if (tickerSubscriptionResult.GetResultOrError(out var data, out var error))
                    return data;
                throw new InvalidOperationException(error.Message);
            });
            tickerSubscription.AutoReconnect(m_logger);
            m_subscriptions.Add(tickerSubscription);

            await Task.WhenAll(initTasks);
            m_strategyExecutionTask = Task.Run(async () => await StrategyExecutionAsync(cancel), cancel);
        }

        private void OnOrderUpdate(DataEvent<IEnumerable<BybitOrderUpdate>> obj)
        {
            foreach (BybitOrderUpdate bybitOrderUpdate in obj.Data)
            {
                if(bybitOrderUpdate.Category != Category.Linear)
                    continue;
                if (bybitOrderUpdate.Status == Bybit.Net.Enums.V5.OrderStatus.Filled)
                {
                    // we want to schedule the strategy execution for the symbol after the order is filled
                    m_logger.LogInformation($"Order {bybitOrderUpdate.OrderId} for symbol {bybitOrderUpdate.Symbol} is filled. Scheduling trade execution.");
                    m_strategyExecutionChannel.Writer.TryWrite(bybitOrderUpdate.Symbol);
                }
            }
        }

        private async void OnTicker(DataEvent<BybitLinearTickerUpdate> obj)
        {
            using (await m_lock.LockAsync())
            {
                var ticker = obj.Data.ToTicker();
                if (ticker == null)
                    return;
                if (m_strategies.TryGetValue(obj.Data.Symbol, out var strategy))
                    await strategy.UpdatePriceDataSync(ticker, CancellationToken.None);
            }
        }

        private async void OnKlineUpdate(DataEvent<IEnumerable<BybitKlineUpdate>> obj)
        {
            if (string.IsNullOrEmpty(obj.Topic))
                return;
            string[] topicParts = obj.Topic.Split('.');
            if (topicParts.Length != 2)
                return;
            string symbol = topicParts[1];
            using (await m_lock.LockAsync())
            {
                foreach (BybitKlineUpdate bybitKlineUpdate in obj.Data)
                {
                    if (!bybitKlineUpdate.Confirm)
                        continue;
                    if (m_strategies.TryGetValue(symbol, out var strategy))
                    {
                        var candle = bybitKlineUpdate.ToCandle();
                        await strategy.AddCandleDataAsync(candle, CancellationToken.None);
                        bool isPrimaryCandle = strategy.RequiredTimeFrameWindows.Any(x => x.TimeFrame == candle.TimeFrame);
                        if (isPrimaryCandle)
                        {
                            m_logger.LogInformation(
                                $"Strategy {strategy.Name}:{strategy.Symbol} received primary candle. Scheduling trade execution.");
                            await m_strategyExecutionChannel.Writer.WriteAsync(strategy.Symbol, CancellationToken.None);
                        }
                    }
                }
            }
        }

        private async Task<SymbolInfo[]> GetSymbolInfoAsync(CancellationToken cancel)
        {
            var symbolData = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                List<SymbolInfo> symbolInfo = new List<SymbolInfo>();
                string? cursor = null;
                while (true)
                {
                    var symbolsResult = await m_bybitRestClient.V5Api.ExchangeData.GetLinearInverseSymbolsAsync(
                        Category.Linear,
                        null,
                        null,
                        null,
                        cursor,
                        cancel);
                    if (!symbolsResult.GetResultOrError(out var data, out var error))
                        throw new InvalidOperationException(error.Message);
                    var s = data.List
                        .Where(x => string.Equals(Assets.QuoteAsset, x.QuoteAsset))
                        .Select(x => x.ToSymbolInfo());
                    symbolInfo.AddRange(s);
                    if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                        break;
                    cursor = data.NextPageCursor;
                }

                return symbolInfo.ToArray();
            });

            return symbolData;
        }

        public async Task StopStrategiesAsync(CancellationToken cancel)
        {
            m_logger.LogInformation("Stopping strategies...");
            m_cancelSource?.Cancel();
            foreach (var subscription in m_subscriptions)
                await subscription.CloseAsync();
            var initTask  = m_initTask;
            try
            {
                if(initTask != null)
                    await initTask;
            }
            catch (OperationCanceledException)
            {
            }
            var executionTask = m_strategyExecutionTask;
            try
            {
                if (executionTask != null)
                    await executionTask;
            }
            catch (OperationCanceledException)
            {
            }

            m_cancelSource?.Dispose();
            m_strategies.Clear();
            m_subscriptions.Clear();
            m_logger.LogInformation("Strategies stopped.");
        }

        private Task CreateStrategiesAsync()
        {
            var config = m_options.Value;
            List<string> finalSymbolList = config.Whitelist
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Except(config.Blacklist.Where(x => !string.IsNullOrWhiteSpace(x)))
                .Distinct()
                .ToList();
            foreach (string symbol in finalSymbolList)
            {
                var strategy = m_strategyFactory.CreateStrategy(config, symbol);
                m_strategies[strategy.Symbol] = strategy;
            }

            return Task.CompletedTask;
        }

        protected async Task ReInitializeStrategies(CancellationToken cancel)
        {
            using (await m_lock.LockAsync())
            {
                List<Task> initTasks = new List<Task>();
                foreach (var tradingStrategy in m_strategies)
                {
                    var initTask = InitializeStrategy(tradingStrategy.Value, cancel);
                    initTasks.Add(initTask);
                }
                await Task.WhenAll(initTasks);
            }
        }

        private async Task InitializeStrategy(ITradingStrategy strategy, CancellationToken cancel)
        {
            var symbol = strategy.Symbol;
            var timeFrames = strategy.RequiredTimeFrameWindows;
            List<Candle> allCandles = new List<Candle>();
            foreach (var timeFrame in timeFrames)
            {
                var candles = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
                {
                    var dataResponse = await m_bybitRestClient.V5Api.ExchangeData.GetKlinesAsync(
                        Category.Linear,
                        symbol,
                        timeFrame.TimeFrame.ToKlineInterval(),
                        null,
                        null,
                        timeFrame.WindowSize + 1,
                        cancel);
                    if (!dataResponse.GetResultOrError(out var data, out var error))
                    {
                        throw new InvalidOperationException(error.Message);
                    }

                    // we don't want the last candle, because it's not closed yet
                    var candleData = data.List.Skip(1).Reverse().Select(x => x.ToCandle(timeFrame.TimeFrame))
                        .ToArray();
                    return candleData;
                });
                allCandles.AddRange(candles);
            }

            var priceData = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                var priceDataRes = await m_bybitRestClient.V5Api.ExchangeData.GetLinearInverseTickersAsync(
                    Category.Linear,
                    symbol, null,
                    null, cancel);
                if (priceDataRes.GetResultOrError(out var data, out var error))
                {
                    return data.List;
                }

                throw new InvalidOperationException(error.Message);
            });

            var ticker = priceData.Select(x => x.ToTicker()).First();
            await strategy.InitializeAsync(allCandles.ToArray(), ticker, cancel);
        }

        private async Task<Order[]> GetOrdersAsync(CancellationToken cancel)
        {
            var orders = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                List<Order> orders = new List<Order>();
                string? cursor = null;
                while (true)
                {
                    var ordersResult = await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                        Category.Linear,
                        settleAsset: Assets.QuoteAsset,
                        cursor: cursor,
                        ct: cancel);
                    if (!ordersResult.GetResultOrError(out var data, out var error))
                        throw new InvalidOperationException(error.Message);
                    orders.AddRange(data.List.Select(x => x.ToOrder()));
                    if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                        break;
                    cursor = data.NextPageCursor;
                }

                return orders.ToArray();
            });

            return orders;
        }

        private async Task<Position[]> GetOpenPositions(CancellationToken cancel)
        {
            var positions = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                List<Position> positions = new List<Position>();
                string? cursor = null;
                while (true)
                {
                    var positionResult = await m_bybitRestClient.V5Api.Trading.GetPositionsAsync(
                        Category.Linear,
                        settleAsset: Assets.QuoteAsset,
                        cursor: cursor,
                        ct: cancel);
                    if (!positionResult.GetResultOrError(out var data, out var error))
                        throw new InvalidOperationException(error.Message);
                    foreach (var bybitPosition in data.List)
                    {
                        var position = bybitPosition.ToPosition();
                        if(position == null)
                            m_logger.LogWarning($"Could not convert position for symbol: {bybitPosition.Symbol}");
                        else
                            positions.Add(position);
                    }

                    if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                        break;
                    cursor = data.NextPageCursor;
                }

                return positions.ToArray();
            });

            return positions;
        }

        protected async Task<StrategyState> UpdateTradingStatesAsync(CancellationToken cancel)
        {
            m_logger.LogInformation("Updating trading state of all strategies.");
            var orders = await GetOrdersAsync(cancel);
            var symbolsFromOrders = orders.Where(x => x.Status != OrderStatus.Cancelled
                                                      && x.Status != OrderStatus.PartiallyFilledCanceled
                                                      && x.Status != OrderStatus.Deactivated
                                                      && x.Status != OrderStatus.Rejected)
                .Select(x => x.Symbol)
                .Distinct()
                .ToArray();
            var positions = await GetOpenPositions(cancel);

            var positionsPerSymbol = positions
                .DistinctBy(x => (x.Symbol, x.Side))
                .ToDictionary(x => (x.Symbol, x.Side));
            var symbols = symbolsFromOrders
                .Union(positionsPerSymbol.Select(x => x.Key.Symbol))
                .Distinct()
                .ToArray();
            using (await m_lock.LockAsync())
            {
                HashSet<string> activeStrategies = new HashSet<string>(symbols);
                foreach (KeyValuePair<string, ITradingStrategy> tradingStrategy in m_strategies)
                {
                    if(activeStrategies.Contains(tradingStrategy.Key))
                        continue;
                    await tradingStrategy.Value.UpdateTradingStateAsync(null, null, Array.Empty<Order>(), cancel);
                }

                foreach (string symbol in symbols)
                {
                    if (!m_strategies.TryGetValue(symbol, out var strategy))
                        continue;
                    var longPosition = positionsPerSymbol.TryGetValue((symbol, PositionSide.Buy), out var p) ? p : null;
                    var shortPosition = positionsPerSymbol.TryGetValue((symbol, PositionSide.Sell), out p) ? p : null;
                    var openOrders = orders
                        .Where(x => string.Equals(symbol, x.Symbol, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    await strategy.UpdateTradingStateAsync(longPosition, shortPosition, openOrders, cancel);
                }
            }

            decimal longExposure = 0;
            decimal shortExposure = 0;
            if (positions.Any())
            {
                longExposure = positions.Where(x => x.Side == PositionSide.Buy).Sum(x => x.Quantity * x.AveragePrice);
                shortExposure = positions.Where(x => x.Side == PositionSide.Sell).Sum(x => x.Quantity * x.AveragePrice);
            }

            var wallet = m_walletManager.Contract;
            decimal? totalWalletLongExposure = null;
            decimal? totalWalletShortExposure = null;
            if (wallet.WalletBalance.HasValue && wallet.WalletBalance.Value > 0)
            {
                totalWalletLongExposure = longExposure / wallet.WalletBalance;
                totalWalletShortExposure = shortExposure / wallet.WalletBalance;
            }

            return new StrategyState(longExposure, shortExposure, totalWalletLongExposure, totalWalletShortExposure);
        }

        protected abstract Task StrategyExecutionAsync(CancellationToken cancel);

        protected void LogRemainingSlots(int remainingSlots)
        {
            if (remainingSlots < 0)
                remainingSlots = 0;
            m_logger.LogInformation("Remaining strategy slots: {RemainingSlots}", remainingSlots);
        }

        protected void LogRemainingLongSlots(int remainingSlots)
        {
            if (remainingSlots < 0)
                remainingSlots = 0;
            m_logger.LogInformation("Remaining long strategy slots: {RemainingSlots}", remainingSlots);
        }

        protected void LogRemainingShortSlots(int remainingSlots)
        {
            if (remainingSlots < 0)
                remainingSlots = 0;
            m_logger.LogInformation("Remaining short strategy slots: {RemainingSlots}", remainingSlots);
        }

        protected Task PrepareStrategyExecutionAsync(List<Task> strategyExecutionTasks, 
            string[] symbols, 
            Dictionary<string, ExecuteParams> executionParams, 
            CancellationToken cancel)
        {
            foreach (var symbol in symbols)
            {
                if (!m_strategies.TryGetValue(symbol, out var strategy))
                    continue;
                Task strategyExecutionTask = Task.Run(async () =>
                {
                    try
                    {
                        executionParams.TryGetValue(symbol, out var execParam);
                        await strategy.ExecuteAsync(execParam.AllowLongOpen, execParam.AllowShortOpen, cancel);
                    }
                    catch (Exception e)
                    {
                        m_logger.LogError(e, "Error while executing strategy {Name} for symbol {Symbol}",
                            strategy.Name, symbol);
                    }
                }, cancel);
                strategyExecutionTasks.Add(strategyExecutionTask);
            }
            return Task.CompletedTask;
        }

        protected readonly record struct StrategyState(
            decimal TotalLongExposure, 
            decimal TotalShortExposure, 
            decimal? TotalWalletLongExposure, 
            decimal? TotalWalletShortExposure);

        protected record struct ExecuteParams(bool AllowLongOpen, bool AllowShortOpen);
    }
}