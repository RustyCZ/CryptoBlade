using CryptoBlade.Strategies.Common;

namespace CryptoBlade.Services
{
    public class NullTradeStrategyManager : ITradeStrategyManager
    {
        public DateTime LastExecution => DateTime.UtcNow;
        
        public Task<ITradingStrategy[]> GetStrategiesAsync(CancellationToken cancel)
        {
            return Task.FromResult(Array.Empty<ITradingStrategy>());
        }

        public Task StartStrategiesAsync(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }

        public Task StopStrategiesAsync(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }
    }
}