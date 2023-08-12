namespace CryptoBlade.Strategies.Wallet
{
    public class NullWalletManager : IWalletManager
    {
        public Balance Contract { get; } = new Balance();

        public Task StartAsync(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancel)
        {
            return Task.CompletedTask;
        }
    }
}