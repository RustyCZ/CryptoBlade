using Bybit.Net.Enums;
using Bybit.Net.Interfaces.Clients;
using CryptoBlade.Strategies.Policies;
using CryptoBlade.Strategies.Wallet;
using Bybit.Net.Objects.Models.V5;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;

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

        public async Task<IUpdateSubscription> SubscribeToWalletUpdatesAsync(Action<Balance> handler, CancellationToken cancel = default)
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
    }
}