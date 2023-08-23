using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoBlade.Models;

namespace CryptoBlade.Mapping
{
    public static class BinanceMappingHelpers
    {
        public static KlineInterval ToBinanceKlineInterval(this TimeFrame timeFrame)
        {
            switch (timeFrame)
            {
                case TimeFrame.OneMinute:
                    return KlineInterval.OneMinute;
                case TimeFrame.FiveMinutes:
                    return KlineInterval.FiveMinutes;
                case TimeFrame.FifteenMinutes:
                    return KlineInterval.FifteenMinutes;
                case TimeFrame.ThirtyMinutes:
                    return KlineInterval.ThirtyMinutes;
                case TimeFrame.OneHour:
                    return KlineInterval.OneHour;
                case TimeFrame.FourHours:
                    return KlineInterval.FourHour;
                case TimeFrame.OneDay:
                    return KlineInterval.OneDay;
                case TimeFrame.OneWeek:
                    return KlineInterval.OneWeek;
                case TimeFrame.OneMonth:
                    return KlineInterval.OneMonth;
                default:
                    return KlineInterval.OneMinute;
            }
        }

        public static Candle ToCandle(this IBinanceKline kline, TimeFrame timeFrame)
        {
            return new Candle
            {
                Open = kline.OpenPrice,
                Close = kline.ClosePrice,
                High = kline.HighPrice,
                Low = kline.LowPrice,
                Volume = kline.QuoteVolume,
                StartTime = kline.OpenTime,
                TimeFrame = timeFrame,
            };
        }
    }
}
