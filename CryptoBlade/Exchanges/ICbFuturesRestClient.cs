using Bybit.Net.Enums;
using CryptoExchange.Net.Objects;
using System;
using CryptoBlade.Models;

namespace CryptoBlade.Exchanges
{
    public interface ICbFuturesRestClient
    {
        Task<bool> SetLeverageAsync(
            SymbolInfo symbol,
            CancellationToken cancel = default);

        Task<bool> SwitchPositionModeAsync(
            Models.PositionMode mode,
            string symbol,
            CancellationToken cancel = default);

        Task<bool> SwitchCrossIsolatedMarginAsync(
            SymbolInfo symbol,
            Models.TradeMode tradeMode,
            CancellationToken cancel = default);

        Task<bool> CancelOrderAsync(
            string symbol,
            string orderId,
            CancellationToken cancel = default);

        Task<bool> PlaceLimitBuyOrderAsync(
            string symbol, 
            decimal quantity, 
            decimal price, 
            CancellationToken cancel = default);

        Task<bool> PlaceLimitSellOrderAsync(
            string symbol,
            decimal quantity,
            decimal price,
            CancellationToken cancel = default);

        Task<bool> PlaceLongTakeProfitOrderAsync(
            string symbol,
            decimal qty,
            decimal price,
            CancellationToken cancel = default);

        Task<bool> PlaceShortTakeProfitOrderAsync(
            string symbol,
            decimal qty,
            decimal price,
            CancellationToken cancel = default);
    }
}
