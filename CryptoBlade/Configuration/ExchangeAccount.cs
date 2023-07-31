namespace CryptoBlade.Configuration
{
    public class ExchangeAccount
    {
        public string Name { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
        public Exchange Exchange { get; set; } = Exchange.Bybit;
    }
}