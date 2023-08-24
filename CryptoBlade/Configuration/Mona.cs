namespace CryptoBlade.Configuration
{
    public class Mona
    {
        public decimal MinReentryPositionDistance { get; set; } = 0.02m;

        public int ClusteringLength { get; set; } = 480;

        public double BandwidthCoefficient { get; set; } = 0.3;
    }
}