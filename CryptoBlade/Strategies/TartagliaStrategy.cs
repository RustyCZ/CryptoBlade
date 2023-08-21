using Accord.Statistics.Models.Regression.Linear;
using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace CryptoBlade.Strategies
{
    public class TartagliaStrategy : TradingStrategyBase
    {
        private readonly IOptions<TartagliaStrategyOptions> m_options;

        public TartagliaStrategy(IOptions<TartagliaStrategyOptions> options, 
            string symbol,
            IWalletManager walletManager,
            ICbFuturesRestClient cbFuturesRestClient) 
            : base(options, symbol, GetRequiredTimeFrames(options.Value.ChannelLength), walletManager, cbFuturesRestClient)
        {
            m_options = options;
        }

        private static TimeFrameWindow[] GetRequiredTimeFrames(int channelLength)
        {
            return new[]
            {
                new TimeFrameWindow(TimeFrame.OneMinute, channelLength, true),
                new TimeFrameWindow(TimeFrame.FiveMinutes, 15, false),
            };
        }

        public override string Name => "Tartaglia";
        protected override decimal WalletExposureLong => m_options.Value.WalletExposureLong;
        protected override decimal WalletExposureShort => m_options.Value.WalletExposureShort;
        protected override int DcaOrdersCount => m_options.Value.DcaOrdersCount;
        protected override bool ForceMinQty => m_options.Value.ForceMinQty;

        protected override async Task CalculateTakeProfitAsync(IList<StrategyIndicator> indicators)
        {
            List<StrategyIndicator> takeProfitIndicators = new List<StrategyIndicator>();
            await base.CalculateTakeProfitAsync(takeProfitIndicators);
            var linearRegressionPriceObj =
                indicators.FirstOrDefault(x => string.Equals(x.Name, nameof(IndicatorType.LinearRegressionPrice)));
            if (linearRegressionPriceObj.Value is decimal linearRegressionPrice)
            {
                if (LongTakeProfitPrice.HasValue && LongTakeProfitPrice < linearRegressionPrice)
                {
                    LongTakeProfitPrice = linearRegressionPrice;
                    indicators.Add(new StrategyIndicator(nameof(IndicatorType.LongTakeProfit), LongTakeProfitPrice));
                }
                else
                {
                    var hasTakeProfitIndicator = takeProfitIndicators.Any(x => string.Equals(x.Name, nameof(IndicatorType.LongTakeProfit)));
                    if (hasTakeProfitIndicator)
                    {
                        var takeProfitIndicator = takeProfitIndicators.First(x => string.Equals(x.Name, nameof(IndicatorType.LongTakeProfit)));
                        indicators.Add(takeProfitIndicator);
                    }
                }
                    
                
                if(ShortTakeProfitPrice.HasValue && ShortTakeProfitPrice > linearRegressionPrice)
                {
                    ShortTakeProfitPrice = linearRegressionPrice;
                    indicators.Add(new StrategyIndicator(nameof(IndicatorType.ShortTakeProfit), ShortTakeProfitPrice));
                }
                else
                {
                    var hasTakeProfitIndicator = takeProfitIndicators.Any(x => string.Equals(x.Name, nameof(IndicatorType.ShortTakeProfit)));
                    if (hasTakeProfitIndicator)
                    {
                        var takeProfitIndicator = takeProfitIndicators.First(x => string.Equals(x.Name, nameof(IndicatorType.ShortTakeProfit)));
                        indicators.Add(takeProfitIndicator);
                    }
                }
            }
        }

        protected override Task<SignalEvaluation> EvaluateSignalsInnerAsync(CancellationToken cancel)
        {
            var quotes = QuoteQueues[TimeFrame.OneMinute].GetQuotes();
            List<StrategyIndicator> indicators = new();
            var lastQuote = quotes.LastOrDefault();
            var ticker = Ticker;
            bool hasBuySignal = false;
            bool hasSellSignal = false;
            bool hasBuyExtraSignal = false;
            bool hasSellExtraSignal = false;

            if (lastQuote != null && ticker != null)
            {
                bool canBeTraded = (lastQuote.Date - SymbolInfo.LaunchTime).TotalDays > m_options.Value.InitialUntradableDays;
                var spread5Min = TradeSignalHelpers.Get5MinSpread(quotes);
                var volume = TradeSignalHelpers.VolumeInQuoteCurrency(lastQuote);
                bool hasMinSpread = spread5Min > m_options.Value.MinimumPriceDistance;
                bool hasMinVolume = volume >= m_options.Value.MinimumVolume;
                bool bellowChannel = false;
                bool aboveChannel = false;
                bool hasBasicConditions = canBeTraded && hasMinSpread && hasMinVolume;
                if (hasBasicConditions)
                {
                    OrdinaryLeastSquares ols = new OrdinaryLeastSquares();
                    double[] priceData = new double[quotes.Length];
                    double[] xAxis = new double[quotes.Length];
                    for (int i = 0; i < quotes.Length; i++)
                    {
                        var averagePrice = (quotes[i].Open + quotes[i].Close) / 2.0m;
                        priceData[i] = (double)averagePrice;
                        xAxis[i] = i;
                    }
                    var lr = ols.Learn(xAxis, priceData.ToArray());
                    var intercept = lr.Intercept;
                    var slope = lr.Slope;
                    var expectedPrice = intercept + slope * quotes.Length;
                    var lowerChannel = expectedPrice - m_options.Value.StandardDeviation * StandardDeviation(priceData);
                    bellowChannel = (double)ticker.LastPrice < lowerChannel;
                    var upperChannel = expectedPrice + m_options.Value.StandardDeviation * StandardDeviation(priceData);
                    aboveChannel = (double)ticker.LastPrice > upperChannel;
                    indicators.Add(new StrategyIndicator(nameof(IndicatorType.LinearRegressionPrice), (decimal)expectedPrice));
                }

                hasBuySignal = hasMinVolume
                               && bellowChannel
                               && hasMinSpread
                               && canBeTraded;

                hasSellSignal = hasMinVolume
                                && aboveChannel
                                && hasMinSpread
                                && canBeTraded;

                var longPosition = LongPosition;
                var shortPosition = ShortPosition;
                if (longPosition != null && hasBuySignal)
                {
                    var rebuyPrice = longPosition.AveragePrice * (1.0m - m_options.Value.MinReentryPositionDistance);
                    if (ticker.BestBidPrice < rebuyPrice)
                        hasBuyExtraSignal = hasBuySignal;
                }

                if (shortPosition != null && hasSellSignal)
                {
                    var resellPrice = shortPosition.AveragePrice * (1.0m + m_options.Value.MinReentryPositionDistance);
                    if (ticker.BestAskPrice > resellPrice)
                        hasSellExtraSignal = hasSellSignal;
                }

                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Volume1Min), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.MainTimeFrameVolume), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Spread5Min), spread5Min));
            }

            return Task.FromResult(new SignalEvaluation(hasBuySignal, hasSellSignal, hasBuyExtraSignal, hasSellExtraSignal, indicators.ToArray()));
        }

        private static double StandardDeviation(double[] data)
        {
            double mean = data.Sum() / data.Length;
            double sumOfSquares = data.Select(x => (x - mean) * (x - mean)).Sum();
            return Math.Sqrt(sumOfSquares / data.Length);
        }
    }
}