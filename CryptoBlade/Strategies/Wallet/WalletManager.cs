using CryptoBlade.Exchanges;

namespace CryptoBlade.Strategies.Wallet
{
    public class WalletManager : IWalletManager
    {
        private readonly ICbFuturesRestClient m_restClient;
        private readonly ICbFuturesSocketClient m_socketClient;
        private IUpdateSubscription? m_walletSubscription;
        private CancellationTokenSource? m_cancellationTokenSource;
        private readonly ILogger<WalletManager> m_logger;
        private Task? m_initTask;

        public WalletManager(ILogger<WalletManager> logger,
            ICbFuturesRestClient restClient,
            ICbFuturesSocketClient socketClient)
        {
            m_restClient = restClient;
            m_socketClient = socketClient;
            m_logger = logger;
            m_cancellationTokenSource = new CancellationTokenSource();
        }

        public Balance Contract { get; private set; }

        public Task StartAsync(CancellationToken cancel)
        {
            m_cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            m_initTask = Task.Run(async () =>
            {
                var subscription = await m_socketClient.SubscribeToWalletUpdatesAsync(OnWalletUpdate, m_cancellationTokenSource.Token);
                subscription.AutoReconnect(m_logger);
                m_walletSubscription = subscription;

                Contract = await m_restClient.GetBalancesAsync(cancel);

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

        private void OnWalletUpdate(Balance obj)
        {
            Contract = obj;
        }
    }
}