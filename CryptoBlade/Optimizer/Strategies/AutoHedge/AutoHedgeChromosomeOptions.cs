namespace CryptoBlade.Optimizer.Strategies.AutoHedge
{
    public class AutoHedgeChromosomeOptions : TradingBotChromosomeOptions
    {
        public OptimizerFloatRange MinReentryPositionDistanceLong { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);

        public OptimizerFloatRange MinReentryPositionDistanceShort { get; set; } = new OptimizerFloatRange(0.001f, 0.2f, 3);
    }
}