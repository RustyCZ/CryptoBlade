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
            decimal minBalance =
                symbolInfo.QtyStep.Value * dcaMultiplier * (100.0m / symbolInfo.MaxLeverage.Value) * price /
                walletExposure;
            return minBalance;
        }

        public static decimal? CalculateShortTakeProfit(Position position, SymbolInfo symbolInfo, Quote[] quotes,
            decimal increasePercentage, Ticker currentPrice, decimal feeRate, decimal minProfitRate)
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
            shortTakeProfit = Math.Round(shortTakeProfit, (int)symbolInfo.PriceScale, MidpointRounding.AwayFromZero);
            if (minProfitRate >= 1.0m)
                minProfitRate = 0.99m;
            decimal shortMinTakeProfit = position.AveragePrice * (1.0m - minProfitRate);
            shortMinTakeProfit -= feeInPrice;
            shortMinTakeProfit = Math.Round(shortMinTakeProfit, (int)symbolInfo.PriceScale, MidpointRounding.AwayFromZero);

            if (shortTakeProfit > shortMinTakeProfit)
                shortTakeProfit = shortMinTakeProfit;

            if (currentPrice.BestBidPrice < shortTakeProfit)
                shortTakeProfit = currentPrice.BestBidPrice;

            if(shortTakeProfit <= 0)
                shortTakeProfit = (decimal)Math.Pow(10, -(int)symbolInfo.PriceScale);

            return shortTakeProfit;
        }

        public static decimal? CalculateLongTakeProfit(Position position, SymbolInfo symbolInfo, Quote[] quotes,
            decimal increasePercentage, Ticker currentPrice, decimal feeRate, decimal minProfitRate)
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
            longMinTakeProfit = Math.Round(longMinTakeProfit, (int)symbolInfo.PriceScale, MidpointRounding.AwayFromZero);

            if (longTakeProfit < longMinTakeProfit)
                longTakeProfit = longMinTakeProfit;

            if (currentPrice.BestAskPrice > longTakeProfit)
                longTakeProfit = currentPrice.BestAskPrice;
            return longTakeProfit;
        }
    }
}