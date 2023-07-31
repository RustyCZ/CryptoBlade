using CryptoBlade.Strategies.Policies;
using CryptoExchange.Net.Sockets;

namespace CryptoBlade.Helpers
{
    public static class SubscriptionReconnectHelper
    {
        public static void AutoReconnect(this UpdateSubscription subscription, ILogger logger)
        {
            subscription.ConnectionRestored += _ =>
            {
                logger.LogWarning("Connection restored...");
            };

            subscription.ConnectionLost += async () =>
            {
                logger.LogWarning("Connection lost. Reconnecting...");
                await ExchangePolicies.RetryForever.ExecuteAsync(async () => await subscription.ReconnectAsync());
            };
            subscription.Exception += async (ex) =>
            {
                logger.LogWarning(ex, "Error in subscription to wallet updates. Retrying...");
                await ExchangePolicies.RetryForever.ExecuteAsync(async () => await subscription.ReconnectAsync());
            };
        }
    }
}
