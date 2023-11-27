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
            if (string.Equals(StrategyNames.AutoHedge, strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateAutoHedgeStrategy(config, symbol);

            if (string.Equals(StrategyNames.MfiRsiCandlePrecise, strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateMfiRsiCandlePreciseStrategy(config, symbol);

            if (string.Equals(StrategyNames.MfiRsiEriTrend, strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateMfiRsiEriTrendPreciseStrategy(config, symbol);

            if (string.Equals(StrategyNames.LinearRegression, strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateLinearRegressionStrategy(config, symbol);

            if (string.Equals(StrategyNames.Tartaglia, strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateTartagliaStrategy(config, symbol);

            if (string.Equals(StrategyNames.Mona, strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateMonaStrategy(config, symbol);

            if (string.Equals(StrategyNames.Qiqi, strategyName, StringComparison.OrdinalIgnoreCase))
                return CreateQiqiStrategy(config, symbol);

            return CreateAutoHedgeStrategy(config, symbol);
        }

        private ITradingStrategy CreateAutoHedgeStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateTradeOptions<AutoHedgeStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.MinimumPriceDistance = config.MinimumPriceDistance;
                    strategyOptions.MinimumVolume = config.MinimumVolume;
                    strategyOptions.MinReentryPositionDistanceLong = config.Strategies.AutoHedge.MinReentryPositionDistanceLong;
                    strategyOptions.MinReentryPositionDistanceShort = config.Strategies.AutoHedge.MinReentryPositionDistanceShort;
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
                    strategyOptions.MinReentryPositionDistanceLong = config.Strategies.MfiRsiEriTrend.MinReentryPositionDistanceLong;
                    strategyOptions.MinReentryPositionDistanceShort = config.Strategies.MfiRsiEriTrend.MinReentryPositionDistanceShort;
                    strategyOptions.MfiRsiLookbackPeriod = config.Strategies.MfiRsiEriTrend.MfiRsiLookbackPeriod;
                    strategyOptions.UseEriOnly = config.Strategies.MfiRsiEriTrend.UseEriOnly;
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
                    strategyOptions.ChannelLengthLong = config.Strategies.Tartaglia.ChannelLengthLong;
                    strategyOptions.ChannelLengthShort = config.Strategies.Tartaglia.ChannelLengthShort;
                    strategyOptions.StandardDeviationLong = config.Strategies.Tartaglia.StandardDeviationLong;
                    strategyOptions.StandardDeviationShort = config.Strategies.Tartaglia.StandardDeviationShort;
                    strategyOptions.MinReentryPositionDistanceLong = config.Strategies.Tartaglia.MinReentryPositionDistanceLong;
                    strategyOptions.MinReentryPositionDistanceShort = config.Strategies.Tartaglia.MinReentryPositionDistanceShort;
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
                    strategyOptions.MfiRsiLookback = config.Strategies.Mona.MfiRsiLookback;
                });
            return new MonaStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private ITradingStrategy CreateQiqiStrategy(TradingBotOptions config, string symbol)
        {
            var options = CreateRecursiveTradeOptions<QiqiStrategyOptions>(config, symbol,
                strategyOptions =>
                {
                    strategyOptions.QflBellowPercentEnterLong = config.Strategies.Qiqi.QflBellowPercentEnterLong;
                    strategyOptions.RsiTakeProfitLong = config.Strategies.Qiqi.RsiTakeProfitLong;
                    strategyOptions.QflAbovePercentEnterShort = config.Strategies.Qiqi.QflAbovePercentEnterShort;
                    strategyOptions.RsiTakeProfitShort = config.Strategies.Qiqi.RsiTakeProfitShort;
                    strategyOptions.MaxTimeStuck = config.Strategies.Qiqi.MaxTimeStuck;
                    strategyOptions.TakeProfitPercentLong = config.Strategies.Qiqi.TakeProfitPercentLong;
                    strategyOptions.TakeProfitPercentShort = config.Strategies.Qiqi.TakeProfitPercentShort;
                });
            return new QiqiStrategy(options, symbol, m_walletManager, m_restClient);
        }

        private IOptions<TOptions> CreateRecursiveTradeOptions<TOptions>(TradingBotOptions config, string symbol, Action<TOptions> optionsSetup)
            where TOptions : RecursiveStrategyBaseOptions, new()
        {
            bool isBackTest = config.IsBackTest();
            int initialUntradableDays = isBackTest ? config.BackTest.InitialUntradableDays : 0;
            var options = new TOptions
            {
                WalletExposureLong = config.WalletExposureLong,
                WalletExposureShort = config.WalletExposureShort,
                TradingMode = GetTradingMode(config, symbol),
                MaxAbsFundingRate = config.MaxAbsFundingRate,
                FeeRate = config.MakerFeeRate,
                ForceUnstuckPercentStep = config.Unstucking.ForceUnstuckPercentStep,
                SlowUnstuckPercentStep = config.Unstucking.SlowUnstuckPercentStep,
                InitialUntradableDays = initialUntradableDays,
                IgnoreInconsistency = isBackTest,
                NormalizedAverageTrueRangePeriod = config.NormalizedAverageTrueRangePeriod,
                StrategySelectPreference = config.StrategySelectPreference,
                DDownFactorLong = config.Strategies.Recursive.DDownFactorLong,
                InitialQtyPctLong = config.Strategies.Recursive.InitialQtyPctLong,
                ReentryPositionPriceDistanceLong = config.Strategies.Recursive.ReentryPositionPriceDistanceLong,
                ReentryPositionPriceDistanceWalletExposureWeightingLong = config.Strategies.Recursive.ReentryPositionPriceDistanceWalletExposureWeightingLong,
                DDownFactorShort = config.Strategies.Recursive.DDownFactorShort,
                InitialQtyPctShort = config.Strategies.Recursive.InitialQtyPctShort,
                ReentryPositionPriceDistanceShort = config.Strategies.Recursive.ReentryPositionPriceDistanceShort,
                ReentryPositionPriceDistanceWalletExposureWeightingShort = config.Strategies.Recursive.ReentryPositionPriceDistanceWalletExposureWeightingShort,
            };
            optionsSetup(options);
            return Options.Create(options);
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
                EnableRecursiveQtyFactorLong = config.EnableRecursiveQtyFactorLong,
                EnableRecursiveQtyFactorShort = config.EnableRecursiveQtyFactorShort,
                QtyFactorLong = config.QtyFactorLong,
                QtyFactorShort = config.QtyFactorShort,
                IgnoreInconsistency = isBackTest,
                NormalizedAverageTrueRangePeriod = config.NormalizedAverageTrueRangePeriod,
                StrategySelectPreference = config.StrategySelectPreference,
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