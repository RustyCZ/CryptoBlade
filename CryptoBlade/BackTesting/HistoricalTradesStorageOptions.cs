namespace CryptoBlade.BackTesting
{
    public class HistoricalTradesStorageOptions
    { 
        public string Directory { get; set; } = "HistoricalData";
        public int MemorySizePerSymbolMB { get; set; } = 256;
    }
}
