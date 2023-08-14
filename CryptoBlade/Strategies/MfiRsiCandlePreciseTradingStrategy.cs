using Bybit.Net.Interfaces.Clients;
using CryptoBlade.Helpers;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;
using System.Threading;
using CryptoBlade.Exchanges;

namespace CryptoBlade.Strategies
{
    public class MfiRsiCandlePreciseTradingStrategy : TradingStrategyBase
    {
        private readonly IOptions<MfiRsiCandlePreciseTradingStrategyOptions> m_options;
        private const int c_candlePeriod = 50;
        private const int c_untradableFirstDays = 30;

        public MfiRsiCandlePreciseTradingStrategy(IOptions<MfiRsiCandlePreciseTradingStrategyOptions> options,
            string symbol, IWalletManager walletManager, ICbFuturesRestClient restClient)
            : base(options, symbol, GetRequiredTimeFrames(), walletManager, restClient)
        {
            m_options = options;
        }

        private static TimeFrameWindow[] GetRequiredTimeFrames()
        {
            return new[]
            {
                new TimeFrameWindow(TimeFrame.OneMinute, c_candlePeriod, true),
                new TimeFrameWindow(TimeFrame.FiveMinutes, c_candlePeriod, false),
            };
        }

        public override string Name
        {
            get { return "MfiRsiCandlePrecise"; }
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
            if (lastQuote != null)
            {
                bool canBeTraded = (lastQuote.Date - SymbolInfo.LaunchTime).TotalDays > c_untradableFirstDays;
                var spread5Min = TradeSignalHelpers.Get5MinSpread(quotes);
                var mfi = quotes.GetMfi();
                var lastMfi = mfi.LastOrDefault();
                var rsi = quotes.GetRsi();
                var lastRsi = rsi.LastOrDefault();
                var mfiRsiBuy = TradeSignalHelpers.IsMfiRsiBuy(lastMfi, lastRsi, lastQuote);
                var mfiRsiSell = TradeSignalHelpers.IsMfiRsiSell(lastMfi, lastRsi, lastQuote);
                bool hasMinSpread = spread5Min >= m_options.Value.MinimumPriceDistance;
                var volume = TradeSignalHelpers.VolumeInQuoteCurrency(lastQuote);
                bool hasMinVolume = volume > m_options.Value.MinimumVolume;
                hasBuySignal = mfiRsiBuy && hasMinSpread && hasMinVolume && canBeTraded;
                hasSellSignal = mfiRsiSell && hasMinSpread && hasMinVolume && canBeTraded;

                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Volume1Min), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.MainTimeFrameVolume), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Spread5Min), spread5Min));
                if (lastMfi?.Mfi != null)
                    indicators.Add(new StrategyIndicator(nameof(IndicatorType.Mfi1Min), lastMfi.Mfi.Value));
                if (lastRsi?.Rsi != null)
                    indicators.Add(new StrategyIndicator(nameof(IndicatorType.Rsi1Min), lastRsi.Rsi.Value));
            }

            return Task.FromResult(new SignalEvaluation(hasBuySignal, hasSellSignal, hasBuySignal, hasSellSignal, indicators.ToArray()));
        }
    }
}