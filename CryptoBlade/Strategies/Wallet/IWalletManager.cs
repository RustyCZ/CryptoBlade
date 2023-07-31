using Bybit.Net.Enums;

namespace CryptoBlade.Strategies.Wallet
{
    public interface IWalletManager
    {
        Balance Contract { get; }

        Task StartAsync(CancellationToken cancel);

        Task StopAsync(CancellationToken cancel);
    }
}
