using CryptoBlade.Configuration;

namespace CryptoBlade.Strategies.Common
{
    public class RecursiveStrategyBaseOptions : TradingStrategyCommonBaseOptions
    {
        public double DDownFactorLong { get; set; }

        public double InitialQtyPctLong { get; set; }

        public double ReentryPositionPriceDistanceLong { get; set; }

        public double ReentryPositionPriceDistanceWalletExposureWeightingLong { get; set; }
    }
}