namespace CryptoBlade.Configuration
{
    public class DynamicBotCount
    {
        public decimal TargetLongExposure { get; set; } = 1.0m;

        public decimal TargetShortExposure { get; set; } = 1.0m;

        public int MaxLongStrategies { get; set; } = 5;

        public int MaxShortStrategies { get; set; } = 5;

        public int MaxDynamicStrategyOpenPerStep { get; set; } = 1;

        public TimeSpan Step { get; set; } = TimeSpan.FromMinutes(5);
    }
}