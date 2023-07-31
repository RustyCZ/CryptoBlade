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

### Dynamic mode
The Dynamic mode is designed to dynamically manage the number of running trading strategies based on specific exposure targets for long and short positions. It allows the bot to adjust the number of open positions to maintain the desired overall exposure in the market.

#### Target Long Exposure and Target Short Exposure
In Dynamic mode, two important parameters are used to control the bot's exposure:
- Target Long Exposure: This parameter represents the target exposure level the bot aims to maintain for long positions. It is expressed as a percentage of the total wallet balance.
- Target Short Exposure: Similarly, the Target Short Exposure represents the target exposure level for short positions.

The bot will stop openning new positions once target exposures are breached.

#### Maximum Long and Short Strategies
To prevent overtrading and to manage risk, Dynamic mode includes two additional parameters:
- MaxLongStrategies: This parameter sets the maximum number of strategies allowed to open long positions.
- MaxShortStrategies: Similarly, MaxShortStrategies sets the maximum number of strategies allowed to open short positions.

The bot adheres to these limits and does not open new positions beyond the maximum specified, even if the target exposure level is not met. It waits for existing strategies to close positions and bring the exposure closer to the target level.

#### Wallet Exposure for Individual Positions
In addition to managing the overall exposure, bot also enforces limits on individual position sizes:
- WalletExposureLong: This parameter sets the maximum exposure the bot is willing to take for each individual long position. It is expressed as a percentage of the total trading capital or wallet balance.
- WalletExposureShort: Similarly, WalletExposureShort sets the maximum exposure for each individual short position.

# Disclaimer
CryptoBlade is an open-source project and comes with no guarantees or warranties. Please use it at your own risk and with caution. The authors are not responsible for any financial losses that may occur while using this bot.
