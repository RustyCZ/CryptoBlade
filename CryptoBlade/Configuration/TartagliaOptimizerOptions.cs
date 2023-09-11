using CryptoBlade.Optimizer;

namespace CryptoBlade.Configuration
{
    public class TartagliaOptimizerOptions
    {
        public OptimizerIntRange ChannelLengthLong { get; set; } = new OptimizerIntRange(5, 1000);

        public OptimizerIntRange ChannelLengthShort { get; set; } = new OptimizerIntRange(5, 1000);

        public OptimizerFloatRange StandardDeviationLong { get; set; } = new OptimizerFloatRange(0.1f, 10.0f, 1);

        public OptimizerFloatRange StandardDeviationShort { get; set; } = new OptimizerFloatRange(0.1f, 10.0f, 1);

        public OptimizerFloatRange MinReentryPositionDistanceLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange MinReentryPositionDistanceShort { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);
    }
}