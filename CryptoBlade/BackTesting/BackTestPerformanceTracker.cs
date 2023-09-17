using System.Text.Json;
using System.Threading;
using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using ScottPlot;
using ScottPlot.Extensions;

namespace CryptoBlade.BackTesting
{
    public class BackTestPerformanceTracker : IHostedService
    {
        private readonly record struct BalanceInTime(Balance Balance, decimal TotalBalance, DateTime Time);
        private readonly record struct ScatterPlotPoint(double X, double Y);

        private readonly IOptions<BackTestPerformanceTrackerOptions> m_options;
        private readonly BackTestExchange m_backTestExchange;
        private IUpdateSubscription? m_nextStepSubscription;
        private decimal m_lowestEquityToBalance;
        private decimal m_maxDrawDown;
        private decimal m_localTopBalance;
        private readonly List<BalanceInTime> m_balanceHistory;
        private readonly AsyncLock m_lock;
        private readonly string m_testId;
        private readonly ILogger<BackTestPerformanceTracker> m_logger;
        private readonly IOptions<TradingBotOptions> m_tradingBotOptions;
        private decimal m_totalLoss;
        private decimal m_totalProfit;
        private BalanceInTime m_lastBalance;
        private decimal m_initialSpot;
        private bool m_resultsSaved;

        public BackTestPerformanceTracker(IOptions<BackTestPerformanceTrackerOptions> options,
            IOptions<TradingBotOptions> tradingBotOptions,
            BackTestExchange backTestExchange, 
            IBackTestIdProvider backTestIdProvider,
            ILogger<BackTestPerformanceTracker> logger)
        {
            m_balanceHistory = new List<BalanceInTime>();
            m_lowestEquityToBalance = 1.0m;
            m_backTestExchange = backTestExchange;
            m_logger = logger;
            m_options = options;
            m_lock = new AsyncLock();
            m_tradingBotOptions = tradingBotOptions;
            m_testId = backTestIdProvider.GetTestId();
            Result = new BacktestPerformanceResult();
        }

        public BacktestPerformanceResult Result { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var balance = await m_backTestExchange.GetBalancesAsync(cancellationToken);
            m_localTopBalance = balance.WalletBalance!.Value;
            //m_initialFutures = balance.WalletBalance!.Value;
            if (m_tradingBotOptions.Value.SpotRebalancingRatio > 0)
            {
                var initialFutures = balance.WalletBalance!.Value;
                var rebalancingRatio = m_tradingBotOptions.Value.SpotRebalancingRatio;
                if (rebalancingRatio > 1)
                    rebalancingRatio = 1;
                var desiredSpot = initialFutures * rebalancingRatio;
                m_initialSpot = desiredSpot;
                await m_backTestExchange.MoveFromFuturesToSpotAsync(desiredSpot, cancellationToken);
                balance = await m_backTestExchange.GetBalancesAsync(cancellationToken);
            }
            m_maxDrawDown = 0.0m;
            var time = m_backTestExchange.CurrentTime;
            var totalBalance = m_backTestExchange.SpotBalance;
            if (balance.WalletBalance.HasValue)
                totalBalance += balance.WalletBalance.Value;
            m_lastBalance = new BalanceInTime(balance, totalBalance, time);
            m_balanceHistory.Add(m_lastBalance);
            m_nextStepSubscription = await m_backTestExchange.SubscribeToNextStepAsync(OnNextStep, cancellationToken);
        }

        private async void OnNextStep(DateTime obj)
        {
            var balance = await m_backTestExchange.GetBalancesAsync(CancellationToken.None);
            OnWalletUpdate(balance);
        }

