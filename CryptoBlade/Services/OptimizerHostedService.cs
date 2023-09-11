using CryptoBlade.Optimizer;

namespace CryptoBlade.Services
{
    public class OptimizerHostedService : IHostedService
    {
        private readonly IOptimizer m_optimizer;

        public OptimizerHostedService(IOptimizer optimizer)
        {
            m_optimizer = optimizer;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await m_optimizer.RunAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await m_optimizer.StopAsync(cancellationToken);
        }
    }
}