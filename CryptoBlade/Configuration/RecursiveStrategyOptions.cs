namespace CryptoBlade.Configuration
{
    public class RecursiveStrategyOptions
    {
        public double DDownFactorLong { get; set; } = 2.0;

        public double InitialQtyPctLong { get; set; } = 0.003;

        public double ReentryPositionPriceDistanceLong { get; set; } = 0.01;

        public double ReentryPositionPriceDistanceWalletExposureWeightingLong { get; set; } = 2.11;

        public double DDownFactorShort { get; set; } = 2.0;

        public double InitialQtyPctShort { get; set; } = 0.003;

        public double ReentryPositionPriceDistanceShort { get; set; } = 0.01;

        public double ReentryPositionPriceDistanceWalletExposureWeightingShort { get; set; } = 2.11;
    }
}