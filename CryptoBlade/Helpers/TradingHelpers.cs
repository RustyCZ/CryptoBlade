using Accord.Statistics.Models.Regression.Linear;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Wallet;
using Skender.Stock.Indicators;

namespace CryptoBlade.Helpers
{
    public static class TradingHelpers
    {
        public static decimal? CalculateQuantity(this SymbolInfo symbolInfo,
            IWalletManager walletManager,
            decimal price,
            decimal walletExposure,
            decimal dcaMultiplier)
        {
            if (price == 0)
                return null;
            if (walletExposure == 0)
                return null;
            decimal? walletBalance = walletManager.Contract.WalletBalance;
            if (!walletBalance.HasValue)
                return null;
            if (!symbolInfo.MaxLeverage.HasValue)
                return null;
            if (!symbolInfo.QtyStep.HasValue)
                return null;
            decimal maxTradeQty =
                walletBalance.Value * walletExposure / price / (100.0m / symbolInfo.MaxLeverage.Value);
            decimal dynamicQty = maxTradeQty / dcaMultiplier;
            dynamicQty -= (dynamicQty % symbolInfo.QtyStep.Value);
            return dynamicQty;
        }

        public static decimal? CalculateMinBalance(this SymbolInfo symbolInfo,
            decimal price,
            decimal walletExposure,
            decimal dcaMultiplier)
        {
            if (!symbolInfo.MaxLeverage.HasValue)
                return null;
            if (!symbolInfo.QtyStep.HasValue)
                return null;
            if (walletExposure == 0)
                return null;
            decimal minBalance =
                symbolInfo.QtyStep.Value * dcaMultiplier * (100.0m / symbolInfo.MaxLeverage.Value) * price /
                walletExposure;
            return minBalance;
        }

        public static decimal? CalculateShortTakeProfit(Position position, SymbolInfo symbolInfo, Quote[] quotes,
            decimal increasePercentage, Ticker currentPrice, decimal feeRate, decimal minProfitRate)
        {
            try
            {
                var ma6High = quotes.Use(CandlePart.High).GetSma(6);
                var ma6Low = quotes.Use(CandlePart.Low).GetSma(6);
                var ma6HighLast = ma6High.LastOrDefault();
                var ma6LowLast = ma6Low.LastOrDefault();
                if (ma6LowLast == null || !ma6LowLast.Sma.HasValue)
                    return null;
                if (ma6HighLast == null || !ma6HighLast.Sma.HasValue)
                    return null;
                decimal shortTargetPrice =
                    position.AveragePrice - ((decimal)ma6HighLast.Sma.Value - (decimal)ma6LowLast.Sma.Value);
                decimal shortTakeProfit = shortTargetPrice * (1.0m - increasePercentage / 100.0m);
                decimal entryFee = position.AveragePrice * position.Quantity * feeRate;
                decimal exitFee = shortTakeProfit * position.Quantity * feeRate;
                decimal totalFee = entryFee + exitFee;
                decimal feeInPrice = totalFee / position.Quantity;
                shortTakeProfit -= feeInPrice;
                shortTakeProfit = Math.Round(shortTakeProfit, (int)symbolInfo.PriceScale,
                    MidpointRounding.AwayFromZero);
                if (minProfitRate >= 1.0m)
                    minProfitRate = 0.99m;
                decimal shortMinTakeProfit = position.AveragePrice * (1.0m - minProfitRate);
                shortMinTakeProfit -= feeInPrice;
                shortMinTakeProfit = Math.Round(shortMinTakeProfit, (int)symbolInfo.PriceScale,
                    MidpointRounding.AwayFromZero);

                if (shortTakeProfit > shortMinTakeProfit)
                    shortTakeProfit = shortMinTakeProfit;

                if (currentPrice.BestBidPrice < shortTakeProfit)
                    shortTakeProfit = currentPrice.BestBidPrice;

                if (shortTakeProfit <= 0)
                    shortTakeProfit = (decimal)Math.Pow(10, -(int)symbolInfo.PriceScale);

                return shortTakeProfit;
            }
            catch
            {
                return null;
            }
        }

