namespace CryptoBlade.BackTesting
{
    public class BackTestExchangeOptions
    {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public TimeSpan StartupCandleData { get; set; } = TimeSpan.FromDays(1);

        public string[] Symbols { get; set; } = Array.Empty<string>();

        public decimal InitialBalance { get; set; } = 5000;

        public decimal MakerFeeRate { get; set; } = 0.0002m;

        public decimal TakerFeeRate { get; set; } = 0.00055m;

        public bool OptimisticFill { get; set; } = true;

        public string HistoricalDataDirectory { get; set; } = "HistoricalData";
    }
}