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

        public int PlaceOrderAttempts { get; set; }
    }
}