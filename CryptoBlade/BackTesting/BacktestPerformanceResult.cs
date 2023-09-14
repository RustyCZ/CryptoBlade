namespace CryptoBlade.BackTesting
{
    public class BacktestPerformanceResult
    {
        public decimal InitialBalance { get; set; }
        public decimal FinalBalance { get; set; }
        public decimal FinalEquity { get; set; }
        public decimal LowestEquityToBalance { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal RealizedPnl { get; set; }
        public decimal AverageDailyGainPercent { get; set; }
        public decimal MaxDrawDown { get; set; }
        public int TotalDays { get; set; }
        public int ExpectedDays { get; set; }
        public decimal LossProfitRatio { get; set; }
        public decimal SpotBalance { get; set; }
        public double EquityBalanceNormalizedRooMeanSquareError { get; set; }
        public double AdgNormalizedRootMeanSquareError { get; set; }
        public OpenPositionWithOrders[] OpenPositionWithOrders { get; set; } = Array.Empty<OpenPositionWithOrders>();
    }
}