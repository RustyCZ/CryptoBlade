using CryptoBlade.Optimizer;

namespace CryptoBlade.Configuration
{
    public class RecursiveStrategyOptimizerOptions
    {
        public OptimizerFloatRange DDownFactorLong { get; set; } = new OptimizerFloatRange(0.1f, 3.0f, 3);

        public OptimizerFloatRange InitialQtyPctLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange ReentryPositionPriceDistanceLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange ReentryPositionPriceDistanceWalletExposureWeightingLong { get; set; } = new OptimizerFloatRange(0.0f, 3.0f, 3);
    }
}