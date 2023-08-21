namespace CryptoBlade.Configuration
{
    public class StrategyOptions
    {
        public LinearRegression LinearRegression { get; set; } = new LinearRegression();
        public Tartaglia Tartaglia { get; set; } = new Tartaglia();
    }
}