using System.Text.Json;
using CryptoBlade.Configuration;
using CryptoBlade.Exchanges;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using ScottPlot;

namespace CryptoBlade.BackTesting
{
    public class BackTestPerformanceTracker : IHostedService
    {
        private readonly record struct BalanceInTime(Balance Balance, DateTime Time);
        private readonly record struct ScatterPlotPoint(double X, double Y);

        private readonly IOptions<BackTestPerformanceTrackerOptions> m_options;
        private readonly BackTestExchange m_backTestExchange;
        private IUpdateSubscription? m_walletSubscription;
        private decimal m_lowestEquityToBalance;
        private decimal m_maxDrawDown;
        private decimal m_localTopBalance;
        private readonly List<BalanceInTime> m_balanceHistory;
        private readonly AsyncLock m_lock;
        private readonly string m_testId;
        private readonly IHostApplicationLifetime m_applicationLifetime;
        private readonly ILogger<BackTestPerformanceTracker> m_logger;
        private readonly IOptions<TradingBotOptions> m_tradingBotOptions;

        public BackTestPerformanceTracker(IOptions<BackTestPerformanceTrackerOptions> options,
            IOptions<TradingBotOptions> tradingBotOptions,
            BackTestExchange backTestExchange, 
            IHostApplicationLifetime applicationLifetime, 
            ILogger<BackTestPerformanceTracker> logger)
        {
            m_balanceHistory = new List<BalanceInTime>();
            m_lowestEquityToBalance = 1.0m;
            m_backTestExchange = backTestExchange;
            m_applicationLifetime = applicationLifetime;
            m_logger = logger;
            m_options = options;
            m_lock = new AsyncLock();
            m_tradingBotOptions = tradingBotOptions;
            m_testId = $"{DateTime.Now:yyyyMMddHHmm}-{Guid.NewGuid().ToString("N")}";
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (await m_lock.LockAsync(cancellationToken))
            {
                var balance = await m_backTestExchange.GetBalancesAsync(cancellationToken);
                m_localTopBalance = balance.WalletBalance!.Value;
                m_maxDrawDown = 0.0m;
                var time = m_backTestExchange.CurrentTime;
                m_balanceHistory.Add(new BalanceInTime(balance, time));
                m_walletSubscription = await m_backTestExchange.SubscribeToWalletUpdatesAsync(OnWalletUpdate, cancellationToken);
            }
        }

        private void OnWalletUpdate(Balance obj)
        {
            using (m_lock.Lock())
            {
                var lastBalance = m_balanceHistory.Last();
                var time = m_backTestExchange.CurrentTime;
                if (time > lastBalance.Time)
                {
                    var equityToBalance = obj.Equity!.Value / obj.WalletBalance!.Value;
                    if (equityToBalance < m_lowestEquityToBalance)
                        m_lowestEquityToBalance = equityToBalance;
                    var walletBalance = obj.WalletBalance!.Value;
                    if (walletBalance > m_localTopBalance)
                        m_localTopBalance = walletBalance;
                    var drawDown = 1.0m - (walletBalance / m_localTopBalance);
                    if (drawDown > m_maxDrawDown)
                        m_maxDrawDown = drawDown;
                    m_balanceHistory.Add(new BalanceInTime(obj, time));
                    if (obj.Equity < 0)
                    {
                        // game over
                        m_applicationLifetime.StopApplication();
                    }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (m_walletSubscription != null)
            {
                await m_walletSubscription.CloseAsync();
                m_walletSubscription = null;
            }

            using (await m_lock.LockAsync(cancellationToken))
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
        }

        private async Task SaveBacktestResultsAsync()
        {
            decimal initialBalance = m_balanceHistory.First().Balance.WalletBalance!.Value;
            decimal finalBalance = m_balanceHistory.Last().Balance.WalletBalance!.Value;
            decimal finalEquity = m_balanceHistory.Last().Balance.Equity!.Value;
            decimal dailyGainPercent;
            int numberOfDays = (int)(m_balanceHistory.Last().Time - m_balanceHistory.First().Time).TotalDays;
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
            BacktestPerformanceResult result = new BacktestPerformanceResult
            {
                InitialBalance = initialBalance,
                FinalEquity = finalEquity,
                FinalBalance = finalEquity < 0 ? 0 : finalBalance,
                LowestEquityToBalance = m_lowestEquityToBalance,
                UnrealizedPnl = m_balanceHistory.Last().Balance.UnrealizedPnl!.Value,
                RealizedPnl = m_balanceHistory.Last().Balance.RealizedPnl!.Value,
                AverageDailyGainPercent = dailyGainPercent,
                TotalDays = numberOfDays,
                MaxDrawDown = m_maxDrawDown,
            };
            var directory = Path.Combine(m_options.Value.BackTestsDirectory, m_testId);
            Directory.CreateDirectory(directory);
            string filePath = Path.Combine(directory, m_tradingBotOptions.Value.BackTest.ResultFileName);

            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            string filePathDetailed = Path.Combine(directory, m_tradingBotOptions.Value.BackTest.ResultDetailedFileName);
            var openPositions = await m_backTestExchange.GetOpenPositionsWithOrdersAsync();
            result.OpenPositionWithOrders = openPositions;
            json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePathDetailed, json);

            var botSettings = JsonSerializer.Serialize(m_tradingBotOptions.Value, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), botSettings);
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

            var directory = Path.Combine(m_options.Value.BackTestsDirectory, m_testId);
            Directory.CreateDirectory(directory);
            string filePath = Path.Combine(directory, "balance_and_equity_sampled.png");
            plt.SavePng(filePath, 2900, 1800);
        }
    }

    public class BacktestPerformanceResult
    {
        public decimal InitialBalance { get; set; }
        public decimal FinalBalance { get; set; }
        public decimal FinalEquity { get; set; }
        public decimal LowestEquityToBalance { get; set; }
        public decimal UnrealizedPnl { get; set; }
        public decimal RealizedPnl { get; set; }
        public decimal AverageDailyGainPercent { get; set; }
        public decimal MaxDrawDown { get; set; }
        public int TotalDays { get; set; }
        public OpenPositionWithOrders[] OpenPositionWithOrders { get; set; } = Array.Empty<OpenPositionWithOrders>();
    }
}
