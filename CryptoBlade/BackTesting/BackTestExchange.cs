using CryptoBlade.Exchanges;
using CryptoBlade.Mapping;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace CryptoBlade.BackTesting
{
    public class BackTestExchange : ICbFuturesRestClient, ICbFuturesSocketClient, IBackTestRunner
    {
        private readonly IOptions<BackTestExchangeOptions> m_options;
        private readonly IBackTestDataDownloader m_backTestDataDownloader;
        private readonly IHistoricalDataStorage m_historicalDataStorage;
        private DateTime m_currentTime;
        private readonly Dictionary<string, CandleBacktestProcessor> m_candleProcessors;
        private readonly AsyncLock m_lock;
        private readonly HashSet<CandleUpdateSubscription> m_candleSubscriptions;
        private readonly HashSet<TickerUpdateSubscription> m_tickerSubscriptions;
        private readonly HashSet<BalanceUpdateSubscription> m_balanceSubscriptions;
        private readonly HashSet<OrderUpdateSubscription> m_orderSubscriptions;
        private readonly ICbFuturesRestClient m_cbFuturesRestClient;
        private Balance m_currentBalance;
        private readonly Dictionary<string, HashSet<Order>> m_openOrders;
        private readonly Dictionary<string, OpenPositionWithOrders> m_longPositions;
        private readonly Dictionary<string, OpenPositionWithOrders> m_shortPositions;

        public BackTestExchange(IOptions<BackTestExchangeOptions> options, 
            IBackTestDataDownloader backTestDataDownloader, 
            IHistoricalDataStorage historicalDataStorage, 
            ICbFuturesRestClient cbFuturesRestClient)
        {
            m_lock = new AsyncLock();
            m_candleSubscriptions = new HashSet<CandleUpdateSubscription>();
            m_tickerSubscriptions = new HashSet<TickerUpdateSubscription>();
            m_balanceSubscriptions = new HashSet<BalanceUpdateSubscription>();
            m_orderSubscriptions = new HashSet<OrderUpdateSubscription>();
            m_candleProcessors = new Dictionary<string, CandleBacktestProcessor>();
            m_backTestDataDownloader = backTestDataDownloader;
            m_historicalDataStorage = historicalDataStorage;
            m_cbFuturesRestClient = cbFuturesRestClient;
            m_options = options;
            m_currentTime = m_options.Value.Start;
            m_currentBalance = new Balance(m_options.Value.InitialBalance, 0m, 0m, 0m);
            m_openOrders = new Dictionary<string, HashSet<Order>>();
            m_longPositions = new Dictionary<string, OpenPositionWithOrders>();
            m_shortPositions = new Dictionary<string, OpenPositionWithOrders>();
        }

        public Task<bool> SetLeverageAsync(SymbolInfo symbol, CancellationToken cancel = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SwitchPositionModeAsync(PositionMode mode, string symbol, CancellationToken cancel = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SwitchCrossIsolatedMarginAsync(SymbolInfo symbol, TradeMode tradeMode, CancellationToken cancel = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CancelOrderAsync(string symbol, string orderId, CancellationToken cancel = default)
        {
            return Task.FromResult(true);
        }

        public async Task<bool> PlaceLimitBuyOrderAsync(string symbol, decimal quantity, decimal price, CancellationToken cancel = default)
        {
            Order order;
            using (await m_lock.LockAsync())
            {
                var currentBalance = m_currentBalance;
                if (currentBalance.Equity < quantity * price)
                    return false;
                order = new Order
                {
                    Symbol = symbol,
                    Status = OrderStatus.Filled,
                    AveragePrice = price,
                    Side = OrderSide.Buy,
                    Price = price,
                    Quantity = quantity,
                    OrderId = Guid.NewGuid().ToString(),
                    PositionMode = OrderPositionMode.BothSideBuy,
                    QuantityFilled = quantity,
                    ValueFilled = quantity * price,
                    QuantityRemaining = 0,
                    ReduceOnly = false,
                    ValueRemaining = 0,
                };
                if (!m_longPositions.TryGetValue(symbol, out var openPosition))
                {
                    openPosition = new OpenPositionWithOrders(order);
                    m_longPositions.Add(symbol, openPosition);
                }
                else
                {
                    openPosition.AddOrder(order);
                }
            }

            await NotifyOrderAsync(order);

            return true;
        }

        public async Task<bool> PlaceLimitSellOrderAsync(string symbol, decimal quantity, decimal price, CancellationToken cancel = default)
        {
            Order order;
            using (await m_lock.LockAsync())
            {
                var currentBalance = m_currentBalance;
                if (currentBalance.Equity < quantity * price)
                    return false;
                order = new Order
                {
                    Symbol = symbol,
                    Status = OrderStatus.Filled,
                    AveragePrice = price,
                    Side = OrderSide.Sell,
                    Price = price,
                    Quantity = quantity,
                    OrderId = Guid.NewGuid().ToString(),
                    PositionMode = OrderPositionMode.BothSideSell,
                    QuantityFilled = quantity,
                    ValueFilled = quantity * price,
                    QuantityRemaining = 0,
                    ReduceOnly = false,
                    ValueRemaining = 0,
                };
                if (!m_shortPositions.TryGetValue(symbol, out var openPosition))
                {
                    openPosition = new OpenPositionWithOrders(order);
                    m_shortPositions.Add(symbol, openPosition);
                }
                else
                {
                    openPosition.AddOrder(order);
                }
            }

            await NotifyOrderAsync(order);

            return true;
        }

        public async Task<bool> PlaceLongTakeProfitOrderAsync(string symbol, decimal qty, decimal price, CancellationToken cancel = default)
        {
            Order order;
            using (await m_lock.LockAsync())
            {
                if (!m_longPositions.TryGetValue(symbol, out var openPosition))
                    return false;
                var position = openPosition.Position;
                if (position.Quantity < qty)
                    return false;
                order = new Order
                {
                    Symbol = symbol,
                    AveragePrice = price,
                    Status = OrderStatus.New,
                    Side = OrderSide.Sell,
                    Price = price,
                    Quantity = qty,
                    OrderId = Guid.NewGuid().ToString(),
                    PositionMode = OrderPositionMode.BothSideBuy,
                    QuantityFilled = 0,
                    ValueFilled = 0,
                    QuantityRemaining = qty,
                    ReduceOnly = true,
                    ValueRemaining = qty * price,
                };

                if (!m_openOrders.TryGetValue(symbol, out var openOrders))
                {
                    openOrders = new HashSet<Order>();
                    m_openOrders.Add(symbol, openOrders);
                }
                openOrders.Add(order);
            }

            await NotifyOrderAsync(order);

            return true;
        }

        public async Task<bool> PlaceShortTakeProfitOrderAsync(string symbol, decimal qty, decimal price, CancellationToken cancel = default)
        {
            Order order;
            using (await m_lock.LockAsync())
            {
                if (!m_shortPositions.TryGetValue(symbol, out var openPosition))
                    return false;
                var position = openPosition.Position;
                if (position.Quantity < qty)
                    return false;
                order = new Order
                {
                    Symbol = symbol,
                    AveragePrice = price,
                    Status = OrderStatus.New,
                    Side = OrderSide.Buy,
                    Price = price,
                    Quantity = qty,
                    OrderId = Guid.NewGuid().ToString(),
                    PositionMode = OrderPositionMode.BothSideSell,
                    QuantityFilled = 0,
                    ValueFilled = 0,
                    QuantityRemaining = qty,
                    ReduceOnly = true,
                    ValueRemaining = qty * price,
                };
                if (!m_openOrders.TryGetValue(symbol, out var openOrders))
                {
                    openOrders = new HashSet<Order>();
                    m_openOrders.Add(symbol, openOrders);
                }
                openOrders.Add(order);
            }

            await NotifyOrderAsync(order);

            return true;
        }

        public Task<Balance> GetBalancesAsync(CancellationToken cancel = default)
        {
            return Task.FromResult(m_currentBalance);
        }

        public async Task<SymbolInfo[]> GetSymbolInfoAsync(CancellationToken cancel = default)
        {
            var symbolInfo = await m_cbFuturesRestClient.GetSymbolInfoAsync(cancel);
            return symbolInfo.Where(x => m_options.Value.Symbols.Contains(x.Name)).ToArray();
        }

        public async Task<Candle[]> GetKlinesAsync(string symbol, TimeFrame interval, int limit, CancellationToken cancel = default)
        {
            var currentTime = m_currentTime;
            if (currentTime != m_options.Value.Start)
            {
                throw new InvalidOperationException("There is something wrong with the logic or data. We should call this only at the beginning.");
            }
            var currentDay = currentTime.Date;
            var previousDay = currentDay.AddDays(-1);
            var previousDayData = await m_historicalDataStorage.ReadAsync(symbol, previousDay, cancel);
            var currentDayData = await m_historicalDataStorage.ReadAsync(symbol, currentDay, cancel);
            var candles = previousDayData.Candles.Concat(currentDayData.Candles).ToArray();
            var currentCandles = candles
                .Where(x => x.TimeFrame == interval)
                .Where(x => x.StartTime.Add(x.TimeFrame.ToTimeSpan()) <= currentTime)
                .OrderByDescending(x => x.StartTime).Take(limit)
                .Reverse()
                .ToArray();
            return currentCandles.ToArray();
        }

        public Task<Candle[]> GetKlinesAsync(string symbol, TimeFrame interval, DateTime start, DateTime end,
            CancellationToken cancel = default)
        {
            throw new NotSupportedException("We don't need this for backtesting at the moment.");
        }

        public async Task<Ticker> GetTickerAsync(string symbol, CancellationToken cancel = default)
        {
            var currentTime = m_currentTime;
            var currentTimeOnMinute = new DateTime(
                currentTime.Year, 
                currentTime.Month, 
                currentTime.Day, 
                currentTime.Hour, 
                currentTime.Minute, 
                0, DateTimeKind.Utc);
            var currentDay = currentTime.Date;
            var currentDayData = await m_historicalDataStorage.ReadAsync(symbol, currentDay, cancel);
            var currentCandle = currentDayData.Candles
                .FirstOrDefault(x => x.TimeFrame == TimeFrame.OneMinute && x.StartTime == currentTimeOnMinute);
            if (currentCandle == null)
            {
                return new Ticker();
            }
            var typicalPrice = (currentCandle.High + currentCandle.Low + currentCandle.Close) / 3;
            return new Ticker
            {
                FundingRate = null,
                BestAskPrice = typicalPrice,
                BestBidPrice = typicalPrice,
                LastPrice = typicalPrice,
            };
        }

        public async Task<Order[]> GetOrdersAsync(CancellationToken cancel = default)
        {
            Order[] orders;
            using (await m_lock.LockAsync())
                orders = m_openOrders.Values.Select(x => x.ToArray()).SelectMany(x => x).ToArray();

            return orders;
        }

        public async Task<Position[]> GetPositionsAsync(CancellationToken cancel = default)
        {
            Position[] positions;
            using (await m_lock.LockAsync())
            {
                positions = m_longPositions.Values
                    .Select(x => x.Position)
                    .Concat(m_shortPositions.Values.Select(x => x.Position))
                    .ToArray();
            }

            return positions;
        }

        public Task<IUpdateSubscription> SubscribeToWalletUpdatesAsync(Action<Balance> handler, CancellationToken cancel = default)
        {
            var subscription = new BalanceUpdateSubscription(this, handler);
            m_balanceSubscriptions.Add(subscription);
            return Task.FromResult<IUpdateSubscription>(subscription);
        }

        public Task<IUpdateSubscription> SubscribeToOrderUpdatesAsync(Action<OrderUpdate> handler, CancellationToken cancel = default)
        {
            var subscription = new OrderUpdateSubscription(this, handler);
            m_orderSubscriptions.Add(subscription);
            return Task.FromResult<IUpdateSubscription>(subscription);
        }

        public Task<IUpdateSubscription> SubscribeToKlineUpdatesAsync(string[] symbols, TimeFrame timeFrame, Action<string, Candle> handler,
            CancellationToken cancel = default)
        {
            CandleUpdateSubscription subscription = new CandleUpdateSubscription(this, handler, symbols, timeFrame);
            m_candleSubscriptions.Add(subscription);
            return Task.FromResult<IUpdateSubscription>(subscription);
        }

        public Task<IUpdateSubscription> SubscribeToTickerUpdatesAsync(string[] symbols, Action<string, Ticker> handler, CancellationToken cancel = default)
        {
            TickerUpdateSubscription subscription = new TickerUpdateSubscription(this, handler, symbols);
            m_tickerSubscriptions.Add(subscription);
            return Task.FromResult<IUpdateSubscription>(subscription);
        }

        public async Task PrepareDataAsync(CancellationToken cancel = default)
        {
            var start = m_options.Value.Start;
            var end = m_options.Value.End;
            start -= m_options.Value.StartupCandleData;
            start = start.Date;
            end = end.Date;
            var symbols = m_options.Value.Symbols;
            await m_backTestDataDownloader.DownloadDataForBackTestAsync(symbols, start, end, cancel);
            await LoadDataForDayAsync(m_currentTime.Date, cancel);
        }

        public async Task<bool> MoveNextAsync(CancellationToken cancel = default)
        {
            var currentTime = m_currentTime;
            foreach (var backtestProcessor in m_candleProcessors)
            {
                Candle[] candles = backtestProcessor.Value.MoveNext(currentTime);
                foreach (var candle in candles)
                {
                    // we should probably use trades data for this
                    if (candle.TimeFrame == TimeFrame.OneMinute)
                    {
                        foreach (TickerUpdateSubscription tickerUpdateSubscription in m_tickerSubscriptions)
                        {
                            var typicalPrice = (candle.High + candle.Low + candle.Close) / 3.0m;
                            tickerUpdateSubscription.Notify(backtestProcessor.Key, new Ticker
                            {
                                FundingRate = null,
                                BestAskPrice = typicalPrice,
                                BestBidPrice = typicalPrice,
                                LastPrice = typicalPrice,
                            });
                        }
                    }
                    foreach (var subscription in m_candleSubscriptions)
                        subscription.Notify(backtestProcessor.Key, candle);
                }
            }

            DateTime nextTime = currentTime.AddMinutes(1);

            if (nextTime.Date != currentTime.Date)
                await LoadDataForDayAsync(nextTime.Date, cancel);
                
            m_currentTime = nextTime;
            bool hasMoreData = m_currentTime < m_options.Value.End;
            return hasMoreData;
        }

        private Task ProcessPositionsAndOrdersAsync(string symbol, Candle candle)
        {
            return Task.CompletedTask;
        }

        private async Task LoadDataForDayAsync(DateTime day, CancellationToken cancel = default)
        {
            var symbols = m_options.Value.Symbols;
            m_candleProcessors.Clear();
            foreach (string symbol in symbols)
            {
                var dayData = await m_historicalDataStorage.ReadAsync(symbol, day, cancel);
                var processor = new CandleBacktestProcessor(dayData);
                m_candleProcessors[symbol] = processor;
            }
        }

        private Task NotifyBalanceAsync(Balance balance)
        {
            m_currentBalance = balance;
            foreach (var subscription in m_balanceSubscriptions)
                subscription.Notify(balance);
            return Task.CompletedTask;
        }

        private Task NotifyOrderAsync(Order order)
        {
            OrderUpdate orderUpdate = new OrderUpdate
            {
                OrderId = order.OrderId,
                Status = order.Status,
                Symbol = order.Symbol,
            };
            foreach (var subscription in m_orderSubscriptions)
                subscription.Notify(orderUpdate);
            return Task.CompletedTask;
        }

        private class OpenPositionWithOrders
        {
            private readonly List<Order> m_filledOrders;

            public OpenPositionWithOrders(Order filledOrder)
            {
                m_filledOrders = new List<Order>
                {
                    filledOrder
                };
            }

            public void AddOrder(Order order)
            {
                m_filledOrders.Add(order);
            }

            public Position Position
            {
                get
                {
                    var firstOrder = m_filledOrders.First();
                    var averagePrice = m_filledOrders.Sum(x => x.Price!.Value * x.Quantity) / m_filledOrders.Sum(x => x.Quantity);
                    var position = new Position
                    {
                        Symbol = firstOrder.Symbol,
                        Side = firstOrder.Side == OrderSide.Buy ? PositionSide.Buy : PositionSide.Sell,
                        TradeMode = TradeMode.CrossMargin,
                        Quantity = m_filledOrders.Sum(x => x.Quantity),
                        AveragePrice = averagePrice,
                    };
                    return position;
                }
            }
        }

        #region Subscriptions
        private class CandleUpdateSubscription : IUpdateSubscription
        {
            private readonly BackTestExchange m_exchange;
            private readonly Action<string, Candle> m_handler;
            private readonly HashSet<string> m_symbols;
            private readonly TimeFrame m_timeFrame;

            public CandleUpdateSubscription(BackTestExchange exchange, Action<string, Candle> handler, string[] symbols, TimeFrame timeFrame)
            {
                m_exchange = exchange;
                m_handler = handler;
                m_timeFrame = timeFrame;
                m_symbols = new HashSet<string>(symbols);
            }

            public void Notify(string symbol, Candle candle)
            {
                if(m_symbols.Contains(symbol) && candle.TimeFrame == m_timeFrame)
                    m_handler(symbol, candle);
            }

            public void AutoReconnect(ILogger logger)
            {
                // no need to reconnect
            }

            public async Task CloseAsync()
            {
                using var l = await m_exchange.m_lock.LockAsync();
                m_exchange.m_candleSubscriptions.Remove(this);
            }
        }

        private class BalanceUpdateSubscription : IUpdateSubscription
        {
            private readonly BackTestExchange m_exchange;
            private readonly Action<Balance> m_handler;

            public BalanceUpdateSubscription(BackTestExchange exchange, Action<Balance> handler)
            {
                m_exchange = exchange;
                m_handler = handler;
            }

            public void Notify(Balance balance)
            {
                m_handler(balance);
            }

            public void AutoReconnect(ILogger logger)
            {
                // no need to reconnect
            }

            public async Task CloseAsync()
            {
                using var l = await m_exchange.m_lock.LockAsync();
                m_exchange.m_balanceSubscriptions.Remove(this);
            }
        }

        private class TickerUpdateSubscription : IUpdateSubscription
        {
            private readonly BackTestExchange m_exchange;
            private readonly Action<string, Ticker> m_handler;
            private readonly HashSet<string> m_symbols;

            public TickerUpdateSubscription(BackTestExchange exchange, Action<string, Ticker> handler, string[] symbols)
            {
                m_exchange = exchange;
                m_handler = handler;
                m_symbols = new HashSet<string>(symbols);
            }

            public void Notify(string symbol, Ticker ticker)
            {
                if(m_symbols.Contains(symbol))
                    m_handler(symbol, ticker);
            }

            public void AutoReconnect(ILogger logger)
            {
                // no need to reconnect
            }

            public async Task CloseAsync()
            {
                using var l = await m_exchange.m_lock.LockAsync();
                m_exchange.m_tickerSubscriptions.Remove(this);
            }
        }

        private class OrderUpdateSubscription : IUpdateSubscription
        {
            private readonly BackTestExchange m_exchange;
            private readonly Action<OrderUpdate> m_handler;

            public OrderUpdateSubscription(BackTestExchange exchange, Action<OrderUpdate> handler)
            {
                m_exchange = exchange;
                m_handler = handler;
            }

            public void Notify(OrderUpdate order)
            {
                m_handler(order);
            }

            public void AutoReconnect(ILogger logger)
            {
                // no need to reconnect
            }

            public async Task CloseAsync()
            {
                using var l = await m_exchange.m_lock.LockAsync();
                m_exchange.m_orderSubscriptions.Remove(this);
            }
        }
        #endregion // Subscriptions
    }
}
