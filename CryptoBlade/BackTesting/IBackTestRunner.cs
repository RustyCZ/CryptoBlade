namespace CryptoBlade.BackTesting
{
    public interface IBackTestRunner
    {
        public DateTime CurrentTime { get; }
        Task PrepareDataAsync(CancellationToken cancel = default);
        Task<bool> AdvanceTimeAsync(CancellationToken cancel = default);
    }
}