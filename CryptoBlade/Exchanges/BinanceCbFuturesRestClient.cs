using Binance.Net.Interfaces.Clients;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Wallet;
using PositionMode = CryptoBlade.Models.PositionMode;

namespace CryptoBlade.Exchanges
{
    public class BinanceCbFuturesRestClient : ICbFuturesRestClient
    {
        private readonly IBinanceRestClient m_binanceRestClient;
        private readonly ILogger<BinanceCbFuturesRestClient> m_logger;

        public BinanceCbFuturesRestClient(ILogger<BinanceCbFuturesRestClient> logger,
            IBinanceRestClient binanceRestClient)
        {
            m_binanceRestClient = binanceRestClient;
            m_logger = logger;
        }

        public Task<bool> SetLeverageAsync(SymbolInfo symbol, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SwitchPositionModeAsync(PositionMode mode, string symbol, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SwitchCrossIsolatedMarginAsync(SymbolInfo symbol, TradeMode tradeMode, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CancelOrderAsync(string symbol, string orderId, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PlaceLimitBuyOrderAsync(string symbol, decimal quantity, decimal price, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PlaceLimitSellOrderAsync(string symbol, decimal quantity, decimal price, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PlaceMarketBuyOrderAsync(string symbol, decimal quantity, decimal price, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PlaceMarketSellOrderAsync(string symbol, decimal quantity, decimal price, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PlaceLongTakeProfitOrderAsync(string symbol, decimal qty, decimal price, bool force,
            CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PlaceShortTakeProfitOrderAsync(string symbol, decimal qty, decimal price, bool force,
            CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<Balance> GetBalancesAsync(CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<SymbolInfo[]> GetSymbolInfoAsync(CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<Candle[]> GetKlinesAsync(string symbol, TimeFrame interval, int limit, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public async Task<Candle[]> GetKlinesAsync(string symbol, TimeFrame interval, DateTime start, DateTime end,
            CancellationToken cancel = default)
        {
            var dataResponse = await m_binanceRestClient.UsdFuturesApi.ExchangeData.GetKlinesAsync(
                symbol,
                interval.ToBinanceKlineInterval(),
                start,
                end,
                1000,
                cancel);
            if (!dataResponse.GetResultOrError(out var data, out var error))
            {
                m_logger.LogError(error.Message);
                throw new InvalidOperationException(error.Message);
            }
                
            var candles = data.Select(x => x.ToCandle(interval)).ToArray();
            return candles;
        }

        public Task<Ticker> GetTickerAsync(string symbol, CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<Order[]> GetOrdersAsync(CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }

        public Task<Position[]> GetPositionsAsync(CancellationToken cancel = default)
        {
            throw new NotImplementedException();
        }
    }
}
