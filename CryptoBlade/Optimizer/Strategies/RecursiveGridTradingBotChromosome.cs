using CryptoBlade.Configuration;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer.Strategies
{
    public abstract class RecursiveGridTradingBotChromosome : ComplexChromosome, ITradingBotChromosome
    {
        protected RecursiveGridTradingBotChromosome(ComplexGene[] complexGenes)
            : base(complexGenes)
        {
        }

        public abstract void ApplyGenesToTradingBotOptions(TradingBotOptions options);

        protected void ApplyTradingBotGenes(TradingBotOptions options, ComplexGeneValue[] genes, int offset)
        {
            options.WalletExposureLong = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.WalletExposureLong + offset].FloatValue);
            options.WalletExposureShort = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.WalletExposureShort + offset].FloatValue);
            options.Unstucking.Enabled = genes[(int)RecursiveGridTradingBotGene.UnstuckingEnabled + offset].BoolValue;
            options.Unstucking.SlowUnstuckThresholdPercent = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.SlowUnstuckThresholdPercent + offset].FloatValue);
            options.Unstucking.SlowUnstuckPositionThresholdPercent = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.SlowUnstuckPositionThresholdPercent + offset].FloatValue);
            options.Unstucking.SlowUnstuckPercentStep = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.SlowUnstuckPercentStep + offset].FloatValue);
            options.Unstucking.ForceUnstuckThresholdPercent = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.ForceUnstuckThresholdPercent + offset].FloatValue);
            options.Unstucking.ForceUnstuckPositionThresholdPercent = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.ForceUnstuckPositionThresholdPercent + offset].FloatValue);
            options.Unstucking.ForceUnstuckPercentStep = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.ForceUnstuckPercentStep + offset].FloatValue);
            options.Unstucking.ForceKillTheWorst = genes[(int)RecursiveGridTradingBotGene.ForceKillTheWorst + offset].BoolValue;
            options.MinimumVolume = genes[(int)RecursiveGridTradingBotGene.MinimumVolume + offset].IntValue;
            options.DynamicBotCount.TargetLongExposure = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.TargetLongExposure + offset].FloatValue);
            options.DynamicBotCount.TargetShortExposure = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.TargetShortExposure + offset].FloatValue);
            options.DynamicBotCount.MaxLongStrategies = genes[(int)RecursiveGridTradingBotGene.MaxLongStrategies + offset].IntValue;
            options.DynamicBotCount.MaxShortStrategies = genes[(int)RecursiveGridTradingBotGene.MaxShortStrategies + offset].IntValue;
            options.CriticalMode.EnableCriticalModeLong = genes[(int)RecursiveGridTradingBotGene.EnableCriticalModeLong + offset].BoolValue;
            options.CriticalMode.EnableCriticalModeShort = genes[(int)RecursiveGridTradingBotGene.EnableCriticalModeShort + offset].BoolValue;
            options.CriticalMode.WalletExposureThresholdLong = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.CriticalModelWalletExposureThresholdLong + offset].FloatValue);
            options.CriticalMode.WalletExposureThresholdShort = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.CriticalModelWalletExposureThresholdShort + offset].FloatValue);
            options.SpotRebalancingRatio = Convert.ToDecimal(genes[(int)RecursiveGridTradingBotGene.SpotRebalancingRatio + offset].FloatValue);
            options.Strategies.Recursive.DDownFactorLong = genes[(int)RecursiveGridTradingBotGene.DDownFactorLong + offset].FloatValue;
            options.Strategies.Recursive.InitialQtyPctLong = genes[(int)RecursiveGridTradingBotGene.InitialQtyPctLong + offset].FloatValue;
            options.Strategies.Recursive.ReentryPositionPriceDistanceLong = genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceLong + offset].FloatValue;
            options.Strategies.Recursive.ReentryPositionPriceDistanceWalletExposureWeightingLong = genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceWalletExposureWeightingLong + offset].FloatValue;
            options.Strategies.Recursive.DDownFactorShort = genes[(int)RecursiveGridTradingBotGene.DDownFactorShort + offset].FloatValue;
            options.Strategies.Recursive.InitialQtyPctShort = genes[(int)RecursiveGridTradingBotGene.InitialQtyPctShort + offset].FloatValue;
            options.Strategies.Recursive.ReentryPositionPriceDistanceShort = genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceShort + offset].FloatValue;
            options.Strategies.Recursive.ReentryPositionPriceDistanceWalletExposureWeightingShort = genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceWalletExposureWeightingShort + offset].FloatValue;
        }

        protected static void AddTradingBotGenesFromOptions(IOptions<RecursiveGridTradingBotChromosomeOptions> options, ComplexGene[] genes, int offset)
        {
            genes[(int)RecursiveGridTradingBotGene.WalletExposureLong + offset] = options.Value.WalletExposureLong.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.WalletExposureShort + offset] = options.Value.WalletExposureShort.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.UnstuckingEnabled + offset] = options.Value.UnstuckingEnabled.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.SlowUnstuckThresholdPercent + offset] = options.Value.SlowUnstuckThresholdPercent.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.SlowUnstuckPositionThresholdPercent + offset] = options.Value.SlowUnstuckPositionThresholdPercent.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.SlowUnstuckPercentStep + offset] = options.Value.SlowUnstuckPercentStep.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ForceUnstuckThresholdPercent + offset] = options.Value.ForceUnstuckThresholdPercent.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ForceUnstuckPositionThresholdPercent + offset] = options.Value.ForceUnstuckPositionThresholdPercent.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ForceUnstuckPercentStep + offset] = options.Value.ForceUnstuckPercentStep.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ForceKillTheWorst + offset] = options.Value.ForceKillTheWorst.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.MinimumVolume + offset] = options.Value.MinimumVolume.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.TargetLongExposure + offset] = options.Value.TargetLongExposure.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.TargetShortExposure + offset] = options.Value.TargetShortExposure.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.MaxLongStrategies + offset] = options.Value.MaxLongStrategies.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.MaxShortStrategies + offset] = options.Value.MaxShortStrategies.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.EnableCriticalModeLong + offset] = options.Value.EnableCriticalModeLong.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.EnableCriticalModeShort + offset] = options.Value.EnableCriticalModeShort.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.CriticalModelWalletExposureThresholdLong + offset] = options.Value.CriticalModelWalletExposureThresholdLong.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.CriticalModelWalletExposureThresholdShort + offset] = options.Value.CriticalModelWalletExposureThresholdShort.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.SpotRebalancingRatio + offset] = options.Value.SpotRebalancingRatio.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.DDownFactorLong + offset] = options.Value.DDownFactorLong.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.InitialQtyPctLong + offset] = options.Value.InitialQtyPctLong.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceLong + offset] = options.Value.ReentryPositionPriceDistanceLong.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceWalletExposureWeightingLong + offset] = options.Value.ReentryPositionPriceDistanceWalletExposureWeightingLong.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.DDownFactorShort + offset] = options.Value.DDownFactorShort.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.InitialQtyPctShort + offset] = options.Value.InitialQtyPctShort.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceShort + offset] = options.Value.ReentryPositionPriceDistanceShort.ToComplexGene();
            genes[(int)RecursiveGridTradingBotGene.ReentryPositionPriceDistanceWalletExposureWeightingShort + offset] = options.Value.ReentryPositionPriceDistanceWalletExposureWeightingShort.ToComplexGene();
        }

        protected static int GetGeneLength<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Length;
        }
    }
}