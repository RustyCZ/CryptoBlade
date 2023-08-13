namespace CryptoBlade.BackTesting
{
    public interface IBackTestRunner
    {
        Task PrepareDataAsync(CancellationToken cancel = default);
        Task<bool> MoveNextAsync(CancellationToken cancel = default);
    }
}