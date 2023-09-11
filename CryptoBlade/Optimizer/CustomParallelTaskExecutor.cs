using GeneticSharp;

namespace CryptoBlade.Optimizer
{
    public class CustomParallelTaskExecutor : TaskExecutorBase
    {
        private CancellationTokenSource? m_cancellationTokenSource;
        private readonly int m_parallelism;
        private readonly ILogger m_logger;

        public CustomParallelTaskExecutor(int parallelism, ILogger logger)
        {
            m_parallelism = parallelism;
            m_logger = logger;
        }

        public override bool Start()
        {
            base.Start();
            m_cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancel = m_cancellationTokenSource.Token;
            var tasks = Tasks;
            var startedTasks = new List<Task>();
            using var semaphore = new SemaphoreSlim(m_parallelism);
            foreach (Action action in tasks)
            {
                semaphore.Wait(cancel);
                startedTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        m_logger.LogError(ex, "Error executing task");
                        throw;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancel));
            }

            Task.WaitAll(startedTasks.ToArray());
            
            return true;
        }

        public override void Stop()
        {
            if (m_cancellationTokenSource != null)
            {
                m_cancellationTokenSource.Cancel();
                m_cancellationTokenSource.Dispose();
            }
            
            base.Stop();
        }
    }
}
