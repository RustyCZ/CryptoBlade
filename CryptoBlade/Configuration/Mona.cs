namespace CryptoBlade.Configuration
{
    public class Mona
    {
        public decimal MinReentryPositionDistanceLong { get; set; } = 0.02m;

        public decimal MinReentryPositionDistanceShort { get; set; } = 0.05m;

        public int ClusteringLength { get; set; } = 480;

        public double BandwidthCoefficient { get; set; } = 0.3;
    }
}