﻿using GeneticSharp;

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
            ThreadPool.SetMinThreads(parallelism, parallelism);
        }

        public int MinThreads => m_parallelism;

        public int MaxThreads => m_parallelism * 50;

        public override bool Start()
        {
            SetThreadPoolConfig(out _, out _, out _, out _);
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

        /// <summary>
        /// Configure the ThreadPool min and max threads number to the define on this instance properties.
        /// </summary>
        /// <param name="minWorker">Minimum worker.</param>
        /// <param name="minIOC">Minimum ioc.</param>
        /// <param name="maxWorker">Max worker.</param>
        /// <param name="maxIOC">Max ioc.</param>
        protected void SetThreadPoolConfig(out int minWorker, out int minIOC, out int maxWorker, out int maxIOC)
        {
            ThreadPool.GetMinThreads(out minWorker, out minIOC);

            if (MinThreads > minWorker)
            {
                ThreadPool.SetMinThreads(MinThreads, minIOC);
            }

            ThreadPool.GetMaxThreads(out maxWorker, out maxIOC);

            if (MaxThreads > maxWorker)
            {
                ThreadPool.SetMaxThreads(MaxThreads, maxIOC);
            }
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
