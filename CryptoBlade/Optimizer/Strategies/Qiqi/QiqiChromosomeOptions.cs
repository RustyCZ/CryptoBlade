namespace CryptoBlade.Optimizer.Strategies.Qiqi
{
    public class QiqiChromosomeOptions : RecursiveGridTradingBotChromosomeOptions
    {
        public OptimizerIntRange RsiTakeProfitLong { get; set; } = new OptimizerIntRange(60, 80);
        public OptimizerFloatRange QflBellowPercentEnterLong { get; set; } = new OptimizerFloatRange(0.1f, 4.0f, 1);
        public OptimizerIntRange RsiTakeProfitShort { get; set; } = new OptimizerIntRange(20, 40);
        public OptimizerFloatRange QflAbovePercentEnterShort { get; set; } = new OptimizerFloatRange(0.1f, 4.0f, 1);
        public OptimizerFloatRange TakeProfitPercentLong { get; set; } = new OptimizerFloatRange(0.02f, 0.5f, 2);
        public OptimizerFloatRange TakeProfitPercentShort { get; set; } = new OptimizerFloatRange(0.02f, 0.5f, 2);
        public OptimizerIntRange EriTrendLengthLong { get; set; } = new OptimizerIntRange(16, 128);
        public OptimizerIntRange EriTrendLengthShort { get; set; } = new OptimizerIntRange(16, 128);
    }
}