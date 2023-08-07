using System.Threading.Channels;
using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Enums.V5;
using Bybit.Net.Interfaces.Clients;
using Bybit.Net.Objects.Models;
using CryptoBlade.Configuration;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Policies;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using Skender.Stock.Indicators;
using OrderSide = CryptoBlade.Models.OrderSide;
using OrderStatus = Bybit.Net.Enums.V5.OrderStatus;
using PositionMode = Bybit.Net.Enums.V5.PositionMode;
using TradeMode = Bybit.Net.Enums.TradeMode;

namespace CryptoBlade.Strategies.Common
{
    public abstract class TradingStrategyBase : ITradingStrategy
    {
        private readonly IOptions<TradingStrategyBaseOptions> m_options;
        private readonly Channel<Candle> m_candleBuffer;
        private const int c_defaultCandleBufferSize = 1000;
        private readonly IWalletManager m_walletManager;
        private readonly IBybitRestClient m_bybitRestClient;
        private readonly ILogger m_logger;
        private Random m_random = new Random();

        protected TradingStrategyBase(IOptions<TradingStrategyBaseOptions> options,
            string symbol, 
            TimeFrameWindow[] requiredTimeFrames, 
            IWalletManager walletManager, 
            IBybitRestClient bybitRestClient)
        {
            m_logger = ApplicationLogging.CreateLogger(GetType().FullName ?? nameof(TradingStrategyBase));
            RequiredTimeFrameWindows = requiredTimeFrames;
            m_walletManager = walletManager;
            m_bybitRestClient = bybitRestClient;
            m_options = options;
            Symbol = symbol;
            QuoteQueues = new Dictionary<TimeFrame, QuoteQueue>();
            foreach (TimeFrameWindow requiredTimeFrame in requiredTimeFrames)
                QuoteQueues[requiredTimeFrame.TimeFrame] = new(requiredTimeFrame.WindowSize, requiredTimeFrame.TimeFrame);
            foreach (TimeFrame timeFrame in Enum.GetValues<TimeFrame>())
            {
                if (!QuoteQueues.ContainsKey(timeFrame))
                    QuoteQueues[timeFrame] = new(c_defaultCandleBufferSize, timeFrame);
            }
            m_candleBuffer = Channel.CreateBounded<Candle>(c_defaultCandleBufferSize);
            Indicators = Array.Empty<StrategyIndicator>();
            BuyOrders = Array.Empty<Order>();
            SellOrders = Array.Empty<Order>();
            LongTakeProfitOrders = Array.Empty<Order>();
            ShortTakeProfitOrders = Array.Empty<Order>();
        }

        public abstract string Name { get; }

        public bool IsInTrade
        {
            get { return IsInLongTrade || IsInShortTrade; }
        }
        public bool IsInLongTrade { get; protected set; }
        public bool IsInShortTrade { get; protected set; }

        public string Symbol { get; }
        public SymbolInfo SymbolInfo { get; private set; }
        public decimal? DynamicQtyShort { get; protected set; }
        public decimal? DynamicQtyLong { get; protected set; }
        public decimal? RecommendedMinBalance { get; protected set; }
        public bool HasSellSignal { get; protected set; }
        public bool HasBuySignal { get; protected set; }
        public bool HasSellExtraSignal { get; protected set; }
        public bool HasBuyExtraSignal { get; protected set; }
        public bool ConsistentData { get; protected set; }
        public Ticker? Ticker { get; protected set; }
        public DateTime LastTickerUpdate { get; protected set; }
        public DateTime LastCandleUpdate { get; protected set; }
        public StrategyIndicator[] Indicators { get; protected set; }
        public TimeFrameWindow[] RequiredTimeFrameWindows { get; }
        protected Position? LongPosition { get; set; }
        protected Position? ShortPosition { get; set; }
        protected Order[] BuyOrders { get; set; }
        protected Order[] SellOrders { get; set; }
        protected Order[] LongTakeProfitOrders { get; set; }
        protected Order[] ShortTakeProfitOrders { get; set; }
        protected decimal? ShortTakeProfitPrice { get; set; }
        protected decimal? LongTakeProfitPrice { get; set; }
        public DateTime? NextShortProfitReplacement { get; set; }
        public DateTime? NextLongProfitReplacement { get; set; }
        public DateTime? LastCandleLongOrder { get; set; }
        public DateTime? LastCandleShortOrder { get; set; }

