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
        public double QflBellowPercentEnterLong { get; set; } = 1.1;
        public TimeSpan MaxTimeStuck { get; set; } = TimeSpan.FromDays(120);
    }

    public class QiqiStrategy : RecursiveStrategyBase
    {
        private readonly IOptions<QiqiStrategyOptions> m_options;
        private DateTime m_lastDayAtr;
        private ReentryMultiplier m_lastDailyMultiplier;
        private DateTime m_lastHourlyRsiTime;
        private RsiResult[] m_lastRsi;
        private DateTime m_lastQflBasesTime;
        private double?[] m_qflBases;

        public QiqiStrategy(IOptions<QiqiStrategyOptions> options, 
            string symbol, 
            IWalletManager walletManager, 
            ICbFuturesRestClient cbFuturesRestClient) : base(options, symbol, GetRequiredTimeFrames(), walletManager, cbFuturesRestClient)
        {
            m_lastDailyMultiplier = new ReentryMultiplier(1.0, 1.0);
            m_lastDayAtr = DateTime.MinValue;
            m_options = options;
            m_lastHourlyRsiTime = DateTime.MinValue;
            m_lastRsi = Array.Empty<RsiResult>();
            m_lastQflBasesTime = DateTime.MinValue;
            m_qflBases = Array.Empty<double?>();
        }

        private static TimeFrameWindow[] GetRequiredTimeFrames()
        {
            return new[]
            {
                new TimeFrameWindow(TimeFrame.OneMinute, 15, true),
                new TimeFrameWindow(TimeFrame.OneHour, 72, false),
                new TimeFrameWindow(TimeFrame.OneDay, 15, false),
            };
        }

        public override string Name => StrategyNames.Qiqi;
        protected override decimal WalletExposureLong  => m_options.Value.WalletExposureLong;
        protected override decimal WalletExposureShort => m_options.Value.WalletExposureShort;

        protected override Task<ReentryMultiplier> CalculateReentryMultiplierLongAsync()
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
            var longPosition = LongPosition;
            if (longPosition == null)
                return Task.CompletedTask;
            var ticker = Ticker;
            if (ticker == null)
                return Task.CompletedTask;

            var maxStuckPositionTime = longPosition.UpdateTime + m_options.Value.MaxTimeStuck;
            if (ticker.Timestamp > maxStuckPositionTime)
            {
                LongTakeProfitPrice = ticker.BestAskPrice;
                return Task.CompletedTask;
            }
            var hourlyQuotes = QuoteQueues[TimeFrame.OneHour].GetQuotes();
            if (hourlyQuotes.Length < 15)
                return Task.CompletedTask;
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
                return Task.CompletedTask;
            if (lastRsi.Rsi == null)
                return Task.CompletedTask;
            var previousRsi = rsi[^2];
            if (previousRsi.Rsi == null)
                return Task.CompletedTask;
            var crossesBellow = TradingHelpers.CrossesBellow(
                previousRsi.Rsi.Value, 
                lastRsi.Rsi.Value, 
                m_options.Value.RsiTakeProfitLong);
            if (!crossesBellow)
                return Task.CompletedTask;
            LongTakeProfitPrice = ticker.BestAskPrice;
            return Task.CompletedTask;
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
                bool canBeTraded = (lastQuote.Date - SymbolInfo.LaunchTime).TotalDays > m_options.Value.InitialUntradableDays;
                var volume = TradeSignalHelpers.VolumeInQuoteCurrency(lastQuote);
                bool shouldLong = false;
                double?[] qflLongBases;
                if (m_lastQflBasesTime == hourlyQuotes.Last().Date)
                {
                    qflLongBases = m_qflBases;
                }
                else
                {
                    qflLongBases = TradeSignalHelpers.CalculateQflBuyBases(hourlyQuotes);
                    m_qflBases = qflLongBases;
                    m_lastQflBasesTime = hourlyQuotes.Last().Date;
                }
                var qflLongBase = qflLongBases.LastOrDefault();
                var nextGridPosition = await CalculateNextGridLongPositionAsync();
                // deal with zero value
                if (qflLongBase != null && nextGridPosition != null)
                {
                    var buyPrice = qflLongBase.Value * (1.0 - m_options.Value.QflBellowPercentEnterLong / 100.0);
                    var isBellowQfl = (double)ticker.BestBidPrice <= buyPrice;
                    var isBellowNextGridPosition = (double)ticker.BestBidPrice <= nextGridPosition.Value.Price;
                    shouldLong = isBellowQfl && isBellowNextGridPosition;
                }

                hasBuySignal = shouldLong
                    && canBeTraded;

                Position? longPosition = LongPosition;

                hasBuyExtraSignal = shouldLong
                                    && longPosition != null
                                    && ticker.BestBidPrice < longPosition.AveragePrice
                                    && canBeTraded;

                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Volume1Min), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.MainTimeFrameVolume), volume));
            }

            return new SignalEvaluation(hasBuySignal, hasSellSignal, hasBuyExtraSignal, hasSellExtraSignal, indicators.ToArray());
        }
    }
}
