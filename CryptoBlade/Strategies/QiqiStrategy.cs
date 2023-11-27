using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace CryptoBlade.Strategies
{
    public class QiqiStrategyOptions : RecursiveStrategyBaseOptions
    {
        public double RsiTakeProfitLong { get; set; } = 70;
        public double RsiTakeProfitShort { get; set; } = 30;
        public double QflBellowPercentEnterLong { get; set; } = 1.1;
        public double QflAbovePercentEnterShort { get; set; } = 1.1;
        public TimeSpan MaxTimeStuck { get; set; } = TimeSpan.FromDays(120);
        public double TakeProfitPercentLong { get; set; } = 0.5;
        public double TakeProfitPercentShort { get; set; } = 0.5;
    }

    public class QiqiStrategy : RecursiveStrategyBase
    {
        private readonly IOptions<QiqiStrategyOptions> m_options;
        private DateTime m_lastDayAtr;
        private ReentryMultiplier m_lastDailyMultiplier;
        private DateTime m_lastHourlyRsiTime;
        private RsiResult[] m_lastRsi;
        private DateTime m_lastQflBasesTime;
        private double?[] m_qflLongBases;
        private double?[] m_qflShortBases;

        public QiqiStrategy(IOptions<QiqiStrategyOptions> options,
            string symbol,
            IWalletManager walletManager,
            ICbFuturesRestClient cbFuturesRestClient) : base(options, symbol, GetRequiredTimeFrames(options), walletManager,
            cbFuturesRestClient)
        {
            m_lastDailyMultiplier = new ReentryMultiplier(1.0, 1.0);
            m_lastDayAtr = DateTime.MinValue;
            m_options = options;
            m_lastHourlyRsiTime = DateTime.MinValue;
            m_lastRsi = Array.Empty<RsiResult>();
            m_lastQflBasesTime = DateTime.MinValue;
            m_qflLongBases = Array.Empty<double?>();
            m_qflShortBases = Array.Empty<double?>();
        }

        private static TimeFrameWindow[] GetRequiredTimeFrames(IOptions<QiqiStrategyOptions> options)
        {
            int hourLength = 72;
            return new[]
            {
                new TimeFrameWindow(TimeFrame.OneMinute, 15, true),
                new TimeFrameWindow(TimeFrame.OneHour, hourLength, false),
                new TimeFrameWindow(TimeFrame.OneDay, 15, false),
            };
        }

        public override string Name => StrategyNames.Qiqi;
        protected override decimal WalletExposureLong => m_options.Value.WalletExposureLong;
        protected override decimal WalletExposureShort => m_options.Value.WalletExposureShort;

        protected override Task<ReentryMultiplier> CalculateReentryMultiplierLongAsync()
        {
            return CalculateReentryMultiplierAsync();
        }

        protected override Task<ReentryMultiplier> CalculateReentryMultiplierShortAsync()
        {
            return CalculateReentryMultiplierAsync();
        }

        protected Task<ReentryMultiplier> CalculateReentryMultiplierAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.FromResult(new ReentryMultiplier(1.0, 1.0));
            var dailyQuotes = QuoteQueues[TimeFrame.OneDay].GetQuotes();
            if (dailyQuotes.Length < 14)
                return Task.FromResult(new ReentryMultiplier(1.0, 1.0));
            DateTime lastDay = dailyQuotes.Last().Date;
            if (lastDay == m_lastDayAtr)
                return Task.FromResult(m_lastDailyMultiplier);
            var atr = dailyQuotes.GetAtr();
            var lastAtr = atr.LastOrDefault();
            if (lastAtr == null)
                return Task.FromResult(new ReentryMultiplier(1.0, 1.0));
            if (lastAtr.Atr == null)
                return Task.FromResult(new ReentryMultiplier(1.0, 1.0));
            var normalizedAtr = (lastAtr.Atr.Value / (double)ticker.BestAskPrice);
            var weightMultiplier = 1.0 + normalizedAtr;
            var distanceMultiplier = normalizedAtr * 100;
            m_lastDailyMultiplier = new ReentryMultiplier(distanceMultiplier, weightMultiplier);
            m_lastDayAtr = lastDay;
            return Task.FromResult(m_lastDailyMultiplier);
        }

        protected override Task CalculateTakeProfitAsync(IList<StrategyIndicator> indicators)
        {
            LongTakeProfitPrice = null;
            ShortTakeProfitPrice = null;

            var ticker = Ticker;
            if (ticker == null)
                return Task.CompletedTask;

            var longPosition = LongPosition;
            if (longPosition != null)
            {
                var maxStuckPositionTime = longPosition.UpdateTime + m_options.Value.MaxTimeStuck;
                if (ticker.Timestamp > maxStuckPositionTime)
                {
                    LongTakeProfitPrice = ticker.BestAskPrice;
                }
                else
                {
                    var rsi = CalculateRsi();
                    if (rsi.HasValue)
                    {
                        var crossesBellow = TradingHelpers.CrossesBellow(
                            rsi.Value.PreviousRsi,
                            rsi.Value.LastRsi,
                            m_options.Value.RsiTakeProfitLong);
                        if (crossesBellow /*&& longPosition.AveragePrice < ticker.BestAskPrice*/)
                        {
                            LongTakeProfitPrice = ticker.BestAskPrice;
                        }
                        else
                        {
                            var longTakeProfitPrice = (double)longPosition.AveragePrice * (1.0 + m_options.Value.TakeProfitPercentLong);
                            if ((double)ticker.BestAskPrice > longTakeProfitPrice)
                                LongTakeProfitPrice = ticker.BestAskPrice;
                        }
                    }
                }
            }

            var shortPosition = ShortPosition;
            if (shortPosition != null)
            {
                var maxStuckPositionTime = shortPosition.UpdateTime + m_options.Value.MaxTimeStuck;
                if (ticker.Timestamp > maxStuckPositionTime)
                {
                    ShortTakeProfitPrice = ticker.BestBidPrice;
                }
                else
                {
                    var rsi = CalculateRsi();
                    if (rsi.HasValue)
                    {
                        var crossesAbove = TradingHelpers.CrossesAbove(
                            rsi.Value.PreviousRsi,
                            rsi.Value.LastRsi,
                            m_options.Value.RsiTakeProfitShort);
                        if (crossesAbove /*&& shortPosition.AveragePrice > ticker.BestBidPrice*/)
                        {
                            ShortTakeProfitPrice = ticker.BestBidPrice;
                        }
                        else
                        {
                            var shortTakeProfitPrice = (double)shortPosition.AveragePrice * (1.0 - m_options.Value.TakeProfitPercentShort);
                            if ((double)ticker.BestBidPrice < shortTakeProfitPrice)
                                ShortTakeProfitPrice = ticker.BestBidPrice;
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private (double PreviousRsi, double LastRsi)? CalculateRsi()
        {
            var hourlyQuotes = QuoteQueues[TimeFrame.OneHour].GetQuotes();
            if (hourlyQuotes.Length < 15)
                return null;
            RsiResult[] rsi;
            if (m_lastHourlyRsiTime == hourlyQuotes.Last().Date)
            {
                rsi = m_lastRsi;
            }
            else
            {
                rsi = hourlyQuotes.GetRsi().ToArray();
                m_lastRsi = rsi;
                m_lastHourlyRsiTime = hourlyQuotes.Last().Date;
            }

            var lastRsi = rsi.LastOrDefault();
            if (lastRsi == null)
                return null;
            if (lastRsi.Rsi == null)
                return null;
            var previousRsi = rsi[^2];
            if (previousRsi.Rsi == null)
                return null;
            return (previousRsi.Rsi.Value, lastRsi.Rsi.Value);
        }

        protected override async Task<SignalEvaluation> EvaluateSignalsInnerAsync(CancellationToken cancel)
        {
            var quotes = QuoteQueues[TimeFrame.OneMinute].GetQuotes();
            var hourlyQuotes = QuoteQueues[TimeFrame.OneHour].GetQuotes();
            var dailyQuotes = QuoteQueues[TimeFrame.OneDay].GetQuotes();
            var ticker = Ticker;
            List<StrategyIndicator> indicators = new();
            var lastQuote = quotes.LastOrDefault();
            bool hasBuySignal = false;
            bool hasSellSignal = false;
            bool hasBuyExtraSignal = false;
            bool hasSellExtraSignal = false;
            const int minQuotes = 15;

            if (lastQuote != null
                && hourlyQuotes.Length >= minQuotes
                && dailyQuotes.Length >= minQuotes
                && ticker != null)
            {
                bool canBeTraded = (lastQuote.Date - SymbolInfo.LaunchTime).TotalDays >
                                   m_options.Value.InitialUntradableDays;
                var volume = TradeSignalHelpers.VolumeInQuoteCurrency(lastQuote);
                bool shouldLong = false;
                bool shouldShort = false;
                double?[] qflLongBases;
                double?[] qflShortBases;
                var rsi = CalculateRsi();
                if (m_lastQflBasesTime == hourlyQuotes.Last().Date)
                {
                    qflLongBases = m_qflLongBases;
                    qflShortBases = m_qflShortBases;
                }
                else
                {
                    qflLongBases = TradeSignalHelpers.CalculateQflBuyBases(hourlyQuotes);
                    m_qflLongBases = qflLongBases;
                    qflShortBases = TradeSignalHelpers.CalculateQflSellBases(hourlyQuotes);
                    m_qflShortBases = qflShortBases;
                    m_lastQflBasesTime = hourlyQuotes.Last().Date;
                }

                var qflLongBase = qflLongBases.LastOrDefault();
                var nextGridLongPosition = await CalculateNextGridLongPositionAsync();
                var isBellowRsi = rsi.HasValue && rsi.Value.LastRsi < m_options.Value.RsiTakeProfitLong;
                var isAboveRsi = rsi.HasValue && rsi.Value.LastRsi > m_options.Value.RsiTakeProfitShort;
                bool canLongTrend = true;
                bool isBellowNextGridPosition = false;
                if (qflLongBase != null && nextGridLongPosition != null)
                {
                    var buyPrice = qflLongBase.Value * (1.0 - m_options.Value.QflBellowPercentEnterLong / 100.0);
                    var isBellowQfl = (double)ticker.BestBidPrice <= buyPrice;
                    isBellowNextGridPosition = (double)ticker.BestBidPrice <= nextGridLongPosition.Value.Price;
                    shouldLong = isBellowQfl && isBellowNextGridPosition;
                }

                var qflShortBase = qflShortBases.LastOrDefault();
                var nextGridShortPosition = await CalculateNextGridShortPositionAsync();
                bool canShortTrend = true;
                bool isAboveNextGridPosition = false;
                if (qflShortBase != null && nextGridShortPosition != null)
                {
                    var sellPrice = qflShortBase.Value * (1.0 + m_options.Value.QflAbovePercentEnterShort / 100.0);
                    var isAboveQfl = (double)ticker.BestAskPrice >= sellPrice;
                    isAboveNextGridPosition = (double)ticker.BestAskPrice >= nextGridShortPosition.Value.Price;
                    shouldShort = isAboveQfl && isAboveNextGridPosition;
                }

                hasBuySignal = shouldLong
                               && isBellowRsi
                               && canLongTrend
                               && canBeTraded;
                hasSellSignal = shouldShort
                                && isAboveRsi
                                && canShortTrend
                                && canBeTraded;

                Position? longPosition = LongPosition;
                Position? shortPosition = ShortPosition;
                hasBuyExtraSignal = isBellowNextGridPosition
                                    && longPosition != null
                                    && ticker.BestBidPrice < longPosition.AveragePrice
                                    && canBeTraded;
                hasSellExtraSignal = isAboveNextGridPosition
                                     && shortPosition != null
                                     && ticker.BestAskPrice > shortPosition.AveragePrice
                                     && canBeTraded;

                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Volume1Min), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.MainTimeFrameVolume), volume));
            }

            return new SignalEvaluation(hasBuySignal, hasSellSignal, hasBuyExtraSignal, hasSellExtraSignal,
                indicators.ToArray());
        }
    }
}