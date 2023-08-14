using Bybit.Net.Enums;
using Bybit.Net.Enums.V5;
using Bybit.Net.Interfaces.Clients;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Policies;
using CryptoBlade.Strategies.Wallet;
using CryptoExchange.Net.CommonObjects;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Order = CryptoBlade.Models.Order;
using OrderSide = Bybit.Net.Enums.OrderSide;
using OrderStatus = Bybit.Net.Enums.V5.OrderStatus;
using Position = CryptoBlade.Models.Position;
using PositionMode = CryptoBlade.Models.PositionMode;
using TradeMode = CryptoBlade.Models.TradeMode;

namespace CryptoBlade.Exchanges
{
    public class BybitCbFuturesRestClient : ICbFuturesRestClient
    {
        private readonly IBybitRestClient m_bybitRestClient;
        private readonly Category m_category;
        private readonly ILogger<BybitCbFuturesRestClient> m_logger;
        private readonly IOptions<BybitCbFuturesRestClientOptions> m_options;

        public BybitCbFuturesRestClient(IOptions<BybitCbFuturesRestClientOptions> options,
            IBybitRestClient bybitRestClient,
            ILogger<BybitCbFuturesRestClient> logger)
        {
            m_options = options;
            m_category = Category.Linear;
            m_bybitRestClient = bybitRestClient;
            m_logger = logger;
        }


        public async Task<bool> SetLeverageAsync(SymbolInfo symbol,
            CancellationToken cancel = default)
        {
            if (!symbol.MaxLeverage.HasValue)
            {
                m_logger.LogError($"Failed to setup leverage. Max leverage is not set for {symbol.Name}");
                return false;
            }

            var leverageRes = await ExchangePolicies.RetryTooManyVisits.ExecuteAsync(async () =>
            {
                var leverageRes = await m_bybitRestClient.V5Api.Account
                    .SetLeverageAsync(
                        m_category,
                        symbol.Name,
                        symbol.MaxLeverage.Value,
                        symbol.MaxLeverage.Value,
                        cancel);
                return leverageRes;
            });
            bool leverageOk = leverageRes.Success || leverageRes.Error != null &&
                leverageRes.Error.Code == (int)BybitErrorCodes.LeverageNotChanged;
            if (!leverageOk)
                m_logger.LogError($"Failed to setup leverage. {leverageRes.Error?.Message}");

            return leverageOk;
        }

        public async Task<bool> SwitchPositionModeAsync(PositionMode mode, string symbol,
            CancellationToken cancel = default)
        {
            var modeChange = await ExchangePolicies.RetryTooManyVisits.ExecuteAsync(async () =>
            {
                var modeChange = await m_bybitRestClient.V5Api.Account.SwitchPositionModeAsync(
                    m_category,
                    mode.ToBybitPositionMode(),
                    symbol,
                    null,
                    cancel);
                return modeChange;
            });

            bool modeOk = modeChange.Success || modeChange.Error != null &&
                modeChange.Error.Code == (int)BybitErrorCodes.PositionModeNotChanged;
            if (!modeOk)
                m_logger.LogError($"Failed to setup position mode. {modeChange.Error?.Message}");

            return modeOk;
        }

        public async Task<bool> SwitchCrossIsolatedMarginAsync(SymbolInfo symbol, 
            TradeMode tradeMode, 
            CancellationToken cancel = default)
        {
            if (!symbol.MaxLeverage.HasValue)
            {
                m_logger.LogError($"Failed to setup cross mode. Max leverage is not set for {symbol.Name}");
                return false;
            }

            var crossMode = await ExchangePolicies.RetryTooManyVisits.ExecuteAsync(async () =>
            {
                var crossModeResult = await m_bybitRestClient.V5Api.Account.SwitchCrossIsolatedMarginAsync(
                    m_category,
                    symbol.Name,
                    tradeMode.ToBybitTradeMode(),
                    symbol.MaxLeverage.Value,
                    symbol.MaxLeverage.Value,
                    cancel);
                return crossModeResult;
            });
            bool crossModeOk = crossMode.Success || crossMode.Error != null &&
                crossMode.Error.Code == (int)BybitErrorCodes.CrossModeNotModified;
            if (!crossModeOk)
                m_logger.LogError($"Failed to setup cross mode. {crossMode.Error?.Message}");

            return crossModeOk;
        }

