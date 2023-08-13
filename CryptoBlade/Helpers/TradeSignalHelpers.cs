using CryptoBlade.Models;
using CryptoBlade.Strategies.Common;
using Microsoft.VisualBasic;
using Skender.Stock.Indicators;

namespace CryptoBlade.Helpers
{
    public static class TradeSignalHelpers
    {
        public static decimal VolumeInQuoteCurrency(Quote quote)
        {
            var typicalPrice = (quote.High + quote.Low + quote.Close) / 3.0m;
            var volume = (quote.Volume * typicalPrice);
            return volume;
        }

        public static decimal? GetTrendPercent(SmaResult? sma, Quote quote)
        {
            if (sma == null || !sma.Sma.HasValue)
                return null;
            var lastClosePrice = quote.Close;
            var smaValue = (decimal)sma.Sma.Value;
            var trendPercent = Math.Round((lastClosePrice - smaValue) / lastClosePrice * 100.0m, 4);
            return trendPercent;
        }

        public static bool ShortCounterTradeCondition(decimal bestAskPrice, decimal maHigh)
        {
            return bestAskPrice > maHigh;
        }

        public static bool LongCounterTradeCondition(decimal bestBidPrice, decimal maLow)
        {
            return bestBidPrice < maLow;
        }

        public static Trend GetTrend(decimal? trendPercent)
        {
            if(trendPercent == null)
                return Trend.Neutral;
            if (trendPercent > 0)
                return Trend.Short;
            if (trendPercent < 0)
                return Trend.Long;
            return Trend.Neutral;
        }

        public static bool IsMfiRsiBuy(MfiResult? mfi, RsiResult? rsi, Quote quote)
        {
            if (mfi == null || rsi == null)
                return false;
            if (!mfi.Mfi.HasValue || !rsi.Rsi.HasValue)
                return false;
            bool buy = mfi.Mfi < 20 && rsi.Rsi < 35 && quote.Close < quote.Open;
            return buy;
        }

        public static bool IsMfiRsiSell(MfiResult? mfi, RsiResult? rsi, Quote quote)
        {
            if (mfi == null || rsi == null)
                return false;
            if (!mfi.Mfi.HasValue || !rsi.Rsi.HasValue)
                return false;
            bool sell = mfi.Mfi > 80 && rsi.Rsi > 65 && quote.Close > quote.Open;
            return sell;
        }

        public static decimal Get5MinSpread(Quote[] quotes)
        {
            var last5 = quotes.Reverse().Take(5).ToArray();
            var highestHigh = last5.Max(x => x.High);
            var lowestLow = last5.Min(x => x.Low);
            var spread = Math.Round((highestHigh - lowestLow) / highestHigh * 100, 4);
            return spread;
        }

        public static Trend GetModifiedEriTrend(Quote[] quotes)
        {
            try
            {
                const int slowEmaPeriod = 64;
                var vwma = quotes.GetVwma(slowEmaPeriod).ToArray();
                if(vwma.All(x => !x.Vwma.HasValue))
                    return Trend.Neutral;
                var slowMovingAverage = vwma.GetEma(slowEmaPeriod);
                var lastAverage = slowMovingAverage.LastOrDefault();
                var lastQuote = quotes.LastOrDefault();
                if(lastAverage == null || lastQuote == null || !lastAverage.Ema.HasValue)
                    return Trend.Neutral;
                return lastQuote.Close > (decimal)lastAverage.Ema.Value ? Trend.Short : Trend.Long;
            }
            catch (ArgumentOutOfRangeException)
            {
                // there could be some shitty symbol that has no volume
                // it should be already handled, this is just a safety net
                return Trend.Neutral;
            }
        }

        public static Trend GetMfiTrend(Quote[] quotes)
        {
            const int lookbackPeriod = 100;
            var mfi = quotes.GetMfi().ToArray();
            var rsi = quotes.GetRsi().ToArray();
            int lookback = Math.Min(Math.Min(mfi.Length, rsi.Length), lookbackPeriod);
            for (int i = 1; i < (lookback + 1); i++)
            {
                var quote = quotes[^i];
                var mfiResult = mfi[^i];
                var rsiResult = rsi[^i];
                if (IsMfiRsiBuy(mfiResult, rsiResult, quote))
                    return Trend.Long;
                if (IsMfiRsiSell(mfiResult, rsiResult, quote))
                    return Trend.Short;
            }

            return Trend.Neutral;
        }
    }
}
