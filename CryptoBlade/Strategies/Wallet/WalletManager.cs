using Bybit.Net.Enums;
using Bybit.Net.Interfaces.Clients;
using Bybit.Net.Objects.Models.V5;
using CryptoBlade.Helpers;
using CryptoBlade.Mapping;
using CryptoBlade.Strategies.Policies;
using CryptoExchange.Net.Sockets;

namespace CryptoBlade.Strategies.Wallet
{
    public class WalletManager : IWalletManager
    {
        private readonly IBybitRestClient m_bybitRestClient;
        private readonly IBybitSocketClient m_bybitSocketClient;
        private UpdateSubscription? m_walletSubscription;
        private CancellationTokenSource? m_cancellationTokenSource;
        private readonly ILogger<WalletManager> m_logger;
        private const string c_asset = Assets.QuoteAsset;
        private Task? m_initTask;

        public WalletManager(ILogger<WalletManager> logger,
            IBybitRestClient bybitRestClient, 
            IBybitSocketClient bybitSocketClient)
        {
            m_bybitRestClient = bybitRestClient;
            m_bybitSocketClient = bybitSocketClient;
            m_logger = logger;
            m_cancellationTokenSource = new CancellationTokenSource();
        }

        public Balance Contract { get; private set; }

        public Task StartAsync(CancellationToken cancel)
        {
            m_cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            m_initTask = Task.Run(async () =>
            {
                var subscription = await ExchangePolicies.RetryForever
                    .ExecuteAsync(async () =>
                    {
                        var subscriptionResult = await m_bybitSocketClient.V5PrivateApi
                            .SubscribeToWalletUpdatesAsync(OnWalletUpdate, m_cancellationTokenSource.Token);
                        if (subscriptionResult.GetResultOrError(out var data, out var error))
                            return data;
                        throw new InvalidOperationException(error.Message);
                    });
                subscription.AutoReconnect(m_logger);
                m_walletSubscription = subscription;

                var balance = await ExchangePolicies.RetryForever
                    .ExecuteAsync(async () =>
                    {
                        var balanceResult = await m_bybitRestClient.V5Api.Account.GetBalancesAsync(AccountType.Contract, null,
                            m_cancellationTokenSource.Token);
                        if (balanceResult.GetResultOrError(out var data, out var error))
                            return data;
                        throw new InvalidOperationException(error.Message);
                    });
                foreach (var b in balance.List)
                {
                    if (b.AccountType == AccountType.Contract)
                    {
                        var asset = b.Assets.FirstOrDefault(x => string.Equals(x.Asset, c_asset, StringComparison.OrdinalIgnoreCase));
                        if (asset != null)
                            Contract = asset.ToBalance();
                    }
                }
            }, cancel);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancel)
        {
            var walletSubscription = m_walletSubscription;
            if (walletSubscription != null)
                await walletSubscription.CloseAsync();
            m_walletSubscription = null;
            m_cancellationTokenSource?.Cancel();
            m_cancellationTokenSource?.Dispose();
        }

        private void OnWalletUpdate(DataEvent<IEnumerable<BybitBalance>> obj)
        {
            foreach (BybitBalance bybitBalance in obj.Data)
            {
                if (bybitBalance.AccountType == AccountType.Contract)
                {
                    var asset = bybitBalance.Assets.FirstOrDefault(x => string.Equals(x.Asset, c_asset, StringComparison.OrdinalIgnoreCase));
                    if (asset != null)
                        Contract = asset.ToBalance();
                }
            }
        }
    }
}