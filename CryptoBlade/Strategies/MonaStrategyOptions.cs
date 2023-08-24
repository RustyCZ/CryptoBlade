namespace CryptoBlade.Strategies
{
    public class MonaStrategyOptions : TradingStrategyBaseOptions
    {
        public decimal MinimumVolume { get; set; }

        public decimal MinimumPriceDistance { get; set; }

        public decimal MinReentryPositionDistance { get; set; } = 0.02m;

        public int ClusteringLength { get; set; } = 480;

        public double BandwidthCoefficient { get; set; } = 0.3;
    }
}