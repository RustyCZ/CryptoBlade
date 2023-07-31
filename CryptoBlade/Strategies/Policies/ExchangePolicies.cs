using Bybit.Net.Objects.Models.V5;
using CryptoBlade.Helpers;
using CryptoExchange.Net.Objects;
using Polly;
using Polly.Retry;
using System;

namespace CryptoBlade.Strategies.Policies
{
    public static class ExchangePolicies
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger("Policies");
        private static readonly Random s_random = new Random();
        private static readonly object s_lock = new object();

        private static TimeSpan GetRandomDelay(int maxSeconds)
        {
            lock (s_lock)
            {
                return TimeSpan.FromSeconds(s_random.Next(5, maxSeconds));
            }
        }

        public static AsyncRetryPolicy RetryForever { get; } = Policy
            .Handle<Exception>(exception => exception is not OperationCanceledException)
            .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (exception, _) =>
            {
                if (exception != null)
                    s_logger.LogWarning(exception, "Error with Exchange API. Retrying...");
                else
                    s_logger.LogWarning("Error with Exchange API. Retrying...");
            });

        public static AsyncRetryPolicy<WebCallResult> RetryTooManyVisits { get; } = Policy
            .Handle<Exception>(exception => exception is not OperationCanceledException)
            .OrResult<WebCallResult>(r => r.Error != null && r.Error.Code.HasValue && (r.Error.Code == (int)BybitErrorCodes.TooManyVisits || r.Error.Code == (int)BybitErrorCodes.IpRateLimit))
            .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (result, _) =>
            {
                if (result.Exception != null)
                    s_logger.LogWarning(result.Exception, "Error with Exchange API. Retrying...");
                else
                    s_logger.LogWarning("Error with Exchange API. Retrying...");
            });
    }

    public static class ExchangePolicies<T>
    {
        // ReSharper disable StaticMemberInGenericType
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger("Policies");
        // ReSharper restore StaticMemberInGenericType

        private static readonly Random s_random = new Random();
        private static readonly object s_lock = new object();

        private static TimeSpan GetRandomDelay(int maxSeconds)
        {
            lock (s_lock)
            {
                return TimeSpan.FromSeconds(s_random.Next(1, maxSeconds));
            }
        }

        public static AsyncRetryPolicy<WebCallResult<T>> RetryTooManyVisits { get; } = Policy
            .Handle<Exception>(exception => exception is not OperationCanceledException)
            .OrResult<WebCallResult<T>>(r => r.Error != null && r.Error.Code.HasValue && (r.Error.Code == (int)BybitErrorCodes.TooManyVisits || r.Error.Code == (int)BybitErrorCodes.IpRateLimit))
            .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (result, _) =>
            {
                if (result.Exception != null)
                    s_logger.LogWarning(result.Exception, "Error with Exchange API. Retrying with delay...");
                else
                    s_logger.LogWarning("Too many visits. Retrying with delay...");
            });

        public static AsyncRetryPolicy<WebCallResult<BybitResponse<T>>> RetryTooManyVisitsBybitResponse { get; } = Policy
            .Handle<Exception>(exception => exception is not OperationCanceledException)
            .OrResult<WebCallResult<BybitResponse<T>>>(r => r.Error != null && r.Error.Code.HasValue && (r.Error.Code == (int)BybitErrorCodes.TooManyVisits || r.Error.Code == (int)BybitErrorCodes.IpRateLimit))
            .WaitAndRetryForeverAsync(_ => GetRandomDelay(10), (result, _) =>
            {
                if (result.Exception != null)
                    s_logger.LogWarning(result.Exception, "Error with Exchange API. Retrying with delay...");
                else
                    s_logger.LogWarning("Too many visits. Retrying with delay...");
            });
    }
}
