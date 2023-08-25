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
            : base(options, symbol, GetRequiredTimeFrames(Math.Max(options.Value.ChannelLengthLong, options.Value.ChannelLengthShort)), walletManager, cbFuturesRestClient)
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
            var linearRegressionPriceLongObj =
                indicators.FirstOrDefault(x => string.Equals(x.Name, nameof(IndicatorType.LinearRegressionPriceLong)));
            var linearRegressionPriceShortObj =
                indicators.FirstOrDefault(x => string.Equals(x.Name, nameof(IndicatorType.LinearRegressionPriceShort)));
            if (linearRegressionPriceLongObj.Value is decimal linearRegressionPriceLong)
            {
                if (LongTakeProfitPrice.HasValue && LongTakeProfitPrice < linearRegressionPriceLong)
                {
                    LongTakeProfitPrice = linearRegressionPriceLong;
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
            }

            if (linearRegressionPriceShortObj.Value is decimal linearRegressionPriceShort)
            {
                if (ShortTakeProfitPrice.HasValue && ShortTakeProfitPrice > linearRegressionPriceShort)
                {
                    ShortTakeProfitPrice = linearRegressionPriceShort;
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
                    var expectedShortPrice = CalculateExpectedPrice(quotes, m_options.Value.ChannelLengthShort);
                    var expectedLongPrice = expectedShortPrice;
                    if (m_options.Value.ChannelLengthLong != m_options.Value.ChannelLengthShort) 
                        expectedLongPrice = CalculateExpectedPrice(quotes, m_options.Value.ChannelLengthLong);

                    if (expectedLongPrice.ExpectedPrice.HasValue && expectedShortPrice.StandardDeviation.HasValue)
                    {
                        var lowerChannel = expectedLongPrice.ExpectedPrice.Value - m_options.Value.StandardDeviationLong * expectedShortPrice.StandardDeviation;
                        bellowChannel = (double)ticker.LastPrice < lowerChannel;
                        indicators.Add(new StrategyIndicator(nameof(IndicatorType.LinearRegressionPriceLong), (decimal)expectedLongPrice.ExpectedPrice.Value));
                    }

                    if (expectedShortPrice.ExpectedPrice.HasValue && expectedShortPrice.StandardDeviation.HasValue)
                    {
                        var upperChannel = expectedShortPrice.ExpectedPrice.Value + m_options.Value.StandardDeviationShort * expectedShortPrice.StandardDeviation;
                        aboveChannel = (double)ticker.LastPrice > upperChannel;
                        indicators.Add(new StrategyIndicator(nameof(IndicatorType.LinearRegressionPriceShort), (decimal)expectedShortPrice.ExpectedPrice.Value));
                    }
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
                    var rebuyPrice = longPosition.AveragePrice * (1.0m - m_options.Value.MinReentryPositionDistanceLong);
                    if (ticker.BestBidPrice < rebuyPrice)
                        hasBuyExtraSignal = hasBuySignal;
                }

                if (shortPosition != null && hasSellSignal)
                {
                    var resellPrice = shortPosition.AveragePrice * (1.0m + m_options.Value.MinReentryPositionDistanceShort);
                    if (ticker.BestAskPrice > resellPrice)
                        hasSellExtraSignal = hasSellSignal;
                }

                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Volume1Min), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.MainTimeFrameVolume), volume));
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.Spread5Min), spread5Min));
            }

            return Task.FromResult(new SignalEvaluation(hasBuySignal, hasSellSignal, hasBuyExtraSignal, hasSellExtraSignal, indicators.ToArray()));
        }

        private LinearChannelPrice CalculateExpectedPrice(Quote[] quotes, int channelLength)
        {
            int quotesLength = quotes.Length;
            int skip = quotesLength - channelLength;
            if (skip < 0)
                return new LinearChannelPrice(null, null); // not enough quotes
            OrdinaryLeastSquares ols = new OrdinaryLeastSquares();
            double[] priceData = new double[channelLength];
            double[] xAxis = new double[channelLength];
            for (int i = 0; i < channelLength; i++)
            {
                var averagePrice = (quotes[i + skip].Open + quotes[i + skip].Close) / 2.0m;
                priceData[i] = (double)averagePrice;
                xAxis[i] = i;
            }
            var lr = ols.Learn(xAxis, priceData.ToArray());
            var intercept = lr.Intercept;
            var slope = lr.Slope;
            var expectedPrice = intercept + slope * quotes.Length;
            var standardDeviation = StandardDeviation(priceData);

            return new LinearChannelPrice(expectedPrice, standardDeviation);
        }

        private static double StandardDeviation(double[] data)
        {
            double mean = data.Sum() / data.Length;
            double sumOfSquares = data.Select(x => (x - mean) * (x - mean)).Sum();
            return Math.Sqrt(sumOfSquares / data.Length);
        }

        private readonly record struct LinearChannelPrice(double? ExpectedPrice, double? StandardDeviation);
    }
}