        private async void OnWalletUpdate(Balance obj)
        {
            using (m_lock.Lock())
            {
                var lastBalance = m_balanceHistory.Last();
                var time = m_backTestExchange.CurrentTime;
                var profitChange = obj.RealizedPnl!.Value - m_lastBalance.Balance.RealizedPnl!.Value;
                if (profitChange > 0)
                    m_totalProfit += profitChange;
                else
                    m_totalLoss += Math.Abs(profitChange);
                var spotBalance = m_backTestExchange.SpotBalance;
                var totalBalance = spotBalance;
                if (obj.WalletBalance.HasValue)
                    totalBalance += obj.WalletBalance.Value;
                m_lastBalance = new BalanceInTime(obj, totalBalance, time);
                decimal equityToBalance;
                if (totalBalance <= 0)
                    equityToBalance = 0;
                else
                {
                    try
                    {
                        equityToBalance = (decimal)Math.Round((double)(obj.Equity!.Value + spotBalance) / (double)totalBalance, 3);
                    }
                    catch (OverflowException)
                    {
                        equityToBalance = 0;
                    }
                }
                if (equityToBalance < m_lowestEquityToBalance)
                    m_lowestEquityToBalance = equityToBalance;
                if (totalBalance > m_localTopBalance)
                    m_localTopBalance = totalBalance;
                decimal drawDown = 1.0m;
                try
                {
                    drawDown = 1.0m - (totalBalance / m_localTopBalance);
                }
                catch (OverflowException)
                {
                }
                
                if (drawDown > m_maxDrawDown)
                    m_maxDrawDown = drawDown;

                if (time > lastBalance.Time || (totalBalance <= 0))
                {
                    m_balanceHistory.Add(new BalanceInTime(obj, totalBalance, time));
                }
            }

            // it needs to happen outside of the lock to avoid deadlocks
            if (m_tradingBotOptions.Value.SpotRebalancingRatio > 0)
            {
                var spotBalance = m_backTestExchange.SpotBalance;
                var totalBalance = spotBalance;
                if (obj.WalletBalance.HasValue)
                    totalBalance += obj.WalletBalance.Value;
                var rebalancingRatio = m_tradingBotOptions.Value.SpotRebalancingRatio;
                if (rebalancingRatio > 1)
                    rebalancingRatio = 1;
                var desiredSpot = totalBalance * rebalancingRatio;
                var spotDiff = desiredSpot - spotBalance;
                const decimal minDiffPercent = 0.005m;
                if (spotDiff > 0 && spotDiff > minDiffPercent * totalBalance)
                {
                    await m_backTestExchange.MoveFromFuturesToSpotAsync(spotDiff, CancellationToken.None);
                }
                else if (spotDiff < 0 && spotDiff < -minDiffPercent * totalBalance)
                {
                    await m_backTestExchange.MoveFromSpotToFuturesAsync(-spotDiff, CancellationToken.None);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (m_nextStepSubscription != null)
            {
                await m_nextStepSubscription.CloseAsync();
                m_nextStepSubscription = null;
            }

            var balance = await m_backTestExchange.GetBalancesAsync(cancellationToken);
            OnWalletUpdate(balance);
            using (await m_lock.LockAsync(cancellationToken))
            {
                await CreateReportAsync();
            }
        }

        private async Task CreateReportAsync()
        {
            try
            {
                await SaveBacktestResultsAsync();
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Failed to save backtest results");
            }
            try
            {
                PlotBalanceAndEquity();
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Failed to plot balance and equity");
            }
        }

        private decimal CalculateAverageDailyGain()
        {
            decimal initialBalance = m_balanceHistory.First().Balance.WalletBalance!.Value + m_initialSpot;
            decimal finalBalance = m_balanceHistory.Last().Balance.WalletBalance!.Value + m_backTestExchange.SpotBalance;
            decimal dailyGainPercent;
            int numberOfDays = (int)Math.Round((m_balanceHistory.Last().Time - m_balanceHistory.First().Time).TotalDays);

            if (numberOfDays <= 0)
                numberOfDays = 1;
            try
            {
                decimal dailyGain = (decimal)Math.Pow((double)(finalBalance / initialBalance), 1.0 / numberOfDays) - 1;
                dailyGainPercent = dailyGain * 100.0m;
            }
            catch (OverflowException)
            {
                double dailyGain = Math.Pow((double)finalBalance / (double)initialBalance, 1.0 / numberOfDays) - 1;
                dailyGainPercent = (decimal)Math.Round(dailyGain * 100.0, 6);
            }

            return dailyGainPercent;
        }

        private async Task SaveBacktestResultsAsync()
        {
            if(m_resultsSaved)
                return;
            decimal initialBalance = m_balanceHistory.First().Balance.WalletBalance!.Value + m_initialSpot;
            decimal finalBalance = m_balanceHistory.Last().Balance.WalletBalance!.Value + m_backTestExchange.SpotBalance;
            decimal finalEquity = m_balanceHistory.Last().Balance.Equity!.Value;
            int numberOfDays = (int)Math.Round((m_balanceHistory.Last().Time - m_balanceHistory.First().Time).TotalDays);
            decimal dailyGainPercent = CalculateAverageDailyGain();

            var totalProfit = m_totalProfit;
            if (m_totalProfit == 0)
                totalProfit = 1;
            decimal lossProfitRatio = m_totalLoss / totalProfit;
            double nrmseBalance = CalculateEquityBalanceNormalizedRooMeanSquareError(m_balanceHistory);
            double nrmseAdg = CalculateAdgNormalizedRootMeanSquareError(m_balanceHistory, (double)dailyGainPercent);
            BacktestPerformanceResult result = new BacktestPerformanceResult
            {
                InitialBalance = initialBalance,
                FinalEquity = finalEquity,
                FinalBalance = finalEquity <= 0 ? 0 : finalBalance,
                LowestEquityToBalance = m_lowestEquityToBalance,
                UnrealizedPnl = m_lastBalance.Balance.UnrealizedPnl!.Value,
                RealizedPnl = m_lastBalance.Balance.RealizedPnl!.Value,
                AverageDailyGainPercent = dailyGainPercent,
                TotalDays = numberOfDays,
                ExpectedDays = (int)(m_tradingBotOptions.Value.BackTest.End - m_tradingBotOptions.Value.BackTest.Start).TotalDays,
                MaxDrawDown = m_maxDrawDown,
                LossProfitRatio = lossProfitRatio,
                SpotBalance = m_backTestExchange.SpotBalance,
                EquityBalanceNormalizedRooMeanSquareError = nrmseBalance,
                AdgNormalizedRootMeanSquareError = nrmseAdg,
            };
            Result = result;
            var directory = Path.Combine(m_options.Value.BackTestsDirectory, m_testId);
            Directory.CreateDirectory(directory);
            string filePath = Path.Combine(directory, m_tradingBotOptions.Value.BackTest.ResultFileName);

            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            string filePathDetailed = Path.Combine(directory, m_tradingBotOptions.Value.BackTest.ResultDetailedFileName);
            var openPositions = await m_backTestExchange.GetOpenPositionsWithOrdersAsync();
            result.OpenPositionWithOrders = openPositions;
            Result = result;
            json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePathDetailed, json);

            var botSettings = JsonSerializer.Serialize(m_tradingBotOptions.Value, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), botSettings);
            m_resultsSaved = true;
        }

