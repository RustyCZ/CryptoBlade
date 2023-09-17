namespace CryptoBlade.Strategies
{
    public class AutoHedgeStrategyOptions : TradingStrategyBaseOptions
    {
        public decimal MinimumVolume { get; set; }

        public decimal MinimumPriceDistance { get; set; }

        public decimal MinReentryPositionDistanceLong { get; set; }

        public decimal MinReentryPositionDistanceShort { get; set; }
    }
}