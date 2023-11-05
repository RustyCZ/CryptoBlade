namespace CryptoBlade.Optimizer.Strategies
{
    public class RecursiveGridTradingBotChromosomeOptions
    {
        public OptimizerFloatRange WalletExposureLong { get; set; } = new OptimizerFloatRange(0, 3, 2);
        public OptimizerFloatRange WalletExposureShort { get; set; } = new OptimizerFloatRange(0, 3, 2);
        public OptimizerBoolRange UnstuckingEnabled { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerFloatRange SlowUnstuckThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange SlowUnstuckPositionThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange SlowUnstuckPercentStep { get; set; } = new OptimizerFloatRange(0.01f, 1.0f, 2);
        public OptimizerFloatRange ForceUnstuckThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange ForceUnstuckPositionThresholdPercent { get; set; } = new OptimizerFloatRange(-1.0f, -0.01f, 2);
        public OptimizerFloatRange ForceUnstuckPercentStep { get; set; } = new OptimizerFloatRange(0.01f, 1.0f, 2);
        public OptimizerBoolRange ForceKillTheWorst { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerIntRange MinimumVolume { get; set; } = new OptimizerIntRange(1000, 30000);
        public OptimizerFloatRange TargetLongExposure { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerFloatRange TargetShortExposure { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerIntRange MaxLongStrategies { get; set; } = new OptimizerIntRange(0, 15);
        public OptimizerIntRange MaxShortStrategies { get; set; } = new OptimizerIntRange(0, 15);
        public OptimizerBoolRange EnableCriticalModeLong { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerBoolRange EnableCriticalModeShort { get; set; } = new OptimizerBoolRange(false, true);
        public OptimizerFloatRange CriticalModelWalletExposureThresholdLong { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerFloatRange CriticalModelWalletExposureThresholdShort { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 2);
        public OptimizerFloatRange SpotRebalancingRatio { get; set; } = new OptimizerFloatRange(0.0f, 1.0f, 2);
        public OptimizerFloatRange DDownFactorLong { get; set; } = new OptimizerFloatRange(0.1f, 3.0f, 3);
        public OptimizerFloatRange InitialQtyPctLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);
        public OptimizerFloatRange ReentryPositionPriceDistanceLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);
        public OptimizerFloatRange ReentryPositionPriceDistanceWalletExposureWeightingLong { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 3);
    }
}