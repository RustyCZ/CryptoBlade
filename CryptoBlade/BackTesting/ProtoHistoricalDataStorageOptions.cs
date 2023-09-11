using CryptoBlade.Configuration;

namespace CryptoBlade.BackTesting
{
    public class ProtoHistoricalDataStorageOptions
    {
        public string Directory { get; set; } = ConfigConstants.DefaultHistoricalDataDirectory;
    }
}