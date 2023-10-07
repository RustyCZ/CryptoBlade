namespace CryptoBlade.Configuration
{
    public class MfiRsiEriTrend
    {
        public decimal MinReentryPositionDistanceLong { get; set; }

        public decimal MinReentryPositionDistanceShort { get; set; }

        public int MfiRsiLookbackPeriod { get; set; } = 100;

        public bool UseEriOnly { get; set; }
    }
}