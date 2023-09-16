using CryptoBlade.Configuration;
using CryptoBlade.Services;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CryptoBlade.BackTesting
{
    public class BackTestDynamicTradingStrategyManager : DynamicTradingStrategyManager
    {
        private readonly BackTestExchange m_backTestExchange;
        private readonly IHostApplicationLifetime m_hostApplicationLifetime;

        public BackTestDynamicTradingStrategyManager(IOptions<TradingBotOptions> options, 
            ILogger<DynamicTradingStrategyManager> logger,
            BackTestExchange backTestExchange,
            ITradingStrategyFactory strategyFactory,
            IWalletManager walletManager, 
            IHostApplicationLifetime hostApplicationLifetime) 
            : base(options, logger, strategyFactory, backTestExchange, backTestExchange, walletManager)
        {
            m_backTestExchange = backTestExchange;
            m_hostApplicationLifetime = hostApplicationLifetime;
        }

        protected override async Task PreInitializationPhaseAsync(CancellationToken cancel)
        {
            await base.PreInitializationPhaseAsync(cancel);
            await m_backTestExchange.PrepareDataAsync(cancel);
        }

        protected override Task StrategyExecutionDataDelayAsync(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }

        protected override Task DelayBetweenEachSymbol(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }

        protected override Task SymbolInitializationCallDelay(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }

        protected override async Task<bool> StrategyExecutionNextStepAsync(CancellationToken cancel)
        {
            bool hasData = await m_backTestExchange.AdvanceTimeAsync(cancel);
            bool canContinue = true;
            if (!hasData)
            {
                m_hostApplicationLifetime.StopApplication();
                canContinue = false;
                return canContinue;
            }
            var balances = await m_backTestExchange.GetBalancesAsync(cancel);
            var spot = m_backTestExchange.SpotBalance;
            var totalBalance = spot;
            if (balances.WalletBalance.HasValue)
                totalBalance += balances.WalletBalance.Value;
            
            if (totalBalance <= 0)
            {
                canContinue = false;
                m_hostApplicationLifetime.StopApplication();
            }
            return canContinue;
        }

        protected override Task StrategyExecutionNextCycleDelayAsync(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }

        protected override Task<bool> ShouldLongThrottleAsync(CancellationToken cancel)
        {
            return Task.FromResult(false);
        }

        protected override Task<bool> ShouldShortThrottleAsync(CancellationToken cancel)
        {
            return Task.FromResult(false);
        }
    }
}
