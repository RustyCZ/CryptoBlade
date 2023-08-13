namespace CryptoBlade.Models
{
    public class Ticker
    {
        public decimal BestAskPrice { get; set; }

        public decimal BestBidPrice { get; set; }

        public decimal LastPrice { get; set; }

        public decimal? FundingRate { get; set; }

        public DateTime Timestamp { get; set; }
    }
}