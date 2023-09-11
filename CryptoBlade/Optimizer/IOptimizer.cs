namespace CryptoBlade.Optimizer
{
    public interface IOptimizer
    {
        Task RunAsync(CancellationToken cancel);

        Task StopAsync(CancellationToken cancel);
    }
}