namespace CryptoBlade.Strategies
{
    public class TartagliaStrategyOptions : TradingStrategyBaseOptions
    {
        public decimal MinimumVolume { get; set; }

        public decimal MinimumPriceDistance { get; set; }

        public int ChannelLength { get; set; } = 100;

        public double StandardDeviation { get; set; } = 1.0;

        public decimal MinReentryPositionDistance { get; set; } = 0.02m;
    }
}