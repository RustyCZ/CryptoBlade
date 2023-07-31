using Bybit.Net;
using CryptoBlade.Configuration;
using CryptoBlade.HealthChecks;
using CryptoBlade.Helpers;
using CryptoBlade.Services;
using CryptoBlade.Strategies;
using CryptoBlade.Strategies.Wallet;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly.Bulkhead;

namespace CryptoBlade
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // hedge insane funding rates
            // better rate limiting
            // executed orders page
            // list of open orders page
            // bybit server time component, check against candle data in periodic verification check
            // Special readonly manager
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables("CB_");
            var tradingBotOptions = builder.Configuration.GetSection("TradingBot").Get<TradingBotOptions>();
            TradingMode tradingMode = tradingBotOptions!.TradingMode;

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddHealthChecks()
                .AddCheck<TradeExecutionHealthCheck>("TradeExecution");
            builder.Services.AddHostedService<TradingHostedService>();
            if (tradingMode == TradingMode.Readonly || tradingMode == TradingMode.Dynamic)
            {
                builder.Services.AddSingleton<ITradeStrategyManager, DynamicTradingStrategyManager>();
            }
            else
            {
                builder.Services.AddSingleton<ITradeStrategyManager, DefaultTradingStrategyManager>();
            }
            builder.Services.Configure<TradingBotOptions>(builder.Configuration.GetSection("TradingBot"));
            builder.Services.AddSingleton<ITradingStrategyFactory, TradingStrategyFactory>();
            builder.Services.AddSingleton<IWalletManager, WalletManager>();
            
            var exchangeAccount =
                tradingBotOptions?.Accounts.FirstOrDefault(x => string.Equals(x.Name, tradingBotOptions.AccountName, StringComparison.Ordinal));
            string apiKey = exchangeAccount?.ApiKey ?? string.Empty;
            string apiSecret = exchangeAccount?.ApiSecret ?? string.Empty;

            builder.Services.AddBybit(
                    restOptions =>
                    {
                        restOptions.V5Options.RateLimitingBehaviour = RateLimitingBehaviour.Wait;
                        restOptions.V5Options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                        restOptions.ReceiveWindow = TimeSpan.FromSeconds(10);
                        restOptions.AutoTimestamp = true;
                        restOptions.TimestampRecalculationInterval = TimeSpan.FromSeconds(10);
                    },
                    socketClientOptions =>
                    {
                        socketClientOptions.V5Options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
                    })
                .AddLogging(options =>
                {
                    options.SetMinimumLevel(LogLevel.Trace);
                    options.AddSimpleConsole(o =>
                    {
                        o.UseUtcTimestamp = true;
                        o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                    });
                });

            var app = builder.Build();
            var lf = app.Services.GetRequiredService<ILoggerFactory>();
            ApplicationLogging.LoggerFactory = lf;

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();
            app.MapHealthChecks("/healthz");
            app.MapRazorPages();
            app.Run();
        }
    }
}