        public Task UpdateTradingStateAsync(Position? longPosition, Position? shortPosition, Order[] orders, CancellationToken cancel)
        {
            BuyOrders = orders.Where(x => x.Side == OrderSide.Buy && x.ReduceOnly.HasValue && !x.ReduceOnly.Value).ToArray();
            SellOrders = orders.Where(x => x.Side == OrderSide.Sell && x.ReduceOnly.HasValue && !x.ReduceOnly.Value).ToArray();
            LongTakeProfitOrders = orders.Where(x => x.Side == OrderSide.Sell && x.ReduceOnly.HasValue && x.ReduceOnly.Value).ToArray();
            ShortTakeProfitOrders = orders.Where(x => x.Side == OrderSide.Buy && x.ReduceOnly.HasValue && x.ReduceOnly.Value).ToArray();
            LongPosition = longPosition;
            ShortPosition = shortPosition;
            IsInLongTrade = longPosition != null || BuyOrders.Length > 0;
            IsInShortTrade = shortPosition != null || SellOrders.Length > 0;
            m_logger.LogInformation(
                $"{Name}: {Symbol} Long position: '{longPosition?.Quantity} @ {longPosition?.AveragePrice}' Short position: '{shortPosition?.Quantity} @ {shortPosition?.AveragePrice}' InTrade: '{IsInTrade}'");
            return Task.CompletedTask;
        }

        protected Dictionary<TimeFrame, QuoteQueue> QuoteQueues { get; }
        protected bool QueueInitialized { get; private set; }
        protected abstract decimal WalletExposureLong { get; }
        protected abstract decimal WalletExposureShort { get; }
        protected abstract int DcaOrdersCount { get; }
        protected abstract bool ForceMinQty { get; }

        public async Task SetupSymbolAsync(SymbolInfo symbol, CancellationToken cancel)
        {
            SymbolInfo = symbol;
            if (symbol.MaxLeverage.HasValue && m_options.Value.TradingMode != TradingMode.Readonly)
            {
                m_logger.LogInformation($"Setting up trading configuration for symbol {symbol.Name}");
                var leverageRes = await ExchangePolicies.
                    RetryTooManyVisits.ExecuteAsync(async () =>
                {
                    var leverageRes = await m_bybitRestClient.V5Api.Account
                        .SetLeverageAsync(Category.Linear, symbol.Name, symbol.MaxLeverage.Value,
                            symbol.MaxLeverage.Value,
                            cancel);
                    return leverageRes;
                });
                bool leverageOk = leverageRes.Success || leverageRes.Error != null && leverageRes.Error.Code == (int)BybitErrorCodes.LeverageNotChanged;
                if (!leverageOk)
                {
                    m_logger.LogError($"Failed to setup leverage. {leverageRes.Error?.Message}");
                    throw new InvalidOperationException($"Failed to setup leverage. {leverageRes.Error?.Message}");
                }

                m_logger.LogInformation($"Leverage set to {symbol.MaxLeverage.Value} for {symbol.Name}");

                var modeChange = await ExchangePolicies.
                    RetryTooManyVisits.ExecuteAsync(async () =>
                    {
                        var modeChange = await m_bybitRestClient.V5Api.Account.SwitchPositionModeAsync(
                            Category.Linear,
                            PositionMode.BothSides,
                            symbol.Name,
                            null,
                            cancel);
                        return modeChange;
                    });
                
                bool modeOk = modeChange.Success || modeChange.Error != null && modeChange.Error.Code == (int)BybitErrorCodes.PositionModeNotChanged;
                if (!modeOk)
                {
                    m_logger.LogError($"Failed to setup position mode. {modeChange.Error?.Message}");
                    throw new InvalidOperationException($"Failed to setup position mode. {modeChange.Error?.Message}");
                }

                m_logger.LogInformation($"Position mode set to {PositionMode.BothSides} for {symbol.Name}");

                var crossMode= await ExchangePolicies.
                    RetryTooManyVisits.ExecuteAsync(async () =>
                    {
                        var crossModeResult = await m_bybitRestClient.V5Api.Account.SwitchCrossIsolatedMarginAsync(
                            Category.Linear, 
                            Symbol,
                            TradeMode.CrossMargin, 
                            symbol.MaxLeverage.Value, 
                            symbol.MaxLeverage.Value,
                            cancel);
                        return crossModeResult;
                    });
                bool crossModeOk = crossMode.Success || crossMode.Error != null && crossMode.Error.Code == (int)BybitErrorCodes.CrossModeNotModified;
                if (!crossModeOk)
                {
                    m_logger.LogError($"Failed to setup cross mode. {crossMode.Error?.Message}");
                    throw new InvalidOperationException($"Failed to setup cross mode. {crossMode.Error?.Message}");
                }

                m_logger.LogInformation($"Cross mode set to {TradeMode.CrossMargin} for {symbol.Name}");
                m_logger.LogInformation($"Symbol {symbol.Name} setup completed");
            }
        }

