using CryptoBlade.Configuration;

namespace CryptoBlade.Optimizer.Strategies
{
    public interface ITradingBotChromosome
    {
        void ApplyGenesToTradingBotOptions(TradingBotOptions options);
    }
}