        private double CalculateEquityBalanceNormalizedRooMeanSquareError(List<BalanceInTime> sampledBalanceHistory)
        {
            if (sampledBalanceHistory.Count == 0)
                return 0;
            var equity = sampledBalanceHistory
                .Select(b => (double)b.Balance.Equity!.Value)
                .ToArray();
            var balance = sampledBalanceHistory
                .Select(b => (double)(b.Balance.WalletBalance ?? 0))
                .ToArray();
            var cvrmse = TradingHelpers.NormalizedRootMeanSquareError(balance, equity);
            if (cvrmse.IsInfiniteOrNaN())
                return 0;
            return cvrmse;
        }

        private double CalculateAdgNormalizedRootMeanSquareError(List<BalanceInTime> sampledBalanceHistory, double averageDailyGainPercent)
        {
            if(sampledBalanceHistory.Count == 0)
                return 0;
            if (averageDailyGainPercent <= 0)
                return 0;
            decimal initialBalance = m_balanceHistory.First().Balance.WalletBalance!.Value + m_initialSpot;
            var expectedBalance = sampledBalanceHistory
                .Select(b =>
                {
                    var elapsedDays = (b.Time - m_tradingBotOptions.Value.BackTest.Start).TotalDays;
                    var adg = 1 + (averageDailyGainPercent / 100.0);
                    var multiplier = Math.Pow(adg, elapsedDays);
                    var expectedBalance = (double)initialBalance * multiplier;
                    return expectedBalance;
                })
                .ToArray();
            var sampledBalance = sampledBalanceHistory
                .Select(b => (double)b.TotalBalance)
                .ToArray();
            var cvrmse = TradingHelpers.NormalizedRootMeanSquareError(expectedBalance, sampledBalance);
            if (cvrmse.IsInfiniteOrNaN())
                return 0;
            return cvrmse;
        }

