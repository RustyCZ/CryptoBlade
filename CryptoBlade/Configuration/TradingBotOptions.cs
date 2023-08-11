namespace CryptoBlade.Configuration
{
    public class TradingBotOptions
    {
        public ExchangeAccount[] Accounts { get; set; } = Array.Empty<ExchangeAccount>();
        public string AccountName { get; set; } = string.Empty;
        public int MaxRunningStrategies { get; set; } = 15;
        public int DcaOrdersCount { get; set; } = 1000;
        public DynamicBotCount DynamicBotCount { get; set; } = new DynamicBotCount();
        public decimal WalletExposureLong { get; set; } = 1.0m;
        public decimal WalletExposureShort { get; set; } = 1.0m;
        public string[] Whitelist { get; set; } = Array.Empty<string>();
        public string[] Blacklist { get; set; } = Array.Empty<string>();
        public SymbolTradingMode[] SymbolTradingModes { get; set; } = Array.Empty<SymbolTradingMode>();
        public decimal MinimumVolume { get; set; }
        public decimal MinimumPriceDistance { get; set; }
        public string StrategyName { get; set; } = "AutoHedge";
        public TradingMode TradingMode { get; set; } = TradingMode.Normal;
        public bool ForceMinQty { get; set; } = true;
        public int PlaceOrderAttempts { get; set; } = 3;
        public decimal MaxAbsFundingRate { get; set; } = 0.0004m;
        public decimal FeeRate { get; set; } = 0.0002m;
        public decimal MinProfitRate { get; set; } = 0.0006m;
    }
}