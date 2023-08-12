using CryptoBlade.Models;
using CryptoBlade.Strategies.Wallet;
using Skender.Stock.Indicators;

namespace CryptoBlade.Mapping
{
    public static class MappingHelpers
    {
        public static TimeSpan ToTimespan(this TimeFrame timeFrame)
        {
            switch (timeFrame)
            {
                case TimeFrame.OneMinute:
                    return TimeSpan.FromMinutes(1);
                case TimeFrame.FiveMinutes:
                    return TimeSpan.FromMinutes(5);
                case TimeFrame.FifteenMinutes:
                    return TimeSpan.FromMinutes(15);
                case TimeFrame.ThirtyMinutes:
                    return TimeSpan.FromMinutes(30);
                case TimeFrame.OneHour:
                    return TimeSpan.FromHours(1);
                case TimeFrame.FourHours:
                    return TimeSpan.FromHours(4);
                case TimeFrame.OneDay:
                    return TimeSpan.FromDays(1);
                case TimeFrame.OneWeek:
                    return TimeSpan.FromDays(7);
                case TimeFrame.OneMonth:
                    return TimeSpan.FromDays(30);
                default: throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null);
            }
        }

        public static Bybit.Net.Enums.KlineInterval ToKlineInterval(this TimeFrame timeFrame)
        {
            switch (timeFrame)
            {
                case TimeFrame.OneMinute:
                    return Bybit.Net.Enums.KlineInterval.OneMinute;
                case TimeFrame.FiveMinutes:
                    return Bybit.Net.Enums.KlineInterval.FiveMinutes;
                case TimeFrame.FifteenMinutes:
                    return Bybit.Net.Enums.KlineInterval.FifteenMinutes;
                case TimeFrame.ThirtyMinutes:
                    return Bybit.Net.Enums.KlineInterval.ThirtyMinutes;
                case TimeFrame.OneHour:
                    return Bybit.Net.Enums.KlineInterval.OneHour;
                case TimeFrame.FourHours:
                    return Bybit.Net.Enums.KlineInterval.FourHours;
                case TimeFrame.OneDay:
                    return Bybit.Net.Enums.KlineInterval.OneDay;
                case TimeFrame.OneWeek:
                    return Bybit.Net.Enums.KlineInterval.OneWeek;
                case TimeFrame.OneMonth:
                    return Bybit.Net.Enums.KlineInterval.OneMonth;
                default: throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null);
            }
        }

        public static TimeFrame ToTimeFrame(this Bybit.Net.Enums.KlineInterval value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.KlineInterval.OneMinute:
                    return TimeFrame.OneMinute;
                case Bybit.Net.Enums.KlineInterval.FiveMinutes:
                    return TimeFrame.FiveMinutes;
                case Bybit.Net.Enums.KlineInterval.FifteenMinutes:
                    return TimeFrame.FifteenMinutes;
                case Bybit.Net.Enums.KlineInterval.ThirtyMinutes:
                    return TimeFrame.ThirtyMinutes;
                case Bybit.Net.Enums.KlineInterval.OneHour:
                    return TimeFrame.OneHour;
                case Bybit.Net.Enums.KlineInterval.FourHours:
                    return TimeFrame.FourHours;
                case Bybit.Net.Enums.KlineInterval.OneDay:
                    return TimeFrame.OneDay;
                case Bybit.Net.Enums.KlineInterval.OneWeek:
                    return TimeFrame.OneWeek;
                case Bybit.Net.Enums.KlineInterval.OneMonth:
                    return TimeFrame.OneMonth;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static Candle ToCandle(this Bybit.Net.Objects.Models.V5.BybitKline kline, TimeFrame timeFrame)
        {
            return new Candle
            {
                Open = kline.OpenPrice,
                Close = kline.ClosePrice,
                High = kline.HighPrice,
                Low = kline.LowPrice,
                Volume = kline.Volume,
                TimeFrame = timeFrame,
                StartTime = kline.StartTime,
            };
        }

        public static Candle ToCandle(this Bybit.Net.Objects.Models.V5.BybitKlineUpdate klineUpdate)
        {
            return new Candle
            {
                Open = klineUpdate.OpenPrice,
                Close = klineUpdate.ClosePrice,
                High = klineUpdate.HighPrice,
                Low = klineUpdate.LowPrice,
                Volume = klineUpdate.Volume,
                TimeFrame = klineUpdate.Interval.ToTimeFrame(),
                StartTime = klineUpdate.StartTime,
            };
        }

        public static Quote ToQuote(this Candle candle)
        {
            return new Quote
            {
                Close = candle.Close,
                Date = candle.StartTime,
                High = candle.High,
                Low = candle.Low,
                Open = candle.Open,
                Volume = candle.Volume
            };
        }

        public static Ticker ToTicker(this Bybit.Net.Objects.Models.V5.BybitLinearInverseTicker ticker)
        {
            return new Ticker
            {
                BestAskPrice = ticker.BestAskPrice,
                LastPrice = ticker.LastPrice,
                BestBidPrice = ticker.BestBidPrice,
                FundingRate = ticker.FundingRate,
            };
        }

        public static TimeSpan ToTimeSpan(this TimeFrame timeFrame)
        {
            switch (timeFrame)
            {
                case TimeFrame.OneMinute:
                    return TimeSpan.FromMinutes(1);
                case TimeFrame.FiveMinutes:
                    return TimeSpan.FromMinutes(5);
                case TimeFrame.FifteenMinutes:
                    return TimeSpan.FromMinutes(15);
                case TimeFrame.ThirtyMinutes:
                    return TimeSpan.FromMinutes(30);
                case TimeFrame.OneHour:
                    return TimeSpan.FromHours(1);
                case TimeFrame.FourHours:
                    return TimeSpan.FromHours(4);
                case TimeFrame.OneDay:
                    return TimeSpan.FromDays(1);
                case TimeFrame.OneWeek:
                    return TimeSpan.FromDays(7);
                case TimeFrame.OneMonth:
                    return TimeSpan.FromDays(30);
                default: throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, null);
            }
        }

        public static Balance ToBalance(this Bybit.Net.Objects.Models.V5.BybitAssetBalance balance)
        {
            return new Balance(
                balance.Equity,
                balance.WalletBalance,
                balance.UnrealizedPnl,
                balance.RealizedPnl);
        }

        public static Ticker? ToTicker(this Bybit.Net.Objects.Models.V5.BybitLinearTickerUpdate ticker)
        {
            if(ticker.BestAskPrice == null || ticker.BestBidPrice == null || ticker.LastPrice == null)
                return null;
            return new Ticker
            {
                BestAskPrice = ticker.BestAskPrice.Value,
                LastPrice = ticker.LastPrice.Value,
                BestBidPrice = ticker.BestBidPrice.Value,
                FundingRate = ticker.FundingRate,
            };
        }

        public static SymbolInfo ToSymbolInfo(this Bybit.Net.Objects.Models.V5.BybitLinearInverseSymbol symbol)
        {
            return new SymbolInfo
            {
                Name = symbol.Name,
                PriceScale = symbol.PriceScale,
                BaseAsset = symbol.BaseAsset,
                QuoteAsset = symbol.QuoteAsset,
                MinOrderQty = symbol.LotSizeFilter?.MinOrderQuantity,
                QtyStep = symbol.LotSizeFilter?.QuantityStep,
                MaxLeverage = symbol.LeverageFilter?.MaxLeverage,
            };
        }

        public static Order ToOrder(this Bybit.Net.Objects.Models.V5.BybitOrder value)
        {
            return new Order
            {
                Symbol = value.Symbol,
                Price = value.Price,
                AveragePrice = value.AveragePrice,
                OrderId = value.OrderId,
                PositionMode = value.PositionMode.ToPositionMode(),
                Quantity = value.Quantity,
                Side = value.Side.ToOrderSide(),
                QuantityFilled = value.QuantityFilled,
                QuantityRemaining = value.QuantityRemaining,
                Status = value.Status.ToOrderStatus(),
                ValueFilled = value.ValueFilled,
                ValueRemaining = value.ValueRemaining,
                ReduceOnly = value.ReduceOnly,
            };
        }

        public static OrderSide ToOrderSide(this Bybit.Net.Enums.OrderSide value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.OrderSide.Buy:
                    return OrderSide.Buy;
                case Bybit.Net.Enums.OrderSide.Sell:
                    return OrderSide.Sell;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static OrderPositionMode? ToPositionMode(this Bybit.Net.Enums.PositionMode? value)
        {
            if(value == null)
                return null;
            switch (value)
            {
                case Bybit.Net.Enums.PositionMode.OneWay:
                    return OrderPositionMode.OneWay;
                case Bybit.Net.Enums.PositionMode.BothSideBuy:
                    return OrderPositionMode.BothSideBuy;
                case Bybit.Net.Enums.PositionMode.BothSideSell:
                    return OrderPositionMode.BothSideSell;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static OrderStatus ToOrderStatus(this Bybit.Net.Enums.V5.OrderStatus value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.V5.OrderStatus.Created:
                    return OrderStatus.Created;
                case Bybit.Net.Enums.V5.OrderStatus.New:
                    return OrderStatus.New;
                case Bybit.Net.Enums.V5.OrderStatus.Rejected:
                    return OrderStatus.Rejected;
                case Bybit.Net.Enums.V5.OrderStatus.PartiallyFilled:
                    return OrderStatus.PartiallyFilled;
                case Bybit.Net.Enums.V5.OrderStatus.PartiallyFilledCanceled:
                    return OrderStatus.PartiallyFilledCanceled;
                case Bybit.Net.Enums.V5.OrderStatus.Filled:
                    return OrderStatus.Filled;
                case Bybit.Net.Enums.V5.OrderStatus.Cancelled:
                    return OrderStatus.Cancelled;
                case Bybit.Net.Enums.V5.OrderStatus.Untriggered:
                    return OrderStatus.Untriggered;
                case Bybit.Net.Enums.V5.OrderStatus.Triggered:
                    return OrderStatus.Triggered;
                case Bybit.Net.Enums.V5.OrderStatus.Deactivated:
                    return OrderStatus.Deactivated;
                case Bybit.Net.Enums.V5.OrderStatus.Active:
                    return OrderStatus.Active;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static Position? ToPosition(this Bybit.Net.Objects.Models.V5.BybitPosition value)
        {
            if (!value.AveragePrice.HasValue)
                return null;
            return new Position
            {
                AveragePrice = value.AveragePrice.Value,
                Quantity = value.Quantity,
                Side = value.Side.ToPositionSide(),
                Symbol = value.Symbol,
                TradeMode = value.TradeMode.ToTradeMode(),
            };
        }

        public static PositionSide ToPositionSide(this Bybit.Net.Enums.PositionSide value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.PositionSide.Buy:
                    return PositionSide.Buy;
                case Bybit.Net.Enums.PositionSide.Sell:
                    return PositionSide.Sell;
                case Bybit.Net.Enums.PositionSide.None:
                    return PositionSide.None;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static TradeMode ToTradeMode(this Bybit.Net.Enums.TradeMode value)
        {
            switch (value)
            {
                case Bybit.Net.Enums.TradeMode.CrossMargin:
                    return TradeMode.CrossMargin;
                case Bybit.Net.Enums.TradeMode.Isolated:
                    return TradeMode.Isolated;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public static Bybit.Net.Enums.V5.PositionMode ToBybitPositionMode(this PositionMode positionMode)
        {
            switch (positionMode)
            {
                case PositionMode.Hedge:
                    return Bybit.Net.Enums.V5.PositionMode.BothSides;
                case PositionMode.OneWay:
                    return Bybit.Net.Enums.V5.PositionMode.MergedSingle;
                default:
                    throw new ArgumentOutOfRangeException(nameof(positionMode), positionMode, null);
            }
        }

        public static Bybit.Net.Enums.TradeMode ToBybitTradeMode(this TradeMode tradeMode)
        {
            switch (tradeMode)
            {
                case TradeMode.CrossMargin:
                    return Bybit.Net.Enums.TradeMode.CrossMargin;
                case TradeMode.Isolated:
                    return Bybit.Net.Enums.TradeMode.Isolated;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tradeMode), tradeMode, null);
            }
        }
    }
}