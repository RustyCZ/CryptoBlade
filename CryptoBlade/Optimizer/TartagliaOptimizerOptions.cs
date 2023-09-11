namespace CryptoBlade.Optimizer
{
    public class TartagliaOptimizerOptions
    {
        public OptimizerIntRange ChannelLengthLong { get; set; } = new OptimizerIntRange(5, 1000);

        public OptimizerIntRange ChannelLengthShort { get; set; } = new OptimizerIntRange(5, 1000);

        public OptimizerDoubleRange StandardDeviationLong { get; set; } = new OptimizerDoubleRange(0.1, 10.0);

        public OptimizerDoubleRange StandardDeviationShort { get; set; } = new OptimizerDoubleRange(0.1, 10.0);

        public OptimizerDoubleRange MinReentryPositionDistanceLong { get; set; } = new OptimizerDoubleRange(0.01, 0.1);

        public OptimizerDoubleRange MinReentryPositionDistanceShort { get; set; } = new OptimizerDoubleRange(0.01, 0.1);
    }
}