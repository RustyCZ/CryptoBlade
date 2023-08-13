using CryptoBlade.Strategies.Wallet;

namespace CryptoBlade.Services
{
    public class TradingHostedService : IHostedService
    {
        private readonly ITradeStrategyManager m_strategyManager;
        private readonly IWalletManager m_walletManager;

        public TradingHostedService(ITradeStrategyManager strategyManager, IWalletManager walletManager)
        {
            m_strategyManager = strategyManager;
            m_walletManager = walletManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await m_walletManager.StartAsync(cancellationToken);
            await m_strategyManager.StartStrategiesAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await m_walletManager.StopAsync(cancellationToken);
            await m_strategyManager.StopStrategiesAsync(cancellationToken);
        }
    }
}
