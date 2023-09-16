using CryptoBlade.Configuration;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer.Strategies
{
    public abstract class TradingBotChromosome : ComplexChromosome
    {
        protected TradingBotChromosome(ComplexGene[] complexGenes) 
            : base(complexGenes)
        {
        }

        public abstract void ApplyGenesToTradingBotOptions(TradingBotOptions options);

        protected void ApplyTradingBotGenes(TradingBotOptions options, ComplexGeneValue[] genes, int offset)
        {
            options.WalletExposureLong = Convert.ToDecimal(genes[(int)TradingBotGene.WalletExposureLong + offset].FloatValue);
            options.WalletExposureShort = Convert.ToDecimal(genes[(int)TradingBotGene.WalletExposureShort + offset].FloatValue);
            options.QtyFactorLong = Convert.ToDecimal(genes[(int)TradingBotGene.QtyFactorLong + offset].FloatValue);
            options.QtyFactorShort = Convert.ToDecimal(genes[(int)TradingBotGene.QtyFactorShort + offset].FloatValue);
            options.EnableRecursiveQtyFactorLong = genes[(int)TradingBotGene.EnableRecursiveQtyFactorLong + offset].BoolValue;
            options.EnableRecursiveQtyFactorShort = genes[(int)TradingBotGene.EnableRecursiveQtyFactorShort + offset].BoolValue;
            options.DcaOrdersCount = genes[(int)TradingBotGene.DcaOrdersCount + offset].IntValue;
            options.Unstucking.Enabled = genes[(int)TradingBotGene.UnstuckingEnabled + offset].BoolValue;
            options.Unstucking.SlowUnstuckThresholdPercent = Convert.ToDecimal(genes[(int)TradingBotGene.SlowUnstuckThresholdPercent + offset].FloatValue);
            options.Unstucking.SlowUnstuckPositionThresholdPercent = Convert.ToDecimal(genes[(int)TradingBotGene.SlowUnstuckPositionThresholdPercent + offset].FloatValue);
            options.Unstucking.SlowUnstuckPercentStep = Convert.ToDecimal(genes[(int)TradingBotGene.SlowUnstuckPercentStep + offset].FloatValue);
            options.Unstucking.ForceUnstuckThresholdPercent = Convert.ToDecimal(genes[(int)TradingBotGene.ForceUnstuckThresholdPercent + offset].FloatValue);
            options.Unstucking.ForceUnstuckPositionThresholdPercent = Convert.ToDecimal(genes[(int)TradingBotGene.ForceUnstuckPositionThresholdPercent + offset].FloatValue);
            options.Unstucking.ForceUnstuckPercentStep = Convert.ToDecimal(genes[(int)TradingBotGene.ForceUnstuckPercentStep + offset].FloatValue);
            options.Unstucking.ForceKillTheWorst = genes[(int)TradingBotGene.ForceKillTheWorst + offset].BoolValue;
            options.MinimumVolume = genes[(int)TradingBotGene.MinimumVolume + offset].IntValue;
            options.MinimumPriceDistance = Convert.ToDecimal(genes[(int)TradingBotGene.MinimumPriceDistance + offset].FloatValue);
            options.MinProfitRate = Convert.ToDecimal(genes[(int)TradingBotGene.MinProfitRate + offset].FloatValue);
            options.DynamicBotCount.TargetLongExposure = Convert.ToDecimal(genes[(int)TradingBotGene.TargetLongExposure + offset].FloatValue);
            options.DynamicBotCount.TargetShortExposure = Convert.ToDecimal(genes[(int)TradingBotGene.TargetShortExposure + offset].FloatValue);
            options.DynamicBotCount.MaxLongStrategies = genes[(int)TradingBotGene.MaxLongStrategies + offset].IntValue;
            options.DynamicBotCount.MaxShortStrategies = genes[(int)TradingBotGene.MaxShortStrategies + offset].IntValue;
            options.CriticalMode.EnableCriticalModeLong = genes[(int)TradingBotGene.EnableCriticalModeLong + offset].BoolValue;
            options.CriticalMode.EnableCriticalModeShort = genes[(int)TradingBotGene.EnableCriticalModeShort + offset].BoolValue;
            options.CriticalMode.WalletExposureThresholdLong = Convert.ToDecimal(genes[(int)TradingBotGene.CriticalModelWalletExposureThresholdLong + offset].FloatValue);
            options.CriticalMode.WalletExposureThresholdShort = Convert.ToDecimal(genes[(int)TradingBotGene.CriticalModelWalletExposureThresholdShort + offset].FloatValue);
            options.SpotRebalancingRatio = Convert.ToDecimal(genes[(int)TradingBotGene.SpotRebalancingRatio + offset].FloatValue);
        }

        protected static void AddTradingBotGenesFromOptions(IOptions<TradingBotChromosomeOptions> options, ComplexGene[] genes, int offset)
        {
            genes[(int)TradingBotGene.WalletExposureLong + offset] = options.Value.WalletExposureLong.ToComplexGene();
            genes[(int)TradingBotGene.WalletExposureShort + offset] = options.Value.WalletExposureShort.ToComplexGene();
            genes[(int)TradingBotGene.QtyFactorLong + offset] = options.Value.QtyFactorLong.ToComplexGene();
            genes[(int)TradingBotGene.QtyFactorShort + offset] = options.Value.QtyFactorShort.ToComplexGene();
            genes[(int)TradingBotGene.EnableRecursiveQtyFactorLong + offset] = options.Value.EnableRecursiveQtyFactorLong.ToComplexGene();
            genes[(int)TradingBotGene.EnableRecursiveQtyFactorShort + offset] = options.Value.EnableRecursiveQtyFactorShort.ToComplexGene();
            genes[(int)TradingBotGene.DcaOrdersCount + offset] = options.Value.DcaOrdersCount.ToComplexGene();
            genes[(int)TradingBotGene.UnstuckingEnabled + offset] = options.Value.UnstuckingEnabled.ToComplexGene();
            genes[(int)TradingBotGene.SlowUnstuckThresholdPercent + offset] = options.Value.SlowUnstuckThresholdPercent.ToComplexGene();
            genes[(int)TradingBotGene.SlowUnstuckPositionThresholdPercent + offset] = options.Value.SlowUnstuckPositionThresholdPercent.ToComplexGene();
            genes[(int)TradingBotGene.SlowUnstuckPercentStep + offset] = options.Value.SlowUnstuckPercentStep.ToComplexGene();
            genes[(int)TradingBotGene.ForceUnstuckThresholdPercent + offset] = options.Value.ForceUnstuckThresholdPercent.ToComplexGene();
            genes[(int)TradingBotGene.ForceUnstuckPositionThresholdPercent + offset] = options.Value.ForceUnstuckPositionThresholdPercent.ToComplexGene();
            genes[(int)TradingBotGene.ForceUnstuckPercentStep + offset] = options.Value.ForceUnstuckPercentStep.ToComplexGene();
            genes[(int)TradingBotGene.ForceKillTheWorst + offset] = options.Value.ForceKillTheWorst.ToComplexGene();
            genes[(int)TradingBotGene.MinimumVolume + offset] = options.Value.MinimumVolume.ToComplexGene();
            genes[(int)TradingBotGene.MinimumPriceDistance + offset] = options.Value.MinimumPriceDistance.ToComplexGene();
            genes[(int)TradingBotGene.MinProfitRate + offset] = options.Value.MinProfitRate.ToComplexGene();
            genes[(int)TradingBotGene.TargetLongExposure + offset] = options.Value.TargetLongExposure.ToComplexGene();
            genes[(int)TradingBotGene.TargetShortExposure + offset] = options.Value.TargetShortExposure.ToComplexGene();
            genes[(int)TradingBotGene.MaxLongStrategies + offset] = options.Value.MaxLongStrategies.ToComplexGene();
            genes[(int)TradingBotGene.MaxShortStrategies + offset] = options.Value.MaxShortStrategies.ToComplexGene();
            genes[(int)TradingBotGene.EnableCriticalModeLong + offset] = options.Value.EnableCriticalModeLong.ToComplexGene();
            genes[(int)TradingBotGene.EnableCriticalModeShort + offset] = options.Value.EnableCriticalModeShort.ToComplexGene();
            genes[(int)TradingBotGene.CriticalModelWalletExposureThresholdLong + offset] = options.Value.CriticalModelWalletExposureThresholdLong.ToComplexGene();
            genes[(int)TradingBotGene.CriticalModelWalletExposureThresholdShort + offset] = options.Value.CriticalModelWalletExposureThresholdShort.ToComplexGene();
            genes[(int)TradingBotGene.SpotRebalancingRatio + offset] = options.Value.SpotRebalancingRatio.ToComplexGene();
        }

        protected static int GetGeneLength<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Length;
        }
    }
}
