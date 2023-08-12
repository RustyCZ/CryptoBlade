using Bybit.Net.Enums;
using Bybit.Net.Enums.V5;
using Bybit.Net.Interfaces.Clients;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Policies;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using OrderSide = Bybit.Net.Enums.OrderSide;
using OrderStatus = Bybit.Net.Enums.V5.OrderStatus;
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
                m_logger.LogInformation($"{symbol} Placing limit buy order for '{quantity}' @ '{price}'");
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
                        m_logger.LogInformation($"{symbol}: Buy order was cancelled. Adjusting price.");
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

                    m_logger.LogInformation($"{symbol} Buy order placed for '{quantity}' @ '{price}'");
                    return true;
                }

                m_logger.LogWarning($"{symbol} Error getting order status: {orderStatusRes.Error}");
                return false;
            }

            m_logger.LogInformation($"{symbol} could not place buy order.");

            return false;
        }

        public async Task<bool> PlaceLimitSellOrderAsync(string symbol, decimal quantity, decimal price,
            CancellationToken cancel = default)
        {
            for (int attempt = 0; attempt < m_options.Value.PlaceOrderAttempts; attempt++)
            {
                m_logger.LogInformation(
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
                        m_logger.LogInformation($"{symbol} Sell order was cancelled. Adjusting price.");
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

                    m_logger.LogInformation($"{symbol} Sell order placed for '{quantity}' @ '{price}'");
                    return true;
                }

                m_logger.LogWarning($"{symbol} Error getting order status: {orderStatusRes.Error}");
                return false;
            }

            m_logger.LogInformation($"{symbol} could not place sell order.");
            return false;
        }

        public async Task<bool> PlaceLongTakeProfitOrderAsync(string symbol, decimal qty, decimal price,
            CancellationToken cancel = default)
        {
            var sellOrderRes =
                await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits.ExecuteAsync(
                    async () =>
                        await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                            category: m_category,
                            symbol: symbol,
                            side: OrderSide.Sell,
                            type: NewOrderType.Limit,
                            quantity: qty,
                            price: price,
                            positionIdx: PositionIdx.BuyHedgeMode,
                            reduceOnly: true,
                            timeInForce: TimeInForce.PostOnly,
                            ct: cancel));
            if (sellOrderRes.GetResultOrError(out _, out var error))
                return true;
            m_logger.LogWarning($"{symbol} Failed to place long take profit order: {error}");
            return false;
        }

        public async Task<bool> PlaceShortTakeProfitOrderAsync(string symbol, decimal qty, decimal price,
            CancellationToken cancel = default)
        {
            var buyOrderRes = await ExchangePolicies<Bybit.Net.Objects.Models.V5.BybitOrderId>.RetryTooManyVisits.ExecuteAsync(async () =>
                await m_bybitRestClient.V5Api.Trading.PlaceOrderAsync(
                    category: m_category,
                    symbol: symbol,
                    side: OrderSide.Buy,
                    type: NewOrderType.Limit,
                    quantity: qty,
                    price: price,
                    positionIdx: PositionIdx.SellHedgeMode,
                    reduceOnly: true,
                    timeInForce: TimeInForce.PostOnly,
                    ct: cancel));
            if (buyOrderRes.GetResultOrError(out _, out var error))
                return true;
            m_logger.LogWarning($"{symbol} Failed to place short take profit order: {error}");

            return false;
        }

        public async Task<Balance> GetBalancesAsync(CancellationToken cancel = default)
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

            return new Balance();
        }
    }
}