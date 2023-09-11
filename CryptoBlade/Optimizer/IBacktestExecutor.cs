using CryptoBlade.BackTesting;
using CryptoBlade.Configuration;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer
{
    public interface IBacktestExecutor
    {
        Task<BacktestPerformanceResult> ExecuteAsync(IOptions<TradingBotOptions> options, CancellationToken cancel);
    }
}