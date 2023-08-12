namespace CryptoBlade.Exchanges
{
    public interface IUpdateSubscription
    {
        void AutoReconnect(ILogger logger);
        Task CloseAsync();
    }
}