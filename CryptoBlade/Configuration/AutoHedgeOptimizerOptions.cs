using CryptoBlade.Optimizer;

namespace CryptoBlade.Configuration
{
    public class AutoHedgeOptimizerOptions
    {
        public OptimizerFloatRange MinReentryPositionDistanceLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange MinReentryPositionDistanceShort { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);
    }
}