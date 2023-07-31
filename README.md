# CryptoBlade
CryptoBlade is a simple trading bot written in C# for cryptocurrency trading. This bot is designed to execute trades based on various strategies and indicators.
**Note**: If you are interested in a specific trading strategy, check out [DirectionalScalper](https://github.com/donewiththedollar/directionalscalper), a popular repository that implements directional scalping strategy. This bot is inspired by directionalscalper and rotates strategies based on volume.

## Getting Started

Follow the instructions below to get started with the CryptoBlade Trading Bot:

1. Clone this repository: `git clone https://github.com/your-username/CryptoBlade.git`
2. Install the required dependencies using .NET Core.
3. Configure the `appsettings.json` file with your exchange API keys and other settings or use environment variables in step 5. (see the Configuration section).
4. Build docker `docker build -f "CryptoBlade/Dockerfile" -t cryptoblade:latest .`
5. Use docker compose to run bot
6. Run the bot and start trading!

## Configuration

Before running the CryptoBlade bot, you need to configure the settings in the `appsettings.json` file or using environment variables. For sample configuration see Samples. Below is an explanation of each configuration option:
In the `appsettings.json` file, you can customize various settings to tailor the bot to your trading preferences.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",

  "TradingBot": {
    "Accounts": [
      {
        "Name": "CryptoBlade01",
        "ApiKey": "YOUR_API_KEY",
        "ApiSecret": "YOUR_API_SECRET",
        "Exchange": "Bybit"
      }
    ],
    "AccountName": "CryptoBlade01",
    "StrategyName": "MfiRsiEriTrend",
    "MaxRunningStrategies": 15,
    "MinimumVolume": 15000.0,
    "MinimumPriceDistance": 0.015,
    "WalletExposureLong": 2.0,
    "WalletExposureShort": 2.0,
    "ForceMinQty": false,
    "PlaceOrderAttempts": 5,
    "TradingMode": "Readonly",
    "DcaOrdersCount": 1000,
    "DynamicBotCount": {
      "TargetLongExposure": 2.0,
      "TargetShortExposure": 1.0,
      "MaxLongStrategies": 50,
      "MaxShortStrategies": 50,
      "MaxDynamicStrategyOpenPerStep": 6,
      "Step": "0.00:03:00"
    },
    "Whitelist": [
    ],
    "Blacklist": [],
    "SymbolTradingModes": [
    ]
  }
}
```
### Trading Modes
- Normal: In normal mode, the bot operates with full trading capabilities, executing buy and sell orders based on the selected strategy and indicators. Strategies tend to enter bot long and short positions.
- Dynamic: In dynamic mode, the bot dynamically adjusts the number of running strategies based on market conditions and target exposure. This mode allows for a more adaptive trading approach.
- GracefulShutdown: In graceful shutdown mode, the bot closes all running strategies and stops trading in a controlled manner.
- Readonly: In readonly mode, the bot only reads market data and performs analysis without placing any actual trades. This is useful for testing and observing strategies without executing real trades.

### ForceMinQty
The ForceMinQty setting, when set to true, indicates that the CryptoBlade Trading Bot will always enter a position by forcing the minimum quantity required by the exchange, even if the available balance is not sufficient for the entire DCA order.

# Disclaimer
CryptoBlade is an open-source project and comes with no guarantees or warranties. Please use it at your own risk and with caution. The authors are not responsible for any financial losses that may occur while using this bot.
