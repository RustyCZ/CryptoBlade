namespace CryptoBlade.Configuration
{
    public class SymbolTradingMode
    {
        public string Symbol { get; set; } = string.Empty;
        public TradingMode TradingMode { get; set; } = TradingMode.Normal;
    }
}