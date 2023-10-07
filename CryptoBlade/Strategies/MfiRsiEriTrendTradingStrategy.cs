using Bybit.Net.Interfaces.Clients;
using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace CryptoBlade.Strategies
{
    public class MfiRsiEriTrendTradingStrategyOptions : TradingStrategyBaseOptions
    {
        public decimal MinimumVolume { get; set; }

        public decimal MinimumPriceDistance { get; set; }

        public decimal MinReentryPositionDistanceLong { get; set; }

        public decimal MinReentryPositionDistanceShort { get; set; }

        public int MfiRsiLookbackPeriod { get; set; } = 100;

        public bool UseEriOnly { get; set; }
    }

    public class MfiRsiEriTrendTradingStrategy : TradingStrategyBase
    {
        private readonly IOptions<MfiRsiEriTrendTradingStrategyOptions> m_options;

        public MfiRsiEriTrendTradingStrategy(IOptions<MfiRsiEriTrendTradingStrategyOptions> options, 
            string symbol, IWalletManager walletManager, ICbFuturesRestClient restClient) 
            : base(options, symbol, GetRequiredTimeFrames(options.Value.MfiRsiLookbackPeriod), walletManager, restClient)
        {
            m_options = options;
        }

        private static TimeFrameWindow[] GetRequiredTimeFrames(int mfiLookbackPeriod)
        {
            const int minLength = 15;
            int requiredLength = mfiLookbackPeriod + minLength;
            return new[]
            {
                new TimeFrameWindow(TimeFrame.OneMinute, requiredLength, true),
                new TimeFrameWindow(TimeFrame.FiveMinutes, minLength, false),
            };
        }

        public override string Name
        {
            get { return "MfiRsiEriTrend"; }
        }

        protected override decimal WalletExposureLong
        {
            get { return m_options.Value.WalletExposureLong; }
        }

        protected override decimal WalletExposureShort
        {
            get { return m_options.Value.WalletExposureShort; }
        }

        protected override int DcaOrdersCount
        {
            get { return m_options.Value.DcaOrdersCount; }
        }

        protected override bool ForceMinQty
        {
            get { return m_options.Value.ForceMinQty; }
        }

        protected override Task<SignalEvaluation> EvaluateSignalsInnerAsync(CancellationToken cancel)
        {
            var quotes = QuoteQueues[TimeFrame.OneMinute].GetQuotes();
            List<StrategyIndicator> indicators = new();
            var lastQuote = quotes.LastOrDefault();
            bool hasBuySignal = false;
            bool hasSellSignal = false;
            bool hasBuyExtraSignal = false;
            bool hasSellExtraSignal = false;
            if (lastQuote != null)
            {
                var sma = quotes.GetSma(14);
                var lastSma = sma.LastOrDefault();
                var spread5Min = TradeSignalHelpers.Get5MinSpread(quotes);
                var mfiTrend = TradeSignalHelpers.GetMfiTrend(quotes, m_options.Value.MfiRsiLookbackPeriod);
                var volume = TradeSignalHelpers.VolumeInQuoteCurrency(lastQuote);
                var eriTrend = TradeSignalHelpers.GetModifiedEriTrend(quotes);
                var movingAverageTrendPct = TradeSignalHelpers.GetTrendPercent(lastSma, lastQuote);
                var movingAverageTrend = TradeSignalHelpers.GetTrend(movingAverageTrendPct);
                var ma6High = quotes.Use(CandlePart.High).GetSma(6);
                var ma6Low = quotes.Use(CandlePart.Low).GetSma(6);

                var ma6HighLast = ma6High.LastOrDefault();
                var ma6LowLast = ma6Low.LastOrDefault();

                bool hasAllRequiredMa = ma6HighLast != null
                                        && ma6HighLast.Sma.HasValue
                                        && ma6LowLast != null
                                        && ma6LowLast.Sma.HasValue;

                var ticker = Ticker;

                bool hasMinSpread = spread5Min > m_options.Value.MinimumPriceDistance;
                bool hasMinVolume = volume >= m_options.Value.MinimumVolume;
                bool shouldAddToShort = false;
                bool shouldAddToLong = false;
                bool hasMinShortPriceDistance = false;
                bool hasMinLongPriceDistance = false;
                bool canBeTraded = (lastQuote.Date - SymbolInfo.LaunchTime).TotalDays > m_options.Value.InitialUntradableDays;
                if (ticker != null 
                    && ma6HighLast != null && ma6HighLast.Sma.HasValue
                    && ma6LowLast != null && ma6LowLast.Sma.HasValue)
                {
                    shouldAddToShort =
                        TradeSignalHelpers.ShortCounterTradeCondition(ticker.BestAskPrice,
                            (decimal)ma6HighLast.Sma.Value);
                    shouldAddToLong =
                        TradeSignalHelpers.LongCounterTradeCondition(ticker.BestBidPrice,
                            (decimal)ma6LowLast.Sma.Value);
                }

                Position? longPosition = LongPosition;
                Position? shortPosition = ShortPosition;

                if (ticker != null && longPosition != null)
                {
                    var rebuyPrice = longPosition.AveragePrice * (1.0m - m_options.Value.MinReentryPositionDistanceLong);
                    if (ticker.BestBidPrice < rebuyPrice)
                        hasMinLongPriceDistance = hasBuySignal;
                }

                if (ticker != null && shortPosition != null)
                {
                    var resellPrice = shortPosition.AveragePrice * (1.0m + m_options.Value.MinReentryPositionDistanceShort);
                    if (ticker.BestAskPrice > resellPrice)
                        hasMinShortPriceDistance = hasSellSignal;
                }

                Trend trend = Trend.Neutral;
                if(m_options.Value.UseEriOnly) 
                    trend = eriTrend;
                else if (eriTrend == Trend.Long || movingAverageTrend == Trend.Long)
                    trend = Trend.Long;
                else if (eriTrend == Trend.Short || movingAverageTrend == Trend.Short)
                    trend = Trend.Short;

                hasBuySignal = hasMinVolume
                               && hasAllRequiredMa
                               && mfiTrend == Trend.Long
                               && trend == Trend.Long
                               && hasMinSpread
                               && canBeTraded;

                hasSellSignal = hasMinVolume
                                && hasAllRequiredMa
                                && mfiTrend == Trend.Short
                                && trend == Trend.Short
                                && hasMinSpread
                                && canBeTraded;

                hasBuyExtraSignal = hasMinVolume
                                    && shouldAddToLong
                                    && hasAllRequiredMa
                                    && mfiTrend == Trend.Long
                                    && trend == Trend.Long
                                    && hasMinSpread
                                    && longPosition != null
                                    && ticker != null
                                    && ticker.BestBidPrice < longPosition.AveragePrice
                                    && hasMinLongPriceDistance
                                    && canBeTraded;

                hasSellExtraSignal = hasMinVolume
                                     && shouldAddToShort
                                     && hasAllRequiredMa
                                     && mfiTrend == Trend.Short
                                     && trend == Trend.Short
                                     && hasMinSpread
                                     && shortPosition != null
                                     && ticker != null
                                     && ticker.BestAskPrice > shortPosition.AveragePrice
                                     && hasMinShortPriceDistance
                                     && canBeTraded;

                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Volume1Min), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.MainTimeFrameVolume), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Spread5Min), spread5Min));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.EriTrend), eriTrend));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.MfiTrend), mfiTrend));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Trend), movingAverageTrend));

                if (hasAllRequiredMa)
                {
                    indicators.Add(new StrategyIndicator(nameof(IndicatorType.Ma6High), ma6HighLast!.Sma!.Value));
                    indicators.Add(new StrategyIndicator(nameof(IndicatorType.Ma6Low), ma6LowLast!.Sma!.Value));
                }
            }

            return Task.FromResult(new SignalEvaluation(hasBuySignal, hasSellSignal, hasBuyExtraSignal, hasSellExtraSignal,
                indicators.ToArray()));
        }
    }
}