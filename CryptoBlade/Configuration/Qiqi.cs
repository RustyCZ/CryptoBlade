namespace CryptoBlade.Configuration
{
    public class Qiqi
    {
        public double RsiTakeProfitLong { get; set; } = 70.0;
        public double QflBellowPercentEnterLong { get; set; } = 1.1;
        public TimeSpan MaxTimeStuck { get; set; } = TimeSpan.FromDays(120);
    }
}