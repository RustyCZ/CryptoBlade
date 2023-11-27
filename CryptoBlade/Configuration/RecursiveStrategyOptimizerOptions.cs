using CryptoBlade.Optimizer;

namespace CryptoBlade.Configuration
{
    public class RecursiveStrategyOptimizerOptions
    {
        public OptimizerFloatRange DDownFactorLong { get; set; } = new OptimizerFloatRange(0.1f, 3.0f, 3);

        public OptimizerFloatRange InitialQtyPctLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange ReentryPositionPriceDistanceLong { get; set; } = new OptimizerFloatRange(0.0001f, 0.2f, 4);

        public OptimizerFloatRange ReentryPositionPriceDistanceWalletExposureWeightingLong { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 3);

        public OptimizerFloatRange DDownFactorShort { get; set; } = new OptimizerFloatRange(0.1f, 3.0f, 3);

        public OptimizerFloatRange InitialQtyPctShort { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange ReentryPositionPriceDistanceShort { get; set; } = new OptimizerFloatRange(0.0001f, 0.2f, 4);

        public OptimizerFloatRange ReentryPositionPriceDistanceWalletExposureWeightingShort { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 3);
    }
}