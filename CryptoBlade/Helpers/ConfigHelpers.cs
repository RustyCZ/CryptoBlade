using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoBlade.Configuration;

namespace CryptoBlade.Helpers
{
    public static class ConfigHelpers
    {
        public static bool IsBackTest(this TradingBotOptions options)
        {
            return options.TradingMode == TradingMode.DynamicBackTest;
        }

        public static TradingBotOptions Clone(this TradingBotOptions options)
        {
            var serializedOptions = JsonSerializer.Serialize(options);
            var clonedOptions = JsonSerializer.Deserialize<TradingBotOptions>(serializedOptions);
            if (clonedOptions == null)
                throw new InvalidOperationException("Failed to deserialize options");
            return clonedOptions;
        }

        public static string CalculateMd5(this TradingBotOptions options)
        {
            var serializedOptions = JsonSerializer.Serialize(options);
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(serializedOptions));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
