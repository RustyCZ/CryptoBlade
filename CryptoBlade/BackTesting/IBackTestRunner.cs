namespace CryptoBlade.BackTesting
{
    public interface IBackTestRunner
    {
        public DateTime CurrentTime { get; }
        Task PrepareDataAsync(CancellationToken cancel = default);
        Task<bool> AdvanceTimeAsync(CancellationToken cancel = default);
        Task ClearPositionsAndOrders(CancellationToken cancel = default);
        Task MoveFromSpotToFuturesAsync(decimal amount, CancellationToken cancel);
        Task MoveFromFuturesToSpotAsync(decimal amount, CancellationToken cancel);
    }
}