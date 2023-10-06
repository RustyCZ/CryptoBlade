using CsvHelper.Configuration.Attributes;

namespace CryptoBlade.BackTesting.Binance
{
    public class BinanceFundingRate
    {
        [Index(0)]
        public long CalcTime { get; set; }

        [Index(1)]
        public int FundingInterval { get; set; }

        [Index(2)]
        public double LastFundingRate { get; set; }

        public DateTime CalcTimestamp => DateTimeOffset.FromUnixTimeMilliseconds(CalcTime).UtcDateTime;
    }
}
