using System.Runtime.InteropServices;
using CryptoBlade.BackTesting.Model;
using CryptoBlade.Models;
using FASTER.core;

namespace CryptoBlade.BackTesting
{
    public class HistoricalSymbolStorage : IDisposable
    {
        private readonly FasterKV<long, TradeStoreModel> m_trades;
        private readonly FasterKV<long, CandleStoreModel> m_candles;
        private readonly FasterKV<long, DayStoreModel> m_days;

        public HistoricalSymbolStorage(string symbol, string directory)
        {
            var tradesSettings = new FasterKVSettings<long, TradeStoreModel>($"{directory}/{symbol.ToUpperInvariant()}/Trades");
            tradesSettings.TryRecoverLatest = true;
            tradesSettings.PageSize = 1024 * 1024 * 4;
            tradesSettings.SegmentSize = 1024 * 1024 * 4;
            tradesSettings.MemorySize = 1024 * 1024 * 256;
            tradesSettings.IndexSize = 1024 * 1024 * 4;
            m_trades = new FasterKV<long, TradeStoreModel>(tradesSettings);

            var candlesSettings = new FasterKVSettings<long, CandleStoreModel>($"{directory}/{symbol.ToUpperInvariant()}/Candles");
            candlesSettings.TryRecoverLatest = true;
            candlesSettings.PageSize = 1024 * 1024 * 4;
            candlesSettings.SegmentSize = 1024 * 1024 * 4;
            candlesSettings.MemorySize = 1024 * 1024 * 256;
            candlesSettings.IndexSize = 1024 * 1024 * 4;
            m_candles = new FasterKV<long, CandleStoreModel>(candlesSettings);

            var daysSettings = new FasterKVSettings<long, DayStoreModel>($"{directory}/{symbol.ToUpperInvariant()}/Days");
            daysSettings.TryRecoverLatest = true;
            daysSettings.PageSize = 1024 * 1024 * 4;
            daysSettings.SegmentSize = 1024 * 1024 * 4;
            daysSettings.MemorySize = 1024 * 1024 * 256;
            daysSettings.IndexSize = 1024 * 1024 * 4;
            m_days = new FasterKV<long, DayStoreModel>(daysSettings);
        }

        public async Task<HistoricalDayData> ReadAsync(DateTime day)
        {
            HistoricalDayData historicalDayData = new HistoricalDayData
            {
                Day = day,
            };
            var result = new List<Trade>();
            var dayStoreFunctions = new SimpleFunctions<long, DayStoreModel>((_, d2) => d2);
            using var daySession = m_days.NewSession(dayStoreFunctions);
            var dayModel = await daySession.ReadAsync(day.Ticks);
            if (!dayModel.Status.Found)
                return historicalDayData;
            var tradeStoreFuncs = new SimpleFunctions<long, TradeStoreModel>((_, d2) => d2);
            using var session2 = m_trades.NewSession(tradeStoreFuncs);
            var current = dayModel.Output.TradeStartIndex;
            while (current != 0)
            {
                var tradeModel = await session2.ReadAsync(current);
                if (!tradeModel.Status.Found)
                    break;
                result.Add(new Trade
                {
                    Size = tradeModel.Output.Size,
                    Price = tradeModel.Output.Price,
                    TimestampDateTime = new DateTime(current, DateTimeKind.Utc),
                });
                current = tradeModel.Output.NextIndex;
            }
            historicalDayData.Trades = result.ToArray();
            
            var candles = new List<Candle>();
            var candleStoreFunctions = new SimpleFunctions<long, CandleStoreModel>((_, d2) => d2);
            using var session3 = m_candles.NewSession(candleStoreFunctions);
            var candleStartIndex = dayModel.Output.CandleStartIndex;
            var candleEndIndex = dayModel.Output.CandleEndIndex;
            for (long i = candleStartIndex; i < candleEndIndex; i++)
            {
                var candleModel = await session3.ReadAsync(i);
                if (!candleModel.Status.Found)
                    continue;
                candles.Add(new Candle
                {
                    TimeFrame = (TimeFrame)candleModel.Output.TimeFrame,
                    StartTime = new DateTime(candleModel.Output.StartTime, DateTimeKind.Utc),
                    Open = candleModel.Output.Open,
                    High = candleModel.Output.High,
                    Low = candleModel.Output.Low,
                    Close = candleModel.Output.Close,
                    Volume = candleModel.Output.Volume,
                });
            }
            historicalDayData.Candles = candles.ToArray();
            return historicalDayData;
        }

