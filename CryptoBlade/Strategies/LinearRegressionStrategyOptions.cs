namespace CryptoBlade.Strategies
{
    public class LinearRegressionStrategyOptions : TradingStrategyBaseOptions
    {
        public decimal MinimumVolume { get; set; }

        public decimal MinimumPriceDistance { get; set; }

        public int ChannelLength { get; set; } = 100;

        public double StandardDeviation { get; set; } = 1.0;
    }
}