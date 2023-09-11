using CryptoBlade.BackTesting;
using CryptoBlade.Configuration;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer
{
    public class HistoricalDataStorageFactory
    {
        public static IHistoricalDataStorage CreateHistoricalDataStorage(IOptions<TradingBotOptions> options)
        {
            const string historicalDataDirectory = ConfigConstants.DefaultHistoricalDataDirectory;
            IOptions<ProtoHistoricalDataStorageOptions> protoHistoricalDataStorageOptions = Options.Create(new ProtoHistoricalDataStorageOptions
            {
                Directory = historicalDataDirectory,
            });
            ProtoHistoricalDataStorage historicalDataStorage = new ProtoHistoricalDataStorage(protoHistoricalDataStorageOptions);
            if (options.Value.Optimizer.EnableHistoricalDataCaching)
            {
                CachedHistoricalDataStorage cachedHistoricalDataStorage =
                    new CachedHistoricalDataStorage(historicalDataStorage);
                return cachedHistoricalDataStorage;
            }
            return historicalDataStorage;
        }
    }
}