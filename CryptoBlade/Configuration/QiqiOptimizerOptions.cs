using CryptoBlade.Optimizer;

namespace CryptoBlade.Configuration
{
    public class QiqiOptimizerOptions
    {
        public OptimizerIntRange RsiTakeProfitLong { get; set; } = new OptimizerIntRange(60, 80);
        public OptimizerFloatRange QflBellowPercentEnterLong { get; set; } = new OptimizerFloatRange(0.1f, 4.0f, 1);
    }
}