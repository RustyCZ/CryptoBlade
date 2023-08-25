using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
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

            if (string.Equals("LinearRegression", strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateLinearRegressionStrategy(config, symbol);

            if (string.Equals("Tartaglia", strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateTartagliaStrategy(config, symbol);

            if (string.Equals("Mona", strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateMonaStrategy(config, symbol);

            return CreateAutoHedgeStrategy(config, symbol);
        }

        private ITradingStrategy CreateAutoHedgeStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<AutoHedgeStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                    strategyOptions.MinReentryPositionDistance = config.Strategies.AutoHedge.MinReentryPositionDistance;
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

        private ITradingStrategy CreateLinearRegressionStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<LinearRegressionStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                    strategyOptions.ChannelLength = config.Strategies.LinearRegression.ChannelLength;
                    strategyOptions.StandardDeviation = config.Strategies.LinearRegression.StandardDeviation;
                });
            return new LinearRegressionStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private ITradingStrategy CreateTartagliaStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<TartagliaStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                    strategyOptions.ChannelLength = config.Strategies.Tartaglia.ChannelLength;
                    strategyOptions.StandardDeviation = config.Strategies.Tartaglia.StandardDeviation;
                    strategyOptions.MinReentryPositionDistance = config.Strategies.Tartaglia.MinReentryPositionDistance;
                });
            return new TartagliaStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private ITradingStrategy CreateMonaStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<MonaStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                    strategyOptions.BandwidthCoefficient = config.Strategies.Mona.BandwidthCoefficient;
                    strategyOptions.MinReentryPositionDistanceLong = config.Strategies.Mona.MinReentryPositionDistanceLong;
                    strategyOptions.MinReentryPositionDistanceShort = config.Strategies.Mona.MinReentryPositionDistanceShort;
                    strategyOptions.ClusteringLength = config.Strategies.Mona.ClusteringLength;
                });
            return new MonaStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private IOptions<TOptions> CreateTradeOptions<TOptions>(TradingBotOptions config, string symbol, Action<TOptions> optionsSetup) 
            where TOptions : TradingStrategyBaseOptions, new()
        {
            bool isBackTest = config.IsBackTest();
            int initialUntradableDays = isBackTest ? config.BackTest.InitialUntradableDays : 0;
            var options = new TOptions
            {
                DcaOrdersCount = config.DcaOrdersCount,
                WalletExposureLong = config.WalletExposureLong,
                WalletExposureShort = config.WalletExposureShort,
                ForceMinQty = config.ForceMinQty,
                TradingMode = GetTradingMode(config, symbol),
                MaxAbsFundingRate = config.MaxAbsFundingRate,
                FeeRate = config.MakerFeeRate,
                MinProfitRate = config.MinProfitRate,
                ForceUnstuckPercentStep = config.Unstucking.ForceUnstuckPercentStep,
                SlowUnstuckPercentStep = config.Unstucking.SlowUnstuckPercentStep,
                InitialUntradableDays = initialUntradableDays,
                QtyFactor = config.QtyFactor,
                EnableRecursiveQtyFactor = config.EnableRecursiveQtyFactor,
                IgnoreInconsistency = isBackTest,
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