        public static decimal? CalculateLongTakeProfit(Position position, SymbolInfo symbolInfo, Quote[] quotes,
            decimal increasePercentage, Ticker currentPrice, decimal feeRate, decimal minProfitRate)
        {
            try
            {
                var ma6High = quotes.Use(CandlePart.High).GetSma(6);
                var ma6Low = quotes.Use(CandlePart.Low).GetSma(6);
                var ma6HighLast = ma6High.LastOrDefault();
                var ma6LowLast = ma6Low.LastOrDefault();
                if (ma6LowLast == null || !ma6LowLast.Sma.HasValue)
                    return null;
                if (ma6HighLast == null || !ma6HighLast.Sma.HasValue)
                    return null;
                decimal longTargetPrice =
                    position.AveragePrice + ((decimal)ma6HighLast.Sma.Value - (decimal)ma6LowLast.Sma.Value);
                decimal longTakeProfit = longTargetPrice * (1.0m + increasePercentage / 100.0m);
                decimal entryFee = position.AveragePrice * position.Quantity * feeRate;
                decimal exitFee = longTakeProfit * position.Quantity * feeRate;
                decimal totalFee = entryFee + exitFee;
                decimal feeInPrice = totalFee / position.Quantity;
                longTakeProfit += feeInPrice;
                longTakeProfit = Math.Round(longTakeProfit, (int)symbolInfo.PriceScale, MidpointRounding.AwayFromZero);

                decimal longMinTakeProfit = position.AveragePrice * (1.0m + minProfitRate);
                longMinTakeProfit += feeInPrice;
                longMinTakeProfit = Math.Round(longMinTakeProfit, (int)symbolInfo.PriceScale,
                    MidpointRounding.AwayFromZero);

                if (longTakeProfit < longMinTakeProfit)
                    longTakeProfit = longMinTakeProfit;

                if (currentPrice.BestAskPrice > longTakeProfit)
                    longTakeProfit = currentPrice.BestAskPrice;
                return longTakeProfit;
            }
            catch
            {
                return null;
            }
        }

        public static bool CrossesBellow(this Quote quote, double priceLevel)
        {
            return (double)quote.High > priceLevel && (double)quote.Low < priceLevel;
        }

        public static bool CrossesBellow(double previousValue, double currentValue, double value)
        {
            return previousValue > value && currentValue <= value;
        }

        public static bool CrossesAbove(this Quote quote, double priceLevel)
        {
            return (double)quote.Low < priceLevel && (double)quote.High > priceLevel;
        }

        public static bool CrossesAbove(double previousValue, double currentValue, double value)
        {
            return previousValue < value && currentValue >= value;
        }

        public static LinearChannelPrice CalculateExpectedPrice(Quote[] quotes, int channelLength)
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

        public static double StandardDeviation(double[] data)
        {
            double mean = data.Sum() / data.Length;
            double sumOfSquares = 0;
            for (int i = 0; i < data.Length; i++)
            {
                var x = data[i];
                sumOfSquares += (x - mean) * (x - mean);
            }

            return Math.Sqrt(sumOfSquares / data.Length);
        }

        public static double RootMeanSquareError(IReadOnlyList<double> estimated, IReadOnlyList<double> measured)
        {
            double sum = 0;
            for (int i = 0; i < estimated.Count; i++)
            {
                var error = estimated[i] - measured[i];
                sum += error * error;
            }

            return Math.Sqrt(sum / estimated.Count);
        }

        public static double NormalizedRootMeanSquareError(IReadOnlyList<double> estimated, IReadOnlyList<double> measured)
        {
            double sum = 0;
            for (int i = 0; i < estimated.Count; i++)
            {
                var error = estimated[i] - measured[i];
                sum += error * error;
            }
            var average = measured.Average();
            var nrmse = Math.Sqrt(sum / estimated.Count) / average;
            if (nrmse > 1)
                nrmse = 1;
            return nrmse;
        }
    }
}