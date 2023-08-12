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
    }
}