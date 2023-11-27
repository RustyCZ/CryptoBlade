using CryptoBlade.Configuration;

namespace CryptoBlade.Strategies.Common
{
    public class RecursiveStrategyBaseOptions : TradingStrategyCommonBaseOptions
    {
        public double DDownFactorLong { get; set; }

        public double InitialQtyPctLong { get; set; }

        public double ReentryPositionPriceDistanceLong { get; set; }

        public double ReentryPositionPriceDistanceWalletExposureWeightingLong { get; set; }

        public double DDownFactorShort { get; set; }

        public double InitialQtyPctShort { get; set; }

        public double ReentryPositionPriceDistanceShort { get; set; }

        public double ReentryPositionPriceDistanceWalletExposureWeightingShort { get; set; }
    }
}