        public async Task<DateTime[]> FindMissingDaysAsync(DateTime start, DateTime end)
        {
            var result = new List<DateTime>();
            var current = start;
            var funcs = new SimpleFunctions<long, DayStoreModel>((_, d2) => d2);
            using var session = m_days.NewSession(funcs);
            while (current <= end)
            {
                var day = current.Date;
                var dayModel = await session.ReadAsync(current.Ticks);
                if(!dayModel.Status.Found)
                    result.Add(day);
                current = current.AddDays(1);
            }
            return result.ToArray();
        }

        public async Task StoreAsync(HistoricalDayData dayData, bool flush)
        {
            var trades = dayData.Trades;
            var candles = dayData.Candles;
            var day = dayData.Day.Date;
            var funcs = new SimpleFunctions<long, TradeStoreModel>((_, current) => current);
            using var tradesSession = m_trades.NewSession(funcs);
            long tradeStartIndex = 0;
            long tradesEndIndex = 0;
            if (trades.Length > 0)
            {
                tradeStartIndex = trades[0].TimestampDateTime.Ticks;
                tradesEndIndex = trades[^1].TimestampDateTime.Ticks;
            }

            for (int i = 0; i < trades.Length; i++)
            {
                var trade = trades[i];
                long nextIndex = 0;
                if (i < trades.Length - 1)
                    nextIndex = trades[i + 1].TimestampDateTime.Ticks;
                var tradeStoreModel = new TradeStoreModel
                {
                    Size = trade.Size,
                    Price = trade.Price,
                    NextIndex = nextIndex,
                };
                await tradesSession.UpsertAsync(trade.TimestampDateTime.Ticks, tradeStoreModel);
            }

            long candlesStartIndex = 0;
            long candlesEndIndex = candles.Length;
            if (candles.Length > 0)
            {
                candlesStartIndex = candles[0].StartTime.Ticks;
                // store it as continuous index because there are multiple candles with the same start time
                // and there is enough space in the long to store it
                candlesEndIndex = candlesStartIndex + candles.Length;
            }

            long currentCandleIndex = candlesStartIndex;
            var candlesFuncs = new SimpleFunctions<long, CandleStoreModel>((_, current) => current);
            using var candlesSession = m_candles.NewSession(candlesFuncs);
            foreach (var candle in candles)
            {
                var candleStoreModel = new CandleStoreModel
                {
                    Open = candle.Open,
                    High = candle.High,
                    Low = candle.Low,
                    Close = candle.Close,
                    Volume = candle.Volume,
                    TimeFrame = (int)candle.TimeFrame,
                    StartTime = candle.StartTime.Ticks,
                };
                await candlesSession.UpsertAsync(currentCandleIndex, candleStoreModel);
                currentCandleIndex++;
            }

            var dayModel = new DayStoreModel
            {
                TradeStartIndex = tradeStartIndex,
                TradeEndIndex = tradesEndIndex,
                CandleStartIndex = candlesStartIndex,
                CandleEndIndex = candlesEndIndex,
                HasCandleData = candles.Any(),
                HasTradeData = trades.Any(),
            };
            var dayFuncs = new SimpleFunctions<long, DayStoreModel>((_, current) => current);
            using var daysSession = m_days.NewSession(dayFuncs);
            await daysSession.UpsertAsync(day.Ticks, dayModel);

            if (flush)
            {
                m_trades.TryInitiateFullCheckpoint(out _, CheckpointType.Snapshot);
                await m_trades.CompleteCheckpointAsync();
                m_trades.Log.Flush(true);

                m_candles.TryInitiateFullCheckpoint(out _, CheckpointType.Snapshot);
                await m_candles.CompleteCheckpointAsync();
                m_candles.Log.Flush(true);

                m_days.TryInitiateFullCheckpoint(out _, CheckpointType.Snapshot);
                await m_days.CompleteCheckpointAsync();
                m_days.Log.Flush(true);
            }
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct TradeStoreModel
        {
            public decimal Size;
            public decimal Price;
            public long NextIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DayStoreModel
        {
            public long TradeStartIndex;
            public long TradeEndIndex;
            public long CandleStartIndex;
            public long CandleEndIndex;
            public bool HasTradeData;
            public bool HasCandleData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CandleStoreModel
        {
            public long StartTime;
            public int TimeFrame;
            public decimal Open;
            public decimal High;
            public decimal Low;
            public decimal Close;
            public decimal Volume;
        }

        public void Dispose()
        {
            m_trades.Dispose();
            m_days.Dispose();
        }
    }
}