namespace CryptoBlade.Optimizer.Strategies.Qiqi
{
    public class QiqiChromosomeOptions : RecursiveGridTradingBotChromosomeOptions
    {
        public OptimizerIntRange RsiTakeProfitLong { get; set; } = new OptimizerIntRange(60, 80);
        public OptimizerFloatRange QflBellowPercentEnterLong { get; set; } = new OptimizerFloatRange(0.1f, 4.0f, 1);
    }
}