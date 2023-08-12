using CryptoBlade.Strategies.Wallet;

namespace CryptoBlade.Exchanges
{
    public interface ICbFuturesSocketClient
    {
        Task<IUpdateSubscription> SubscribeToWalletUpdatesAsync(Action<Balance> handler,
            CancellationToken cancel = default);
    }
}