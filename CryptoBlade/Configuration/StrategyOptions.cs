namespace CryptoBlade.Configuration
{
    public class StrategyOptions
    {
        public AutoHedge AutoHedge { get; set; } = new AutoHedge();
        public LinearRegression LinearRegression { get; set; } = new LinearRegression();
        public Tartaglia Tartaglia { get; set; } = new Tartaglia();
        public Mona Mona { get; set; } = new Mona();
    }
}