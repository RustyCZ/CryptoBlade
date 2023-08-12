﻿using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Strategies
{
    public class TradingStrategyFactory : ITradingStrategyFactory
    {
        private readonly IWalletManager m_walletManager;
        private readonly ICbFuturesRestClient m_restClient;

        public TradingStrategyFactory(IWalletManager walletManager, ICbFuturesRestClient restClient)
        {
            m_walletManager = walletManager;
            m_restClient = restClient;
        }

        public ITradingStrategy CreateStrategy(TradingBotOptions config, string symbol)
        {
            string strategyName = config.StrategyName;
            if (string.Equals("AutoHedge", strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateAutoHedgeStrategy(config, symbol);

            if (string.Equals("MfiRsiCandlePrecise", strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateMfiRsiCandlePreciseStrategy(config, symbol);

            if (string.Equals("MfiRsiEriTrend", strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateMfiRsiEriTrendPreciseStrategy(config, symbol);

            return CreateAutoHedgeStrategy(config, symbol);
        }

        private ITradingStrategy CreateAutoHedgeStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<AutoHedgeStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                });
            return new AutoHedgeStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private ITradingStrategy CreateMfiRsiCandlePreciseStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<MfiRsiCandlePreciseTradingStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                });
            return new MfiRsiCandlePreciseTradingStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private ITradingStrategy CreateMfiRsiEriTrendPreciseStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<MfiRsiEriTrendTradingStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                });
            return new MfiRsiEriTrendTradingStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private IOptions<TOptions> CreateTradeOptions<TOptions>(TradingBotOptions config, string symbol, Action<TOptions> optionsSetup) 
            where TOptions : TradingStrategyBaseOptions, new()
        {
            var options = new TOptions
            {
                DcaOrdersCount = config.DcaOrdersCount,
                WalletExposureLong = config.WalletExposureLong,
                WalletExposureShort = config.WalletExposureShort,
                ForceMinQty = config.ForceMinQty,
                TradingMode = GetTradingMode(config, symbol),
                MaxAbsFundingRate = config.MaxAbsFundingRate,
                FeeRate = config.FeeRate,
                MinProfitRate = config.MinProfitRate,
            };
            optionsSetup(options);
            return Options.Create(options);
        }

        private TradingMode GetTradingMode(TradingBotOptions config, string symbol)
        {
            var tradingMode = config.SymbolTradingModes.FirstOrDefault(x =>
                string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if(tradingMode != null)
                return tradingMode.TradingMode;
            return config.TradingMode;
        }
    }
}