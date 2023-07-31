namespace CryptoBlade.Strategies
{
    public class AutoHedgeStrategyOptions : TradingStrategyBaseOptions
    {
        public decimal MinimumVolume { get; set; }

        public decimal MinimumPriceDistance { get; set; }
    }
}