        public async Task InitializeAsync(Candle[] candles, Ticker ticker, CancellationToken cancel)
        {
            bool consistent = true;
            QueueInitialized = false;
            foreach (var queue in QuoteQueues.Values)
                queue.Clear();

            foreach (var candle in candles)
            {
                bool candleConsistent = QuoteQueues[candle.TimeFrame].Enqueue(candle.ToQuote());
                if (!candleConsistent)
                    consistent = false;
            }

            QueueInitialized = true;

            await ProcessCandleBuffer();

            Ticker = ticker;
            ConsistentData = consistent;

            await EvaluateSignalsAsync(cancel);
        }

        public async Task ExecuteAsync(bool allowLongPositionOpen, bool allowShortPositionOpen, CancellationToken cancel)
        {
            int jitter = m_random.Next(500, 5500);
            await Task.Delay(jitter, cancel); // random delay to lower probability of hitting rate limits until we have a better solution
            m_logger.LogInformation($"{Name}: {Symbol} Executing strategy. TradingMode: {m_options.Value.TradingMode}");

            var buyOrders = BuyOrders;
            var sellOrders = SellOrders;
            var longPosition = LongPosition;
            var shortPosition = ShortPosition;
            bool hasBuySignal = HasBuySignal;
            bool hasSellSignal = HasSellSignal;
            bool hasBuyExtraSignal = HasBuyExtraSignal;
            bool hasSellExtraSignal = HasSellExtraSignal;
            decimal? dynamicQtyShort = DynamicQtyShort;
            decimal? dynamicQtyLong = DynamicQtyLong;
            var ticker = Ticker;
            var longTakeProfitOrders = LongTakeProfitOrders;
            var shortTakeProfitOrders = ShortTakeProfitOrders;
            decimal? longTakeProfitPrice = LongTakeProfitPrice;
            decimal? shortTakeProfitPrice = ShortTakeProfitPrice;
            DateTime utcNow = DateTime.UtcNow;
            TimeSpan replacementTime = TimeSpan.FromMinutes(4.5);
            decimal? maxShortQty = null;
            decimal? maxLongQty = null;
            decimal? longExposure = null;
            decimal? shortExposure = null;
            var primary = RequiredTimeFrameWindows.First(x => x.Primary).TimeFrame;
            var lastPrimaryQuote = QuoteQueues[primary].GetQuotes().LastOrDefault();
            if(dynamicQtyShort.HasValue)
                maxShortQty = DcaOrdersCount * dynamicQtyShort.Value;
            if (dynamicQtyLong.HasValue)
                maxLongQty = DcaOrdersCount * dynamicQtyLong.Value;
            if (longPosition != null)
                longExposure = longPosition.Quantity * longPosition.AveragePrice;
            if (shortPosition != null)
                shortExposure = shortPosition.Quantity * shortPosition.AveragePrice;
            var wallet = m_walletManager.Contract;
            decimal? walletLongExposure = null;
            decimal? walletShortExposure = null;
            if (wallet.WalletBalance.HasValue && longExposure.HasValue && wallet.WalletBalance.Value > 0)
                walletLongExposure = longExposure / wallet.WalletBalance;
            if (wallet.WalletBalance.HasValue && shortExposure.HasValue && wallet.WalletBalance.Value > 0)
                walletShortExposure = shortExposure / wallet.WalletBalance;

            // log variables above
            m_logger.LogInformation($"{Name}: {Symbol} Buy orders: '{buyOrders.Length}'; Sell orders: '{sellOrders.Length}'; Long position: '{longPosition?.Quantity}'; Short position: '{shortPosition?.Quantity}'; Has buy signal: '{hasBuySignal}'; Has sell signal: '{hasSellSignal}'; Has buy extra signal: '{hasBuyExtraSignal}'; Has sell extra signal: '{hasSellExtraSignal}'. Allow long open: '{allowLongPositionOpen}' Allow short open: '{allowShortPositionOpen}'");

            if (m_options.Value.TradingMode == TradingMode.Readonly)
            {
                m_logger.LogInformation($"{Name}: {Symbol} Finished executing strategy. ReadOnly: {m_options.Value.TradingMode}");
                return;
            }

            if (!hasBuySignal && !hasBuyExtraSignal && buyOrders.Any())
            {
                m_logger.LogInformation($"{Name}: {Symbol} no buy signal. Canceling buy orders.");
                // cancel outstanding buy orders
                foreach (Order buyOrder in buyOrders)
                {
                    bool canceled = await CancelOrderAsync(buyOrder.OrderId, cancel);
                    m_logger.LogInformation($"{Name}: {Symbol} Canceling buy order '{buyOrder.OrderId}' {(canceled ? "succeeded" : "failed")}");
                }
            }

            if (!hasSellSignal && !hasSellExtraSignal && sellOrders.Any())
            {
                m_logger.LogInformation($"{Name}: {Symbol} no sell signal. Canceling sell orders.");
                // cancel outstanding sell orders
                foreach (Order sellOrder in sellOrders)
                {
                    bool canceled = await CancelOrderAsync(sellOrder.OrderId, cancel);
                    m_logger.LogInformation($"{Name}: {Symbol} Canceling sell order '{sellOrder.OrderId}' {(canceled ? "succeeded" : "failed")}");
                }
            }

            bool canOpenLongPosition = (m_options.Value.TradingMode == TradingMode.Normal
                                        || m_options.Value.TradingMode == TradingMode.Dynamic) 
                                       && allowLongPositionOpen;
            bool canOpenShortPosition = (m_options.Value.TradingMode == TradingMode.Normal 
                                         || m_options.Value.TradingMode == TradingMode.Dynamic) 
                                        && allowShortPositionOpen;

            if (ticker != null && lastPrimaryQuote != null)
            {
                if (hasBuySignal
                    && longPosition == null 
                    && !buyOrders.Any() 
                    && NoPositionIncreaseOrderForCandle(lastPrimaryQuote, LastCandleLongOrder)
                    && dynamicQtyLong.HasValue
                    && canOpenLongPosition
                    && LongFundingWithinLimit(ticker))
                {
                    m_logger.LogInformation($"{Name}: {Symbol} trying to open long position");
                    await PlaceLimitBuyOrderAsync(dynamicQtyLong.Value, ticker.BestBidPrice, lastPrimaryQuote.Date, cancel);
                }

                if (hasSellSignal 
                    && shortPosition == null 
                    && !sellOrders.Any() 
                    && NoPositionIncreaseOrderForCandle(lastPrimaryQuote, LastCandleShortOrder)
                    && dynamicQtyShort.HasValue
                    && canOpenShortPosition
                    && ShortFundingWithinLimit(ticker))
                {
                    m_logger.LogInformation($"{Name}: {Symbol} trying to open short position");
                    await PlaceLimitSellOrderAsync(dynamicQtyShort.Value, ticker.BestAskPrice, lastPrimaryQuote.Date, cancel);
                }

                if (hasBuyExtraSignal 
                    && longPosition != null 
                    && maxLongQty.HasValue 
                    && longPosition.Quantity < maxLongQty.Value
                    && walletLongExposure.HasValue && walletLongExposure.Value < m_options.Value.WalletExposureLong
                    && !buyOrders.Any()
                    && dynamicQtyLong.HasValue
                    && NoPositionIncreaseOrderForCandle(lastPrimaryQuote, LastCandleLongOrder))
                {
                    m_logger.LogInformation($"{Name}: {Symbol} trying to add to open long position");
                    await PlaceLimitBuyOrderAsync(dynamicQtyLong.Value, ticker.BestBidPrice, lastPrimaryQuote.Date, cancel);
                }

                if (hasSellExtraSignal 
                    && shortPosition != null 
                    && maxShortQty.HasValue 
                    && shortPosition.Quantity < maxShortQty.Value
                    && walletShortExposure.HasValue && walletShortExposure.Value < m_options.Value.WalletExposureShort
                    && !sellOrders.Any()
                    && dynamicQtyShort.HasValue
                    && NoPositionIncreaseOrderForCandle(lastPrimaryQuote, LastCandleShortOrder))
                {
                    m_logger.LogInformation($"{Name}: {Symbol} trying to add to open short position");
                    await PlaceLimitSellOrderAsync(dynamicQtyShort.Value, ticker.BestAskPrice, lastPrimaryQuote.Date, cancel);
                }
            }

            if (longPosition != null && longTakeProfitPrice.HasValue)
            {
                decimal longTakeProfitQty = longTakeProfitOrders.Length > 0 ? longTakeProfitOrders.Sum(x => x.Quantity) : 0;
                if (longTakeProfitQty != longPosition.Quantity || NextLongProfitReplacement == null || (NextLongProfitReplacement != null && utcNow > NextLongProfitReplacement))
                {
                    foreach (Order longTakeProfitOrder in longTakeProfitOrders)
                    {
                        m_logger.LogInformation($"{Name}: {Symbol} Canceling long take profit order '{longTakeProfitOrder.OrderId}'");
                        await CancelOrderAsync(longTakeProfitOrder.OrderId, cancel);
                    }
                    m_logger.LogInformation($"{Name}: {Symbol} Placing long take profit order for '{longPosition.Quantity}' @ '{longTakeProfitPrice.Value}'");
                    await PlaceLongTakeProfitOrderAsync(longPosition.Quantity, longTakeProfitPrice.Value, cancel);
                    NextLongProfitReplacement = utcNow + replacementTime;
                }
            }

            if (shortPosition != null && shortTakeProfitPrice.HasValue)
            {
                decimal shortTakeProfitQty = shortTakeProfitOrders.Length > 0 ? shortTakeProfitOrders.Sum(x => x.Quantity) : 0;
                if ((shortTakeProfitQty != shortPosition.Quantity) || NextShortProfitReplacement == null || (NextShortProfitReplacement != null && utcNow > NextShortProfitReplacement))
                {
                    foreach (Order shortTakeProfitOrder in shortTakeProfitOrders)
                    {
                        m_logger.LogInformation($"{Name}: {Symbol} Canceling short take profit order '{shortTakeProfitOrder.OrderId}'");
                        await CancelOrderAsync(shortTakeProfitOrder.OrderId, cancel);
                    }
                    m_logger.LogInformation($"{Name}: {Symbol} Placing short take profit order for '{shortPosition.Quantity}' @ '{shortTakeProfitPrice.Value}'");
                    await PlaceShortTakeProfitOrderAsync(shortPosition.Quantity, shortTakeProfitPrice.Value, cancel);
                    NextShortProfitReplacement = utcNow + replacementTime;
                }
            }

            m_logger.LogInformation($"{Name}: {Symbol} Finished executing strategy. TradingMode: {m_options.Value.TradingMode}");
        }

