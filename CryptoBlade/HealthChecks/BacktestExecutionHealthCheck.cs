using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CryptoBlade.HealthChecks
{
    public class BacktestExecutionHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.FromResult(new HealthCheckResult(HealthStatus.Healthy, "Backtest is healthy."));
        }
    }
}