        private void PlotBalanceAndEquity()
        {
            var plt = new Plot();
            plt.XAxis.Label.Text = "Minutes";
            plt.XAxis.IsVisible = true;

            plt.YAxis.Label.Text = "Balance";
            plt.YAxis.IsVisible = true;

            plt.TitlePanel.Label.Text = "Balance and Equity";
            plt.TitlePanel.IsVisible = true;

            DateTime startTime = m_balanceHistory.First().Time;
            var walletBalanceInTime = m_balanceHistory
                .Select(x => new ScatterPlotPoint((x.Time - startTime).TotalMinutes, (double)x.Balance.WalletBalance!))
                .ToArray();
            var equityInTime = m_balanceHistory
                .Select(x => new ScatterPlotPoint((x.Time - startTime).TotalMinutes, (double)x.Balance.Equity!))
                .ToArray();
            plt.Add.Scatter(
                walletBalanceInTime.Select(x => x.X).ToArray(),
                walletBalanceInTime.Select(x => x.Y).ToArray(),
                Color.FromARGB((uint)System.Drawing.Color.Aqua.ToArgb()));
            plt.Add.Scatter(
                equityInTime.Select(x => x.X).ToArray(),
                equityInTime.Select(x => x.Y).ToArray(),
                Color.FromARGB((uint)System.Drawing.Color.DarkOrange.ToArgb()));

            if (m_tradingBotOptions.Value.SpotRebalancingRatio > 0)
            {
                var totalWalletBalanceInTime = m_balanceHistory
                    .Select(x => new ScatterPlotPoint((x.Time - startTime).TotalMinutes, (double)x.TotalBalance))
                    .ToArray();
                plt.Add.Scatter(
                    totalWalletBalanceInTime.Select(x => x.X).ToArray(),
                    totalWalletBalanceInTime.Select(x => x.Y).ToArray(),
                    Color.FromARGB((uint)System.Drawing.Color.Purple.ToArgb()));
            }

            if (m_balanceHistory.Count > 0)
            {
                var averageDailyGainPercent = CalculateAverageDailyGain();
                if (averageDailyGainPercent > 0)
                {
                    decimal initialBalance = m_balanceHistory.First().Balance.WalletBalance!.Value + m_initialSpot;
                    var expectedBalance = m_balanceHistory
                        .Select(x =>
                        {
                            var elapsedDays = (x.Time - m_tradingBotOptions.Value.BackTest.Start).TotalDays;
                            var adg = 1 + ((double)averageDailyGainPercent / 100.0);
                            var multiplier = Math.Pow(adg, elapsedDays);
                            var expectedBalance = (double)initialBalance * multiplier;
                            return new ScatterPlotPoint((x.Time - startTime).TotalMinutes, expectedBalance);
                        })
                        .ToArray();
                    plt.Add.Scatter(
                        expectedBalance.Select(x => x.X).ToArray(),
                        expectedBalance.Select(x => x.Y).ToArray(),
                        Color.FromARGB((uint)System.Drawing.Color.Green.ToArgb()));
                }
            }

            var directory = Path.Combine(m_options.Value.BackTestsDirectory, m_testId);
            Directory.CreateDirectory(directory);
            string filePath = Path.Combine(directory, "balance_and_equity_sampled.png");
            plt.SavePng(filePath, 2900, 1800);
        }
    }
}
