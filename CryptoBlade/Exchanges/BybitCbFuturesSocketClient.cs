using Bybit.Net.Enums;
using Bybit.Net.Interfaces.Clients;
using CryptoBlade.Strategies.Policies;
using CryptoBlade.Strategies.Wallet;
using Bybit.Net.Objects.Models.V5;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoExchange.Net.CommonObjects;

namespace CryptoBlade.Exchanges
{
    public class BybitCbFuturesSocketClient : ICbFuturesSocketClient
    {
        private readonly IBybitSocketClient m_bybitSocketClient;
        private const string c_asset = Assets.QuoteAsset;

        public BybitCbFuturesSocketClient(IBybitSocketClient bybitSocketClient)
        {
            m_bybitSocketClient = bybitSocketClient;
        }

        public async Task<IUpdateSubscription> SubscribeToWalletUpdatesAsync(Action<Strategies.Wallet.Balance> handler, CancellationToken cancel = default)
        {
            var subscription = await ExchangePolicies.RetryForever
                .ExecuteAsync(async () =>
                {
                    var subscriptionResult = await m_bybitSocketClient.V5PrivateApi
                        .SubscribeToWalletUpdatesAsync(walletUpdateEvent =>
                    {
                        foreach (BybitBalance bybitBalance in walletUpdateEvent.Data)
                        {
                            if (bybitBalance.AccountType == AccountType.Contract)
                            {
                                var asset = bybitBalance.Assets.FirstOrDefault(x => string.Equals(x.Asset, c_asset, StringComparison.OrdinalIgnoreCase));
                                if (asset != null)
                                {
                                    var contractBalance = asset.ToBalance();
                                    handler(contractBalance);
                                }
                            }
                        }
                    }, cancel);
                    if (subscriptionResult.GetResultOrError(out var data, out var error))
                        return data;
                    throw new InvalidOperationException(error.Message);
                });
            return new BybitUpdateSubscription(subscription);
        }

        public async Task<IUpdateSubscription> SubscribeToOrderUpdatesAsync(Action<OrderUpdate> handler, CancellationToken cancel = default)
        {
            var orderUpdateSubscription = await ExchangePolicies.RetryForever
                .ExecuteAsync(async () =>
                {
                    var subscriptionResult = await m_bybitSocketClient.V5PrivateApi.SubscribeToOrderUpdatesAsync(
                        orderUpdateEvent =>
                        {
                            foreach (BybitOrderUpdate bybitOrderUpdate in orderUpdateEvent.Data)
                            {
                                if (bybitOrderUpdate.Category != Category.Linear)
                                    continue;
                                var orderUpdate = bybitOrderUpdate.ToOrderUpdate();
                                handler(orderUpdate);
                            }
                        }, cancel);
                    if (subscriptionResult.GetResultOrError(out var data, out var error))
                        return data;
                    throw new InvalidOperationException(error.Message);
                });

            return new BybitUpdateSubscription(orderUpdateSubscription);
        }

        public async Task<IUpdateSubscription> SubscribeToKlineUpdatesAsync(string[] symbols, TimeFrame timeFrame, Action<string, Candle> handler,
            CancellationToken cancel = default)
        {
            var klineUpdatesSubscription = await ExchangePolicies.RetryForever
                .ExecuteAsync(async () =>
                {
                    var subscriptionResult = await m_bybitSocketClient.V5LinearApi.SubscribeToKlineUpdatesAsync(
                        symbols,
                        timeFrame.ToKlineInterval(),
                        klineUpdateEvent =>
                        {
                            if (string.IsNullOrEmpty(klineUpdateEvent.Topic))
                                return;
                            string[] topicParts = klineUpdateEvent.Topic.Split('.');
                            if (topicParts.Length != 2)
                                return;
                            string symbol = topicParts[1];
                            foreach (BybitKlineUpdate bybitKlineUpdate in klineUpdateEvent.Data)
                            {
                                if (!bybitKlineUpdate.Confirm)
                                    continue;
                                var candle = bybitKlineUpdate.ToCandle();
                                handler(symbol, candle);
                            }
                        },
                        cancel);
                    if (subscriptionResult.GetResultOrError(out var data, out var error))
                        return data;
                    throw new InvalidOperationException(error.Message);
                });

            return new BybitUpdateSubscription(klineUpdatesSubscription);
        }

        public async Task<IUpdateSubscription> SubscribeToTickerUpdatesAsync(string[] symbols, Action<string, Models.Ticker> handler, CancellationToken cancel = default)
        {
            var tickerSubscription = await ExchangePolicies.RetryForever.ExecuteAsync(async () =>
            {
                var tickerSubscriptionResult = await m_bybitSocketClient.V5LinearApi
                    .SubscribeToTickerUpdatesAsync(symbols,
                        tickerUpdateEvent =>
                        {
                            var ticker = tickerUpdateEvent.Data.ToTicker();
                            if (ticker == null)
                                return;
                            handler(tickerUpdateEvent.Data.Symbol, ticker);
                        }, 
                        cancel);
                if (tickerSubscriptionResult.GetResultOrError(out var data, out var error))
                    return data;
                throw new InvalidOperationException(error.Message);
            });

            return new BybitUpdateSubscription(tickerSubscription);
        }
    }
}