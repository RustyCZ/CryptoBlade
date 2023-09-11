namespace CryptoBlade.Optimizer
{
    public class OptimizerApplicationHostApplicationLifetime : IHostApplicationLifetime
    {
        public event Action<EventArgs>? ApplicationStoppedEvent;

        public OptimizerApplicationHostApplicationLifetime(CancellationToken cancel)
        {
            ApplicationStopping = cancel;
            ApplicationStopped = cancel;
        }

        public void StopApplication()
        {
            OnApplicationStopped();
        }

        protected virtual void OnApplicationStopped()
        {
            ApplicationStoppedEvent?.Invoke(EventArgs.Empty);
        }

        public CancellationToken ApplicationStarted { get; } = CancellationToken.None;
        public CancellationToken ApplicationStopped { get; }
        public CancellationToken ApplicationStopping { get; }
    }
}