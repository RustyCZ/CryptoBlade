namespace CryptoBlade.Strategies
{
    public class MfiRsiCandlePreciseTradingStrategyOptions : TradingStrategyBaseOptions
    {
        public decimal MinimumVolume { get; set; }

        public decimal MinimumPriceDistance { get; set; }
    }
}