        public async Task AddCandleDataAsync(Candle candle, CancellationToken cancel)
        {
            // we need to first subscribe to candles so we don't miss any
            // we need to put them in a buffer so we can process them in order
            // after initializing the queue we can process the buffer
            // If we get inconsistent data we will reinitialize queue while still receiving candles
            // eventually we will get consistent data and can process the buffer
            // duplicate candles will be ignored by the queue
            m_candleBuffer.Writer.TryWrite(candle);
            if (QueueInitialized)
            {
                await ProcessCandleBuffer();
                await EvaluateSignalsAsync(cancel);
            }
        }

        public async Task UpdatePriceDataSync(Ticker ticker, CancellationToken cancel)
        {
            Ticker = ticker;
            LastTickerUpdate = DateTime.UtcNow;
            if (QueueInitialized)
            {
                await EvaluateSignalsAsync(cancel);
            }
        }

        protected virtual async Task EvaluateSignalsAsync(CancellationToken cancel)
        {
            HasBuySignal = false;
            HasSellSignal = false;
            HasSellExtraSignal = false;
            HasBuyExtraSignal = false;
            Indicators = Array.Empty<StrategyIndicator>();
            if (!ConsistentData)
                return;
            var ticker = Ticker;
            if (ticker == null)
                return;
            RecommendedMinBalance = SymbolInfo.CalculateMinBalance(ticker.BestAskPrice, Math.Min(WalletExposureLong, WalletExposureShort), DcaOrdersCount);

            if (!DynamicQtyShort.HasValue || !IsInTrade)
                DynamicQtyShort = CalculateDynamicQty(ticker.BestAskPrice, WalletExposureShort);
            if (!DynamicQtyLong.HasValue || !IsInTrade)
                DynamicQtyLong = CalculateDynamicQty(ticker.BestBidPrice, WalletExposureLong);

            var signalEvaluation = await EvaluateSignalsInnerAsync(cancel);
            HasBuySignal = signalEvaluation.BuySignal;
            HasSellSignal = signalEvaluation.SellSignal;
            HasBuyExtraSignal = signalEvaluation.HasBuyExtraSignal;
            HasSellExtraSignal = signalEvaluation.HasSellExtraSignal;
            var indicators = new List<StrategyIndicator>
            {
                new StrategyIndicator(nameof(IndicatorType.Buy), HasBuySignal),
                new StrategyIndicator(nameof(IndicatorType.BuyExtra), HasBuyExtraSignal),
                new StrategyIndicator(nameof(IndicatorType.Sell), HasSellSignal),
                new StrategyIndicator(nameof(IndicatorType.SellExtra), HasSellExtraSignal)
            };
            var quotes5Min = QuoteQueues[TimeFrame.FiveMinutes].GetQuotes();
            if(quotes5Min.Length < 1)
                return;
            var quotes1Min = QuoteQueues[TimeFrame.OneMinute].GetQuotes();
            if(quotes1Min.Length < 1)
                return;
            var spread5Min = TradeSignalHelpers.Get5MinSpread(quotes1Min);
            var longPosition = LongPosition;
            var shortPosition = ShortPosition;
            decimal? shortTakeProfit = null;
            if(shortPosition != null)
                shortTakeProfit = TradingHelpers.CalculateShortTakeProfit(shortPosition.AveragePrice, SymbolInfo, quotes5Min, spread5Min, ticker);
            ShortTakeProfitPrice = shortTakeProfit;
            if(shortTakeProfit.HasValue)
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.ShortTakeProfit), shortTakeProfit.Value));

            decimal? longTakeProfit = null;
            if(longPosition != null)
                longTakeProfit = TradingHelpers.CalculateLongTakeProfit(longPosition.AveragePrice, SymbolInfo, quotes5Min, spread5Min, ticker);
            LongTakeProfitPrice = longTakeProfit;
            if(longTakeProfit.HasValue)
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.LongTakeProfit), longTakeProfit.Value));
            indicators.AddRange(signalEvaluation.Indicators);
            Indicators = indicators.ToArray();
        }

        protected abstract Task<SignalEvaluation> EvaluateSignalsInnerAsync(CancellationToken cancel);

        private Task ProcessCandleBuffer()
        {
            while (m_candleBuffer.Reader.TryRead(out var bufferedCandle))
            {
                bool consistent = QuoteQueues[bufferedCandle.TimeFrame].Enqueue(bufferedCandle.ToQuote());
                if (!consistent)
                    ConsistentData = false;
            }
            LastCandleUpdate = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        private async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancel)
        {
            var cancelOrder = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits
                .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading
                .CancelOrderAsync(Category.Linear, Symbol, orderId, null, null, cancel));
            if(cancelOrder.GetResultOrError(out _, out var error))
                return true;
            m_logger.LogError($"{Name}: {Symbol} Error canceling order: {error}");
            return false;
        }

        private async Task PlaceLimitBuyOrderAsync(decimal qty, decimal bidPrice, DateTime candleTime, CancellationToken cancel)
        {
            for (int attempt = 0; attempt < m_options.Value.PlaceOrderAttempts; attempt++)
            {
                m_logger.LogInformation($"{Name}: {Symbol} Placing limit buy order for '{qty}' @ '{bidPrice}'");
                var buyOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                        category: Category.Linear,
                        symbol: Symbol,
                        side: Bybit.Net.Enums.OrderSide.Buy,
                        type: NewOrderType.Limit,
                        quantity: qty,
                        price: bidPrice,
                        positionIdx: PositionIdx.BuyHedgeMode,
                        reduceOnly: false,
                        timeInForce: TimeInForce.PostOnly,
                        ct: cancel));
                if (!buyOrderRes.GetResultOrError(out var buyOrder, out _)) 
                    continue;
                var orderStatusRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrder>
                    .RetryTooManyVisitsBybitResponse
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                        category: Category.Linear,
                        symbol: Symbol,
                        orderId: buyOrder.OrderId,
                        ct: cancel));
                if (orderStatusRes.GetResultOrError(out var orderStatus, out _))
                {
                    var order = orderStatus.List
                        .FirstOrDefault(x => string.Equals(x.OrderId, buyOrder.OrderId, StringComparison.Ordinal));
                    if (order != null && order.Status == OrderStatus.Cancelled)
                    {
                        m_logger.LogInformation($"{Name}: {Symbol} Buy order was cancelled. Adjusting price.");
                        var orderBook = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderbook>
                            .RetryTooManyVisits
                            .ExecuteAsync(async () =>
                                await m_bybitRestClient.V5Api.ExchangeData.GetOrderbookAsync(Category.Linear, Symbol,
                                    limit: 1, cancel));
                        if (orderBook.GetResultOrError(out var orderBookData, out _))
                        {
                            var bestBid = orderBookData.Bids.FirstOrDefault();
                            if (bestBid != null)
                            {
                                bidPrice = bestBid.Price;
                            }
                        }
                        continue;
                    }
                    m_logger.LogInformation($"{Name}: {Symbol} Buy order placed for '{qty}' @ '{bidPrice}'");
                    LastCandleLongOrder = candleTime;
                    return;
                }

                m_logger.LogWarning($"{Name}: {Symbol} Error getting order status: {orderStatusRes.Error}");
                return;
            }
            
            m_logger.LogInformation($"{Name}: {Symbol} could not place buy order.");
        }

        private async Task PlaceLimitSellOrderAsync(decimal qty, decimal askPrice, DateTime candleTime, CancellationToken cancel)
        {
            for (int attempt = 0; attempt < m_options.Value.PlaceOrderAttempts; attempt++)
            {
                m_logger.LogInformation($"{Name}: {Symbol} Placing limit sell order for '{qty}' @ '{askPrice}' attempt: {attempt}");
                var sellOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                        category: Category.Linear,
                        symbol: Symbol,
                        side: Bybit.Net.Enums.OrderSide.Sell,
                        type: NewOrderType.Limit,
                        quantity: qty,
                        price: askPrice,
                        positionIdx: PositionIdx.SellHedgeMode,
                        reduceOnly: false,
                        timeInForce: TimeInForce.PostOnly,
                        ct: cancel));
                if (!sellOrderRes.GetResultOrError(out var sellOrder, out _))
                    continue;
                var orderStatusRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrder>.RetryTooManyVisitsBybitResponse
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                        category: Category.Linear,
                        symbol: Symbol,
                        orderId: sellOrder.OrderId,
                        ct: cancel));
                if (orderStatusRes.GetResultOrError(out var orderStatus, out _))
                {
                    var order = orderStatus.List
                        .FirstOrDefault(x => string.Equals(x.OrderId, sellOrder.OrderId, StringComparison.Ordinal));
                    if (order != null && order.Status == OrderStatus.Cancelled)
                    {
                        m_logger.LogInformation($"{Name}: {Symbol} Sell order was cancelled. Adjusting price.");
                        var orderBook = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderbook>.RetryTooManyVisits
                            .ExecuteAsync(async () =>
                                await m_bybitRestClient.V5Api.ExchangeData.GetOrderbookAsync(Category.Linear, Symbol,
                                    limit: 1, cancel));
                        if (orderBook.GetResultOrError(out var orderBookData, out _))
                        {
                            var bestAsk = orderBookData.Asks.FirstOrDefault();
                            if (bestAsk != null)
                            {
                                askPrice = bestAsk.Price;
                            }
                        }
                        continue;
                    }
                    m_logger.LogInformation($"{Name}: {Symbol} Sell order placed for '{qty}' @ '{askPrice}'");
                    LastCandleShortOrder = candleTime;
                    return;
                }

                m_logger.LogWarning($"{Name}: {Symbol} Error getting order status: {orderStatusRes.Error}");
                return;
            }
        }

        private async Task PlaceLongTakeProfitOrderAsync(decimal qty, decimal price, CancellationToken cancel)
        {
            var sellOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits.ExecuteAsync(async () =>
                await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                    category: Category.Linear,
                    symbol: Symbol,
                    side: Bybit.Net.Enums.OrderSide.Sell,
                    type: NewOrderType.Limit,
                    quantity: qty,
                    price: price,
                    positionIdx: PositionIdx.BuyHedgeMode,
                    reduceOnly: true,
                    timeInForce: TimeInForce.PostOnly,
                    ct: cancel));
            if (sellOrderRes.GetResultOrError(out _, out var error))
                return;
            m_logger.LogWarning($"{Name}: {Symbol} Failed to place long take profit order: {error}");
        }

        private async Task PlaceShortTakeProfitOrderAsync(decimal qty, decimal price, CancellationToken cancel)
        {
            var buyOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits.ExecuteAsync(async () =>
                await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                    category: Category.Linear,
                    symbol: Symbol,
                    side: Bybit.Net.Enums.OrderSide.Buy,
                    type: NewOrderType.Limit,
                    quantity: qty,
                    price: price,
                    positionIdx: PositionIdx.SellHedgeMode,
                    reduceOnly: true,
                    timeInForce: TimeInForce.PostOnly,
                    ct: cancel));
            if (buyOrderRes.GetResultOrError(out _, out var error))
                return;
            m_logger.LogWarning($"{Name}: {Symbol} Failed to place short take profit order: {error}");
        }

        private bool NoPositionIncreaseOrderForCandle(Quote candle, DateTime? lastTrade)
        {
            if (lastTrade == null)
                return true;
            return lastTrade.Value < candle.Date;
        }

        private bool ShortFundingWithinLimit(Ticker ticker)
        {
            if (!ticker.FundingRate.HasValue)
                return true;
            return ticker.FundingRate.Value >= -m_options.Value.MaxAbsFundingRate;
        }

        private bool LongFundingWithinLimit(Ticker ticker)
        {
            if (!ticker.FundingRate.HasValue)
                return true;
            return ticker.FundingRate.Value <= m_options.Value.MaxAbsFundingRate;
        }

        private decimal? CalculateDynamicQty(decimal price, decimal walletExposure)
        {
            var dynamicQty = SymbolInfo.CalculateQuantity(m_walletManager, price, walletExposure, DcaOrdersCount);
            if (!dynamicQty.HasValue && ForceMinQty) // we could not calculate a quantity so we will use the minimum
                dynamicQty = SymbolInfo.MinOrderQty;

            if (dynamicQty.HasValue && dynamicQty.Value < SymbolInfo.MinOrderQty)
                dynamicQty = ForceMinQty ? SymbolInfo.MinOrderQty : null;

            bool isInTrade = IsInTrade;
            if (!dynamicQty.HasValue && isInTrade)
            {
                // we are in a trade and we could not calculate a quantity so we will use the minimum
                dynamicQty = SymbolInfo.MinOrderQty;
            }

            return dynamicQty;
        }

        protected readonly record struct SignalEvaluation(bool BuySignal,
            bool SellSignal,
            bool HasBuyExtraSignal,
            bool HasSellExtraSignal,
            StrategyIndicator[] Indicators);
    }
}