using System.Text.Json;
using CryptoBlade.BackTesting;
using CryptoBlade.Configuration;
using CryptoBlade.Helpers;
using CryptoBlade.Optimizer.Strategies;
using GeneticSharp;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer.Fitness
{
    public class StrategyFitness : IFitness
    {
        private readonly IHistoricalDataStorage m_historicalDataStorage;
        private readonly IOptions<TradingBotOptions> m_initialOptions;
        private readonly CancellationToken m_cancel;
        private readonly ILogger m_logger;

        public StrategyFitness(IOptions<TradingBotOptions> initialOptions,
            IHistoricalDataStorage historicalDataStorage,
            CancellationToken cancel,
            ILogger logger)
        {
            m_historicalDataStorage = historicalDataStorage;
            m_initialOptions = initialOptions;
            m_cancel = cancel;
            m_logger = logger;
        }

        public double Evaluate(IChromosome chromosome)
        {
            OptimizerBacktestExecutor backtestExecutor = new OptimizerBacktestExecutor(m_historicalDataStorage);
            var clonedOptions = Options.Create(m_initialOptions.Value.Clone());
            TradingBotChromosome tradingBotChromosome = (TradingBotChromosome)chromosome;
            tradingBotChromosome.ApplyGenesToTradingBotOptions(clonedOptions.Value);
            BacktestPerformanceResult? backtestResult =
                TryToLoadExistingResult(clonedOptions.Value)
                ?? backtestExecutor.ExecuteAsync(clonedOptions, m_cancel).GetAwaiter().GetResult();

            var fitness = CalculateFitness(backtestResult);

            return fitness;
        }

        private BacktestPerformanceResult? TryToLoadExistingResult(TradingBotOptions options)
        {
            var md5Options = options.CalculateMd5();
            var backtestResultPath = Path.Combine(ConfigConstants.BackTestsDirectory, md5Options);
            if (Directory.Exists(backtestResultPath))
            {
                var resultFile = Path.Combine(backtestResultPath, options.BackTest.ResultFileName);
                if (File.Exists(resultFile))
                {
                    var json = File.ReadAllText(resultFile);
                    var result = JsonSerializer.Deserialize<BacktestPerformanceResult>(json);
                    return result;
                }
            }
            return null;
        }

        private double CalculateFitness(BacktestPerformanceResult result)
        {
            try
            {
                double runningDaysRatio = result.TotalDays / (double)result.ExpectedDays;
                var fitnessOptions = m_initialOptions.Value.Optimizer.GeneticAlgorithm.FitnessOptions;
                double runningDaysPreference = fitnessOptions.RunningDaysPreference;
                double avgDailyGainPreference = fitnessOptions.AvgDailyGainPreference;
                double lowestEquityToBalancePreference = fitnessOptions.LowestEquityToBalancePreference;
                double expectedGainsStdDevPreference = fitnessOptions.ExpectedGainsStdDevPreference;
                double equityToBalanceStdDevPreference = fitnessOptions.EquityToBalanceStdDevPreference;
                double fitness =
                    runningDaysPreference * runningDaysRatio
                    - avgDailyGainPreference
                    - lowestEquityToBalancePreference
                    - expectedGainsStdDevPreference
                    - equityToBalanceStdDevPreference;
                double maxAvgDailyGainPercent = fitnessOptions.MaxAvgDailyGainPercent;
                double minAvgDailyGainPercent = fitnessOptions.MinAvgDailyGainPercent;
                if (result.LowestEquityToBalance > 0)
                {
                    double avgDailyGainPercent = (double)result.AverageDailyGainPercent;
                    avgDailyGainPercent = Math.Max(minAvgDailyGainPercent, avgDailyGainPercent);
                    avgDailyGainPercent = Math.Min(maxAvgDailyGainPercent, avgDailyGainPercent);
                    double normalizedAvgDailyGainPercent = avgDailyGainPercent / maxAvgDailyGainPercent;
                    fitness = runningDaysPreference * runningDaysRatio
                              + avgDailyGainPreference * normalizedAvgDailyGainPercent
                              + lowestEquityToBalancePreference * (double)result.LowestEquityToBalance
                              - expectedGainsStdDevPreference * result.ExpectedGainsStdDev
                              - equityToBalanceStdDevPreference * result.EquityToBalanceStdDev;
                }

                return fitness;
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Error calculating fitness");
                return -999;
            }
        }
    }
}