using CsvHelper.Configuration.Attributes;

namespace CryptoBlade.BackTesting.Bybit
{
    public class BybitHistoricalTick
    {
        [Name("timestamp")]
        public double Timestamp { get; set; }

        [Name("size")]
        public decimal Size { get; set; }

        [Name("price")]
        public decimal Price { get; set; }

        public DateTime TimestampDateTime => DateTime.UnixEpoch.AddSeconds(Timestamp);
    }
}