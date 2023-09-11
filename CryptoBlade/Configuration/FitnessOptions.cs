namespace CryptoBlade.Configuration
{
    public class FitnessOptions
    {
        public double RunningDaysPreference { get; set; } = 0.1;
        public double AvgDailyGainPreference { get; set; } = 0.7;
        public double LowestEquityToBalancePreference { get; set; } = 0.2;
        public double ExpectedGainsStdDevPreference { get; set; } = 0.0;
        public double MaxAvgDailyGainPercent { get; set; } = 5.0;
        public double MinAvgDailyGainPercent { get; set; } = -5.0;
    }
}