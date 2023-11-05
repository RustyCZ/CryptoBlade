using CryptoBlade.Configuration;

namespace CryptoBlade.Strategies.Common
{
    public class TradingStrategyCommonBaseOptions
    {
        public decimal WalletExposureLong { get; set; }

        public decimal WalletExposureShort { get; set; }

        public TradingMode TradingMode { get; set; }

        public decimal MaxAbsFundingRate { get; set; } = 0.0004m;

        public decimal FeeRate { get; set; } = 0.0002m;

        public decimal SlowUnstuckPercentStep { get; set; } = 0.05m;

        public decimal ForceUnstuckPercentStep { get; set; } = 0.1m;

        public int InitialUntradableDays { get; set; }

        public bool IgnoreInconsistency { get; set; }

        public StrategySelectPreference StrategySelectPreference { get; set; } = StrategySelectPreference.Volume;

        public int NormalizedAverageTrueRangePeriod { get; set; } = 14;
    }
}