using Bybit.Net.Interfaces.Clients;
using CryptoBlade.Configuration;
using CryptoBlade.Strategies.Common;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Strategies
{
    public class TradingStrategyFactory : ITradingStrategyFactory
    {
        private readonly IWalletManager m_walletManager;
        private readonly IBybitRestClient m_bybitRestClient;

        public TradingStrategyFactory(IWalletManager walletManager, IBybitRestClient bybitRestClient)
        {
            m_walletManager = walletManager;
            m_bybitRestClient = bybitRestClient;
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
            var options = Options.Create(new AutoHedgeStrategyOptions
            {
                MinimumPriceDistance = config.MinimumPriceDistance,
                MinimumVolume = config.MinimumVolume,
                DcaOrdersCount = config.DcaOrdersCount,
                WalletExposureLong = config.WalletExposureLong,
                WalletExposureShort = config.WalletExposureShort,
                ForceMinQty = config.ForceMinQty,
                PlaceOrderAttempts = config.PlaceOrderAttempts,
                TradingMode = GetTradingMode(config, symbol)
            });
            return new AutoHedgeStrategy(options, symbol, m_walletManager, m_bybitRestClient);
        }

        private ITradingStrategy CreateMfiRsiCandlePreciseStrategy(TradingBotOptions config, string symbol)
        {
            var options = Options.Create(new MfiRsiCandlePreciseTradingStrategyOptions
            {
                MinimumPriceDistance = config.MinimumPriceDistance,
                MinimumVolume = config.MinimumVolume,
                DcaOrdersCount = config.DcaOrdersCount,
                WalletExposureLong = config.WalletExposureLong,
                WalletExposureShort = config.WalletExposureShort,
                ForceMinQty = config.ForceMinQty,
                PlaceOrderAttempts = config.PlaceOrderAttempts,
                TradingMode = GetTradingMode(config, symbol)
            });
            return new MfiRsiCandlePreciseTradingStrategy(options, symbol, m_walletManager, m_bybitRestClient);
        }

        private ITradingStrategy CreateMfiRsiEriTrendPreciseStrategy(TradingBotOptions config, string symbol)
        {
            var options = Options.Create(new MfiRsiEriTrendTradingStrategyOptions
            {
                MinimumPriceDistance = config.MinimumPriceDistance,
                MinimumVolume = config.MinimumVolume,
                DcaOrdersCount = config.DcaOrdersCount,
                WalletExposureLong = config.WalletExposureLong,
                WalletExposureShort = config.WalletExposureShort,
                ForceMinQty = config.ForceMinQty,
                PlaceOrderAttempts = config.PlaceOrderAttempts,
                TradingMode = GetTradingMode(config, symbol)
            });
            return new MfiRsiEriTrendTradingStrategy(options, symbol, m_walletManager, m_bybitRestClient);
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