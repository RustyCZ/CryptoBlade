using CryptoBlade.Models;
using CryptoBlade.Strategies.Wallet;

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

        Task<bool> PlaceMarketBuyOrderAsync(
            string symbol,
            decimal quantity,
            decimal price,
            CancellationToken cancel = default);

        Task<bool> PlaceMarketSellOrderAsync(
            string symbol,
            decimal quantity,
            decimal price,
            CancellationToken cancel = default);

        Task<bool> PlaceLongTakeProfitOrderAsync(
            string symbol,
            decimal qty,
            decimal price,
            bool force,
            CancellationToken cancel = default);

        Task<bool> PlaceShortTakeProfitOrderAsync(
            string symbol,
            decimal qty,
            decimal price,
            bool force,
            CancellationToken cancel = default);

        Task<Balance> GetBalancesAsync(CancellationToken cancel = default);

        Task<SymbolInfo[]> GetSymbolInfoAsync(CancellationToken cancel = default);

        Task<Candle[]> GetKlinesAsync(
            string symbol,
            TimeFrame interval,
            int limit, 
            CancellationToken cancel = default);

        Task<Candle[]> GetKlinesAsync(
            string symbol,
            TimeFrame interval,
            DateTime start,
            DateTime end,
            CancellationToken cancel = default);

        Task<Ticker> GetTickerAsync(string symbol, CancellationToken cancel = default);

        Task<Order[]> GetOrdersAsync(CancellationToken cancel = default);

        Task<Position[]> GetPositionsAsync(CancellationToken cancel = default);
    }
}
