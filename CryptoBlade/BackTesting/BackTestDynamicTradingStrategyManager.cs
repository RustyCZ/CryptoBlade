﻿using CryptoBlade.Configuration;
using CryptoBlade.Services;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;

namespace CryptoBlade.BackTesting
{
    public class BackTestDynamicTradingStrategyManager : DynamicTradingStrategyManager
    {
        private readonly BackTestExchange m_backTestExchange;

        public BackTestDynamicTradingStrategyManager(IOptions<TradingBotOptions> options, 
            ILogger<DynamicTradingStrategyManager> logger,
            BackTestExchange backTestExchange,
            ITradingStrategyFactory strategyFactory,
            IWalletManager walletManager) 
            : base(options, logger, strategyFactory, backTestExchange, backTestExchange, walletManager)
        {
            m_backTestExchange = backTestExchange;
        }

        protected override async Task PreInitializationPhaseAsync(CancellationToken cancel)
        {
            await base.PreInitializationPhaseAsync(cancel);
            await m_backTestExchange.PrepareDataAsync(cancel);
        }

        protected override Task StrategyExecutionDataDelay(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }

        protected override async Task StrategyExecutionNextStep(CancellationToken cancel)
        {
            await m_backTestExchange.AdvanceTimeAsync(cancel);
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