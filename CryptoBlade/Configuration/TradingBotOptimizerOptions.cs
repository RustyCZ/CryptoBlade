using CryptoBlade.Optimizer;

namespace CryptoBlade.Configuration
{
    public class TradingBotOptimizerOptions
    {
        public OptimizerFloatRange WalletExposureLong { get; set; } = new OptimizerFloatRange(0, 3, 2);
        public OptimizerFloatRange WalletExposureShort { get; set; } = new OptimizerFloatRange(0, 3, 2);
        public OptimizerFloatRange QtyFactorLong { get; set; } = new OptimizerFloatRange(0.001f, 3, 3);
        public OptimizerFloatRange QtyFactorShort { get; set; } = new OptimizerFloatRange(0.001f, 3, 3);
        public OptimizerBoolRange EnableRecursiveQtyFactorLong { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerBoolRange EnableRecursiveQtyFactorShort { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerIntRange DcaOrdersCount { get; set; } = new OptimizerIntRange(1, 5000);
        public OptimizerBoolRange UnstuckingEnabled { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerFloatRange SlowUnstuckThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange SlowUnstuckPositionThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange SlowUnstuckPercentStep { get; set; } = new OptimizerFloatRange(0.01f, 1.0f, 2);
        public OptimizerFloatRange ForceUnstuckThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange ForceUnstuckPositionThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange ForceUnstuckPercentStep { get; set; } = new OptimizerFloatRange(0.01f, 1.0f, 2);
        public OptimizerBoolRange ForceKillTheWorst { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerIntRange MinimumVolume { get; set; } = new OptimizerIntRange(1000, 30000);
        public OptimizerFloatRange MinimumPriceDistance { get; set; } = new OptimizerFloatRange(0.015f, 0.03f, 3);
        public OptimizerFloatRange MinProfitRate { get; set; } = new OptimizerFloatRange(0.0006f, 0.01f, 4);
        public OptimizerFloatRange TargetLongExposure { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerFloatRange TargetShortExposure { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerIntRange MaxLongStrategies { get; set; } = new OptimizerIntRange(0, 15);
        public OptimizerIntRange MaxShortStrategies { get; set; } = new OptimizerIntRange(0, 15);
        public OptimizerBoolRange EnableCriticalModeLong { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerBoolRange EnableCriticalModeShort { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerFloatRange CriticalModelWalletExposureThresholdLong { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerFloatRange CriticalModelWalletExposureThresholdShort { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerFloatRange SpotRebalancingRatio { get; set; } = new OptimizerFloatRange(0.0f, 1.0f, 2);
    }
}