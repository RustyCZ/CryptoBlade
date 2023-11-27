namespace CryptoBlade.Configuration
{
    public class Qiqi
    {
        public double RsiTakeProfitLong { get; set; } = 70.0;
        public double QflBellowPercentEnterLong { get; set; } = 1.1;
        public double RsiTakeProfitShort { get; set; } = 30.0;
        public double QflAbovePercentEnterShort { get; set; } = 1.1;
        public TimeSpan MaxTimeStuck { get; set; } = TimeSpan.FromDays(120);
        public double TakeProfitPercentLong { get; set; } = 0.5;
        public double TakeProfitPercentShort { get; set; } = 0.5;
    }
}