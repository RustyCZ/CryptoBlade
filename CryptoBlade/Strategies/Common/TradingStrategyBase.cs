using System.Threading.Channels;
using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace CryptoBlade.Strategies.Common
{
    public abstract class TradingStrategyBase : ITradingStrategy
    {
        private readonly IOptions<TradingStrategyBaseOptions> m_options;
        private readonly Channel<Candle> m_candleBuffer;
        private const int c_defaultCandleBufferSize = 1000;
        private readonly IWalletManager m_walletManager;
        private readonly ICbFuturesRestClient m_cbFuturesRestClient;
        private readonly ILogger m_logger;
        private readonly Random m_random = new Random();

        protected TradingStrategyBase(IOptions<TradingStrategyBaseOptions> options,
            string symbol, 
            TimeFrameWindow[] requiredTimeFrames, 
            IWalletManager walletManager,
            ICbFuturesRestClient cbFuturesRestClient)
        {
            m_logger = ApplicationLogging.CreateLogger(GetType().FullName ?? nameof(TradingStrategyBase));
            RequiredTimeFrameWindows = requiredTimeFrames;
            m_walletManager = walletManager;
            m_cbFuturesRestClient = cbFuturesRestClient;
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
        public decimal? UnrealizedLongPnlPercent { get; protected set; }
        public decimal? UnrealizedShortPnlPercent { get; protected set; }
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
            UnrealizedLongPnlPercent = null;
            UnrealizedShortPnlPercent = null;
            var balance = m_walletManager.Contract;
            if (longPosition != null && Ticker != null && balance.WalletBalance.HasValue)
            {
                var longPositionValue = (Ticker.LastPrice - longPosition.AveragePrice) * longPosition.Quantity;
                UnrealizedLongPnlPercent = longPositionValue / balance.WalletBalance.Value;
            }

            if (shortPosition != null && Ticker != null && balance.WalletBalance.HasValue)
            {
                var shortPositionValue = (shortPosition.AveragePrice - Ticker.LastPrice) * shortPosition.Quantity;
                UnrealizedShortPnlPercent = shortPositionValue / balance.WalletBalance.Value;
            }
                
            m_logger.LogDebug(
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
            if (m_options.Value.TradingMode != TradingMode.Readonly)
            {
                m_logger.LogInformation($"Setting up trading configuration for symbol {symbol.Name}");
                bool leverageOk = await m_cbFuturesRestClient.SetLeverageAsync(symbol, cancel);
                if (!leverageOk)
                    throw new InvalidOperationException("Failed to setup leverage.");

                m_logger.LogInformation($"Leverage set to {symbol.MaxLeverage} for {symbol.Name}");

                bool modeOk = await m_cbFuturesRestClient.SwitchPositionModeAsync(PositionMode.Hedge, symbol.Name, cancel);
                if (!modeOk)
                    throw new InvalidOperationException("Failed to setup position mode.");

                m_logger.LogInformation($"Position mode set to {PositionMode.Hedge} for {symbol.Name}");

                
                bool crossModeOk = await m_cbFuturesRestClient.SwitchCrossIsolatedMarginAsync(symbol, TradeMode.CrossMargin, cancel);
                if (!crossModeOk)
                    throw new InvalidOperationException("Failed to setup cross mode.");

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
            bool isLive = m_options.Value.TradingMode == TradingMode.Normal 
                          || m_options.Value.TradingMode == TradingMode.Dynamic
                          || m_options.Value.TradingMode == TradingMode.Readonly;
            if (isLive)
            {
                int jitter = m_random.Next(500, 5500);
                await Task.Delay(jitter, cancel); // random delay to lower probability of hitting rate limits until we have a better solution
            }
                
            m_logger.LogDebug($"{Name}: {Symbol} Executing strategy. TradingMode: {m_options.Value.TradingMode}");

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
            DateTime utcNow = ticker?.Timestamp ?? DateTime.UtcNow;
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
            m_logger.LogDebug($"{Name}: {Symbol} Buy orders: '{buyOrders.Length}'; Sell orders: '{sellOrders.Length}'; Long position: '{longPosition?.Quantity}'; Short position: '{shortPosition?.Quantity}'; Has buy signal: '{hasBuySignal}'; Has sell signal: '{hasSellSignal}'; Has buy extra signal: '{hasBuyExtraSignal}'; Has sell extra signal: '{hasSellExtraSignal}'. Allow long open: '{allowLongPositionOpen}' Allow short open: '{allowShortPositionOpen}'");

            if (m_options.Value.TradingMode == TradingMode.Readonly)
            {
                m_logger.LogDebug($"{Name}: {Symbol} Finished executing strategy. ReadOnly: {m_options.Value.TradingMode}");
                return;
            }

            if (!hasBuySignal && !hasBuyExtraSignal && buyOrders.Any())
            {
                m_logger.LogDebug($"{Name}: {Symbol} no buy signal. Canceling buy orders.");
                // cancel outstanding buy orders
                foreach (Order buyOrder in buyOrders)
                {
                    bool canceled = await CancelOrderAsync(buyOrder.OrderId, cancel);
                    m_logger.LogDebug($"{Name}: {Symbol} Canceling buy order '{buyOrder.OrderId}' {(canceled ? "succeeded" : "failed")}");
                }
            }

            if (!hasSellSignal && !hasSellExtraSignal && sellOrders.Any())
            {
                m_logger.LogDebug($"{Name}: {Symbol} no sell signal. Canceling sell orders.");
                // cancel outstanding sell orders
                foreach (Order sellOrder in sellOrders)
                {
                    bool canceled = await CancelOrderAsync(sellOrder.OrderId, cancel);
                    m_logger.LogDebug($"{Name}: {Symbol} Canceling sell order '{sellOrder.OrderId}' {(canceled ? "succeeded" : "failed")}");
                }
            }

            bool canOpenLongPosition = (m_options.Value.TradingMode == TradingMode.Normal
                                        || m_options.Value.TradingMode == TradingMode.Dynamic
                                        || m_options.Value.TradingMode == TradingMode.DynamicBackTest) 
                                       && allowLongPositionOpen;
            bool canOpenShortPosition = (m_options.Value.TradingMode == TradingMode.Normal 
                                         || m_options.Value.TradingMode == TradingMode.Dynamic
                                         || m_options.Value.TradingMode == TradingMode.DynamicBackTest) 
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
                    m_logger.LogDebug($"{Name}: {Symbol} trying to open long position");
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
                    m_logger.LogDebug($"{Name}: {Symbol} trying to open short position");
                    await PlaceLimitSellOrderAsync(dynamicQtyShort.Value, ticker.BestAskPrice, lastPrimaryQuote.Date, cancel);
                }

                if (hasBuyExtraSignal 
                    && longPosition != null 
                    && maxLongQty.HasValue 
                    && longPosition.Quantity < maxLongQty.Value
                    && walletLongExposure.HasValue && walletLongExposure.Value < m_options.Value.WalletExposureLong
                    && !buyOrders.Any()
                    && dynamicQtyLong.HasValue
                    && NoPositionIncreaseOrderForCandle(lastPrimaryQuote, LastCandleLongOrder)
                    && LongFundingWithinLimit(ticker))
                {
                    m_logger.LogDebug($"{Name}: {Symbol} trying to add to open long position");
                    await PlaceLimitBuyOrderAsync(dynamicQtyLong.Value, ticker.BestBidPrice, lastPrimaryQuote.Date, cancel);
                }

                if (hasSellExtraSignal 
                    && shortPosition != null 
                    && maxShortQty.HasValue 
                    && shortPosition.Quantity < maxShortQty.Value
                    && walletShortExposure.HasValue && walletShortExposure.Value < m_options.Value.WalletExposureShort
                    && !sellOrders.Any()
                    && dynamicQtyShort.HasValue
                    && NoPositionIncreaseOrderForCandle(lastPrimaryQuote, LastCandleShortOrder)
                    && ShortFundingWithinLimit(ticker))
                {
                    m_logger.LogDebug($"{Name}: {Symbol} trying to add to open short position");
                    await PlaceLimitSellOrderAsync(dynamicQtyShort.Value, ticker.BestAskPrice, lastPrimaryQuote.Date, cancel);
                }
            }

            bool hasPlacedOrder = lastPrimaryQuote != null 
                                  && (LastCandleLongOrder.HasValue && LastCandleLongOrder.Value == lastPrimaryQuote.Date 
                                      || LastCandleShortOrder.HasValue && LastCandleShortOrder.Value == lastPrimaryQuote.Date);
            // do not place take profit orders if we have placed an order for the current candle
            // quantity would not be valid
            if (longPosition != null && longTakeProfitPrice.HasValue && !hasPlacedOrder)
            {
                decimal longTakeProfitQty = longTakeProfitOrders.Length > 0 ? longTakeProfitOrders.Sum(x => x.Quantity) : 0;
                if (longTakeProfitQty != longPosition.Quantity || NextLongProfitReplacement == null || (NextLongProfitReplacement != null && utcNow > NextLongProfitReplacement))
                {
                    foreach (Order longTakeProfitOrder in longTakeProfitOrders)
                    {
                        m_logger.LogDebug($"{Name}: {Symbol} Canceling long take profit order '{longTakeProfitOrder.OrderId}'");
                        await CancelOrderAsync(longTakeProfitOrder.OrderId, cancel);
                    }
                    m_logger.LogDebug($"{Name}: {Symbol} Placing long take profit order for '{longPosition.Quantity}' @ '{longTakeProfitPrice.Value}'");
                    await PlaceLongTakeProfitOrderAsync(longPosition.Quantity, longTakeProfitPrice.Value, false, cancel);
                    NextLongProfitReplacement = utcNow + replacementTime;
                }
            }

            if (shortPosition != null && shortTakeProfitPrice.HasValue && !hasPlacedOrder)
            {
                decimal shortTakeProfitQty = shortTakeProfitOrders.Length > 0 ? shortTakeProfitOrders.Sum(x => x.Quantity) : 0;
                if ((shortTakeProfitQty != shortPosition.Quantity) || NextShortProfitReplacement == null || (NextShortProfitReplacement != null && utcNow > NextShortProfitReplacement))
                {
                    foreach (Order shortTakeProfitOrder in shortTakeProfitOrders)
                    {
                        m_logger.LogDebug($"{Name}: {Symbol} Canceling short take profit order '{shortTakeProfitOrder.OrderId}'");
                        await CancelOrderAsync(shortTakeProfitOrder.OrderId, cancel);
                    }
                    m_logger.LogDebug($"{Name}: {Symbol} Placing short take profit order for '{shortPosition.Quantity}' @ '{shortTakeProfitPrice.Value}'");
                    await PlaceShortTakeProfitOrderAsync(shortPosition.Quantity, shortTakeProfitPrice.Value, false, cancel);
                    NextShortProfitReplacement = utcNow + replacementTime;
                }
            }

            m_logger.LogDebug($"{Name}: {Symbol} Finished executing strategy. TradingMode: {m_options.Value.TradingMode}");
        }

        public async Task ExecuteUnstuckAsync(bool unstuckLong, bool unstuckShort, bool forceUnstuckLong, bool forceUnstuckShort, CancellationToken cancel)
        {
            if (unstuckLong)
            {
                if (HasSellSignal || HasSellExtraSignal || forceUnstuckLong)
                { 
                    m_logger.LogDebug($"{Name}: {Symbol} Unstuck long position");
                    await ExecuteLongUnstuckAsync(forceUnstuckLong, cancel);
                }
            }

            if (unstuckShort)
            {
                if (HasBuySignal || HasBuyExtraSignal || forceUnstuckShort)
                {
                    m_logger.LogDebug($"{Name}: {Symbol} Unstuck short position");
                    await ExecuteShortUnstuckAsync(forceUnstuckShort, cancel);
                }
            }
        }

        private async Task ExecuteLongUnstuckAsync(bool force, CancellationToken cancel)
        {
            var longPosition = LongPosition;
            if (longPosition == null)
                return;
            var longTakeProfitOrders = LongTakeProfitOrders;
            var ticker = Ticker;
            if(ticker == null)
                return;
            var dynamicQtyLong = DynamicQtyLong;
            if(dynamicQtyLong == null)
                return;

            foreach (Order longTakeProfitOrder in longTakeProfitOrders)
            {
                m_logger.LogDebug($"{Name}: {Symbol} Canceling long take profit order '{longTakeProfitOrder.OrderId}'");
                await CancelOrderAsync(longTakeProfitOrder.OrderId, cancel);
            }
            
            await PlaceLongTakeProfitOrderAsync(dynamicQtyLong.Value, ticker.BestAskPrice, force, cancel);
        }

        private async Task ExecuteShortUnstuckAsync(bool force, CancellationToken cancel)
        {
            var shortPosition = ShortPosition;
            if (shortPosition == null)
                return;
            var shortTakeProfitOrders = ShortTakeProfitOrders;
            var ticker = Ticker;
            if (ticker == null)
                return;
            var dynamicQtyShort = DynamicQtyShort;
            if (dynamicQtyShort == null)
                return;

            foreach (Order shortTakeProfitOrder in shortTakeProfitOrders)
            {
                m_logger.LogDebug($"{Name}: {Symbol} Canceling short take profit order '{shortTakeProfitOrder.OrderId}'");
                await CancelOrderAsync(shortTakeProfitOrder.OrderId, cancel);
            }

            await PlaceShortTakeProfitOrderAsync(dynamicQtyShort.Value, ticker.BestBidPrice, force, cancel);
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
            LastTickerUpdate = ticker.Timestamp;
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
            
            var minExposure = Math.Min(WalletExposureLong, WalletExposureShort);
            if (minExposure == 0)
                minExposure = Math.Max(WalletExposureLong, WalletExposureShort);

            RecommendedMinBalance = SymbolInfo.CalculateMinBalance(ticker.BestAskPrice, minExposure, DcaOrdersCount);

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
                shortTakeProfit = TradingHelpers.CalculateShortTakeProfit(
                    shortPosition, 
                    SymbolInfo, 
                    quotes5Min, 
                    spread5Min, 
                    ticker,
                    m_options.Value.FeeRate,
                    m_options.Value.MinProfitRate);
            ShortTakeProfitPrice = shortTakeProfit;
            if(shortTakeProfit.HasValue)
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.ShortTakeProfit), shortTakeProfit.Value));

            decimal? longTakeProfit = null;
            if(longPosition != null)
                longTakeProfit = TradingHelpers.CalculateLongTakeProfit(
                    longPosition, 
                    SymbolInfo, 
                    quotes5Min, 
                    spread5Min, 
                    ticker,
                    m_options.Value.FeeRate,
                    m_options.Value.MinProfitRate);
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
                if(bufferedCandle.TimeFrame == TimeFrame.OneMinute)
                    LastCandleUpdate = bufferedCandle.StartTime + bufferedCandle.TimeFrame.ToTimeSpan();
            }

            return Task.CompletedTask;
        }

        private async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancel)
        {
            bool res = await m_cbFuturesRestClient.CancelOrderAsync(Symbol, orderId, cancel);
            return res;
        }

        private async Task PlaceLimitBuyOrderAsync(decimal qty, decimal bidPrice, DateTime candleTime, CancellationToken cancel)
        {
            bool placed = await m_cbFuturesRestClient.PlaceLimitBuyOrderAsync(Symbol, qty, bidPrice, cancel);
            if(placed)
                LastCandleLongOrder = candleTime;
        }

        private async Task PlaceLimitSellOrderAsync(decimal qty, decimal askPrice, DateTime candleTime, CancellationToken cancel)
        {
            bool placed = await m_cbFuturesRestClient.PlaceLimitSellOrderAsync(Symbol, qty, askPrice, cancel);
            if(placed)
                LastCandleShortOrder = candleTime;
        }

        private async Task PlaceLongTakeProfitOrderAsync(decimal qty, decimal price, bool force, CancellationToken cancel)
        {
            await m_cbFuturesRestClient.PlaceLongTakeProfitOrderAsync(Symbol, qty, price, force, cancel);
        }

        private async Task PlaceShortTakeProfitOrderAsync(decimal qty, decimal price, bool force, CancellationToken cancel)
        {
            await m_cbFuturesRestClient.PlaceShortTakeProfitOrderAsync(Symbol, qty, price, force, cancel);
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