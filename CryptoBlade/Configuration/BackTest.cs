namespace CryptoBlade.Configuration
{
    public class BackTest
    {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public decimal InitialBalance { get; set; } = 5000;

        public TimeSpan StartupCandleData { get; set; } = TimeSpan.FromDays(1);

        public string ResultFileName { get; set; } = "result.json";

        public string ResultDetailedFileName { get; set; } = "result_detailed.json";

        public int InitialUntradableDays { get; set; } = 30;
    }
}