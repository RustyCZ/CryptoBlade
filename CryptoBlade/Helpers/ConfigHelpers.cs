using CryptoBlade.Configuration;

namespace CryptoBlade.Helpers
{
    public static class ConfigHelpers
    {
        public static bool IsBackTest(this TradingBotOptions options)
        {
            return options.TradingMode == TradingMode.DynamicBackTest;
        }
    }
}