        public async Task<bool> CancelOrderAsync(string symbol, string orderId, CancellationToken cancel = default)
        {
            var cancelOrder = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits
                .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading
                    .CancelOrderAsync(m_category, symbol, orderId, null, null, cancel));
            if (cancelOrder.GetResultOrError(out _, out var error))
                return true;
            m_logger.LogError($"{symbol}: Error canceling order: {error}");

            return false;
        }

        public async Task<bool> PlaceLimitBuyOrderAsync(string symbol, decimal quantity, decimal price,
            CancellationToken cancel = default)
        {
            for (int attempt = 0; attempt < m_options.Value.PlaceOrderAttempts; attempt++)
            {
                m_logger.LogDebug($"{symbol} Placing limit buy order for '{quantity}' @ '{price}'");
                var buyOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                        category: m_category,
                        symbol: symbol,
                        side: OrderSide.Buy,
                        type: NewOrderType.Limit,
                        quantity: quantity,
                        price: price,
                        positionIdx: PositionIdx.BuyHedgeMode,
                        reduceOnly: false,
                        timeInForce: TimeInForce.PostOnly,
                        ct: cancel));
                if (!buyOrderRes.GetResultOrError(out var buyOrder, out _))
                    continue;
                var orderStatusRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrder>
                    .RetryTooManyVisitsBybitResponse
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                        category: m_category,
                        symbol: symbol,
                        orderId: buyOrder.OrderId,
                        ct: cancel));
                if (orderStatusRes.GetResultOrError(out var orderStatus, out _))
                {
                    var order = orderStatus.List
                        .FirstOrDefault(x => string.Equals(x.OrderId, buyOrder.OrderId, StringComparison.Ordinal));
                    if (order != null && order.Status == OrderStatus.Cancelled)
                    {
                        m_logger.LogDebug($"{symbol}: Buy order was cancelled. Adjusting price.");
                        var orderBook = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderbook>
                            .RetryTooManyVisits
                            .ExecuteAsync(async () =>
                                await m_bybitRestClient.V5Api.ExchangeData.GetOrderbookAsync(m_category,
                                    symbol,
                                    limit: 1, cancel));
                        if (orderBook.GetResultOrError(out var orderBookData, out _))
                        {
                            var bestBid = orderBookData.Bids.FirstOrDefault();
                            if (bestBid != null)
                            {
                                price = bestBid.Price;
                            }
                        }

                        continue;
                    }

                    m_logger.LogDebug($"{symbol} Buy order placed for '{quantity}' @ '{price}'");
                    return true;
                }

                m_logger.LogWarning($"{symbol} Error getting order status: {orderStatusRes.Error}");
                return false;
            }

            m_logger.LogDebug($"{symbol} could not place buy order.");

            return false;
        }

        public async Task<bool> PlaceLimitSellOrderAsync(string symbol, decimal quantity, decimal price,
            CancellationToken cancel = default)
        {
            for (int attempt = 0; attempt < m_options.Value.PlaceOrderAttempts; attempt++)
            {
                m_logger.LogDebug(
                    $"{symbol} Placing limit sell order for '{quantity}' @ '{price}' attempt: {attempt}");
                var sellOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                        category: m_category,
                        symbol: symbol,
                        side: OrderSide.Sell,
                        type: NewOrderType.Limit,
                        quantity: quantity,
                        price: price,
                        positionIdx: PositionIdx.SellHedgeMode,
                        reduceOnly: false,
                        timeInForce: TimeInForce.PostOnly,
                        ct: cancel));
                if (!sellOrderRes.GetResultOrError(out var sellOrder, out _))
                    continue;
                var orderStatusRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrder>
                    .RetryTooManyVisitsBybitResponse
                    .ExecuteAsync(async () => await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                        category: m_category,
                        symbol: symbol,
                        orderId: sellOrder.OrderId,
                        ct: cancel));
                if (orderStatusRes.GetResultOrError(out var orderStatus, out _))
                {
                    var order = orderStatus.List
                        .FirstOrDefault(x => string.Equals(x.OrderId, sellOrder.OrderId, StringComparison.Ordinal));
                    if (order != null && order.Status == OrderStatus.Cancelled)
                    {
                        m_logger.LogDebug($"{symbol} Sell order was cancelled. Adjusting price.");
                        var orderBook = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderbook>
                            .RetryTooManyVisits
                            .ExecuteAsync(async () =>
                                await m_bybitRestClient.V5Api.ExchangeData.GetOrderbookAsync(m_category,
                                    symbol,
                                    limit: 1, cancel));
                        if (orderBook.GetResultOrError(out var orderBookData, out _))
                        {
                            var bestAsk = orderBookData.Asks.FirstOrDefault();
                            if (bestAsk != null)
                            {
                                price = bestAsk.Price;
                            }
                        }

                        continue;
                    }

                    m_logger.LogDebug($"{symbol} Sell order placed for '{quantity}' @ '{price}'");
                    return true;
                }

                m_logger.LogWarning($"{symbol} Error getting order status: {orderStatusRes.Error}");
                return false;
            }

            m_logger.LogDebug($"{symbol} could not place sell order.");
            return false;
        }

        public async Task<bool> PlaceLongTakeProfitOrderAsync(string symbol, decimal qty, decimal price, bool force,
            CancellationToken cancel = default)
        {
            var sellOrderRes =
                await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits.ExecuteAsync(
                    async () =>
                        await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                            category: m_category,
                            symbol: symbol,
                            side: OrderSide.Sell,
                            type: force ? NewOrderType.Market : NewOrderType.Limit,
                            quantity: qty,
                            price: price,
                            positionIdx: PositionIdx.BuyHedgeMode,
                            reduceOnly: true,
                            timeInForce: force ? TimeInForce.GoodTillCanceled : TimeInForce.PostOnly,
                            ct: cancel));
            if (sellOrderRes.GetResultOrError(out _, out var error))
                return true;
            m_logger.LogWarning($"{symbol} Failed to place long take profit order: {error}");
            return false;
        }

        public async Task<bool> PlaceShortTakeProfitOrderAsync(string symbol, decimal qty, decimal price, bool force,
            CancellationToken cancel = default)
        {
            var buyOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits.ExecuteAsync(async () =>
                await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                    category: m_category,
                    symbol: symbol,
                    side: OrderSide.Buy,
                    type: force ? NewOrderType.Market : NewOrderType.Limit,
                    quantity: qty,
                    price: price,
                    positionIdx: PositionIdx.SellHedgeMode,
                    reduceOnly: true,
                    timeInForce: force ? TimeInForce.GoodTillCanceled : TimeInForce.PostOnly,
                    ct: cancel));
            if (buyOrderRes.GetResultOrError(out _, out var error))
                return true;
            m_logger.LogWarning($"{symbol} Failed to place short take profit order: {error}");

            return false;
        }

        public async Task<Strategies.Wallet.Balance> GetBalancesAsync(CancellationToken cancel = default)
        {
            var balance = await ExchangePolicies.RetryForever
                .ExecuteAsync(async () =>
                {
                    var balanceResult = await m_bybitRestClient.V5Api.Account.GetBalancesAsync(AccountType.Contract, null,
                        cancel);
                    if (balanceResult.GetResultOrError(out var data, out var error))
                        return data;
                    throw new InvalidOperationException(error.Message);
                });
            foreach (var b in balance.List)
            {
                if (b.AccountType == AccountType.Contract)
                {
                    var asset = b.Assets.FirstOrDefault(x => string.Equals(x.Asset, Assets.QuoteAsset, StringComparison.OrdinalIgnoreCase));
                    if (asset != null)
                    {
                        var contract = asset.ToBalance();
                        return contract;
                    }
                }
            }

            return new Strategies.Wallet.Balance();
        }

        public async Task<SymbolInfo[]> GetSymbolInfoAsync(CancellationToken cancel = default)
        {
            var symbolData = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                List<SymbolInfo> symbolInfo = new List<SymbolInfo>();
                string? cursor = null;
                while (true)
                {
                    var symbolsResult = await m_bybitRestClient.V5Api.ExchangeData.GetLinearInverseSymbolsAsync(
                        m_category,
                        null,
                        null,
                        null,
                        cursor,
                        cancel);
                    if (!symbolsResult.GetResultOrError(out var data, out var error))
                        throw new InvalidOperationException(error.Message);
                    var s = data.List
                        .Where(x => string.Equals(Assets.QuoteAsset, x.QuoteAsset))
                        .Select(x => x.ToSymbolInfo());
                    symbolInfo.AddRange(s);
                    if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                        break;
                    cursor = data.NextPageCursor;
                }

                return symbolInfo.ToArray();
            });

            return symbolData;
        }

        public async Task<Candle[]> GetKlinesAsync(
            string symbol, 
            TimeFrame interval,
            int limit, 
            CancellationToken cancel = default)
        {
            var candles = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                var dataResponse = await m_bybitRestClient.V5Api.ExchangeData.GetKlinesAsync(
                    m_category,
                    symbol,
                    interval.ToKlineInterval(),
                    null,
                    null,
                    limit,
                    cancel);
                if (!dataResponse.GetResultOrError(out var data, out var error))
                {
                    throw new InvalidOperationException(error.Message);
                }

                // we don't want the last candle, because it's not closed yet
                var candleData = data.List.Skip(1).Reverse().Select(x => x.ToCandle(interval))
                    .ToArray();
                return candleData;
            });

            return candles;
        }

        public async Task<Candle[]> GetKlinesAsync(string symbol, TimeFrame interval, DateTime start, DateTime end,
            CancellationToken cancel = default)
        {
            var candles = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                var dataResponse = await m_bybitRestClient.V5Api.ExchangeData.GetKlinesAsync(
                    m_category,
                    symbol,
                    interval.ToKlineInterval(),
                    start,
                    end,
                    1000,
                    cancel);
                if (!dataResponse.GetResultOrError(out var data, out var error))
                {
                    throw new InvalidOperationException(error.Message);
                }

                // we don't want the last candle, because it's not closed yet
                var candleData = data.List.Reverse().Select(x => x.ToCandle(interval))
                    .ToArray();
                return candleData;
            });

            return candles;
        }

        public async Task<Models.Ticker> GetTickerAsync(string symbol, CancellationToken cancel = default)
        {
            var priceData = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                var priceDataRes = await m_bybitRestClient.V5Api.ExchangeData.GetLinearInverseTickersAsync(
                    m_category,
                    symbol, 
                    null,
                    null, 
                    cancel);
                if (priceDataRes.GetResultOrError(out var data, out var error))
                {
                    return data.List;
                }

                throw new InvalidOperationException(error.Message);
            });

            var ticker = priceData.Select(x => x.ToTicker()).First();

            return ticker;
        }

        public async Task<Order[]> GetOrdersAsync(CancellationToken cancel = default)
        {
            var orders = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                List<Order> orders = new List<Order>();
                string? cursor = null;
                while (true)
                {
                    var ordersResult = await m_bybitRestClient.V5Api.Trading.GetOrdersAsync(
                        m_category,
                        settleAsset: Assets.QuoteAsset,
                        cursor: cursor,
                        ct: cancel);
                    if (!ordersResult.GetResultOrError(out var data, out var error))
                        throw new InvalidOperationException(error.Message);
                    orders.AddRange(data.List.Select(x => x.ToOrder()));
                    if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                        break;
                    cursor = data.NextPageCursor;
                }

                return orders.ToArray();
            });

            return orders;
        }

        public async Task<Position[]> GetPositionsAsync(CancellationToken cancel = default)
        {
            var positions = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                List<Position> positions = new List<Position>();
                string? cursor = null;
                while (true)
                {
                    var positionResult = await m_bybitRestClient.V5Api.Trading.GetPositionsAsync(
                        m_category,
                        settleAsset: Assets.QuoteAsset,
                        cursor: cursor,
                        ct: cancel);
                    if (!positionResult.GetResultOrError(out var data, out var error))
                        throw new InvalidOperationException(error.Message);
                    foreach (var bybitPosition in data.List)
                    {
                        var position = bybitPosition.ToPosition();
                        if (position == null)
                            m_logger.LogWarning($"Could not convert position for symbol: {bybitPosition.Symbol}");
                        else
                            positions.Add(position);
                    }

                    if (string.IsNullOrWhiteSpace(data.NextPageCursor))
                        break;
                    cursor = data.NextPageCursor;
                }

                return positions.ToArray();
            });

            return positions;
        }
    }
}