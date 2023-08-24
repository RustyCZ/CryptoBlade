using CsvHelper.Configuration.Attributes;

namespace CryptoBlade.BackTesting.Binance
{
    public class BinanceCandle
    {
        [Index(0)]
        public long UnixTimestamp { get; set; }

        [Index(1)]
        public decimal Open { get; set; }

        [Index(2)]
        public decimal High { get; set; }

        [Index(3)]
        public decimal Low { get; set; }

        [Index(4)]
        public decimal Close { get; set; }

        [Index(5)]
        public decimal Volume { get; set; }

        public DateTime Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(UnixTimestamp).UtcDateTime;
    }
}
