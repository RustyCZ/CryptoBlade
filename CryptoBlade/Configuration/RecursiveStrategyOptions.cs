namespace CryptoBlade.Configuration
{
    public class RecursiveStrategyOptions
    {
        public double DDownFactorLong { get; set; } = 2.0;

        public double InitialQtyPctLong { get; set; } = 0.003;

        public double ReentryPositionPriceDistanceLong { get; set; } = 0.01;

        public double ReentryPositionPriceDistanceWalletExposureWeightingLong { get; set; } = 2.11;
    }
}