namespace CryptoBlade.Configuration
{
    public class Unstucking
    {
        public bool Enabled { get; set; } = true;
        public decimal SlowUnstuckThresholdPercent { get; set; } = -0.1m;
        public decimal SlowUnstuckPositionThresholdPercent { get; set; } = -0.01m;
        public decimal ForceUnstuckThresholdPercent { get; set; } = -0.3m;
        public decimal ForceUnstuckPositionThresholdPercent { get; set; } = -0.005m;
        public decimal SlowUnstuckPercentStep { get; set; } = 0.05m;
        public decimal ForceUnstuckPercentStep { get; set; } = 0.1m;
        public bool ForceKillTheWorst { get; set; } = false;
    }
}