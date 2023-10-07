namespace CryptoBlade.Optimizer.Strategies.MfiRsiTrend
{
    public class MfiRsiTrendChromosomeOptions : TradingBotChromosomeOptions
    {
        public OptimizerFloatRange MinReentryPositionDistanceLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange MinReentryPositionDistanceShort { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerIntRange MfiRsiLookbackPeriod { get; set; } = new OptimizerIntRange(14, 120);

        public OptimizerBoolRange UseEriOnly { get; set; } = new OptimizerBoolRange(false, true);
    }
}