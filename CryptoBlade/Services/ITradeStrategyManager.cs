using CryptoBlade.Strategies.Common;

namespace CryptoBlade.Services
{
    public interface ITradeStrategyManager
    {
        DateTime LastExecution { get; }
        Task<ITradingStrategy[]> GetStrategiesAsync(CancellationToken cancel);
        Task StartStrategiesAsync(CancellationToken cancel);
        Task StopStrategiesAsync(CancellationToken cancel);
    }
}