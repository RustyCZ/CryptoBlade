using CryptoBlade.Configuration;

namespace CryptoBlade.Strategies
{
    public class TradingStrategyBaseOptions
    {
        public int DcaOrdersCount { get; set; }

        public bool ForceMinQty { get; set; }

        public decimal WalletExposureLong { get; set; }

        public decimal WalletExposureShort { get; set; }

        public TradingMode TradingMode { get; set; }

        public decimal MaxAbsFundingRate { get; set; } = 0.0004m;

        public decimal FeeRate { get; set; } = 0.0002m;

        public decimal MinProfitRate { get; set; } = 0.0006m;

        public decimal SlowUnstuckPercentStep { get; set; } = 0.05m;

        public decimal ForceUnstuckPercentStep { get; set; } = 0.1m;

        public int InitialUntradableDays { get; set; }

        public decimal QtyFactor { get; set; } = 1.0m;

        public bool EnableRecursiveQtyFactor { get; set; }

        public bool IgnoreInconsistency { get; set; }
    }
}