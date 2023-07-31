using CryptoBlade.Configuration;
using CryptoBlade.Strategies.Common;

namespace CryptoBlade.Strategies
{
    public interface ITradingStrategyFactory
    {
        ITradingStrategy CreateStrategy(TradingBotOptions config, string symbol);
    }
}