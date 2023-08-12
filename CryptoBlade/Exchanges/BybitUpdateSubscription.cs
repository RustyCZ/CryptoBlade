using CryptoBlade.Helpers;
using CryptoExchange.Net.Sockets;

namespace CryptoBlade.Exchanges
{
    public class BybitUpdateSubscription : IUpdateSubscription
    {
        private readonly UpdateSubscription m_subscription;

        public BybitUpdateSubscription(UpdateSubscription subscription)
        {
            m_subscription = subscription;
        }

        public void AutoReconnect(ILogger logger)
        {
            m_subscription.AutoReconnect(logger);
        }

        public async Task CloseAsync()
        {
            await m_subscription.CloseAsync();
        }
    }
}