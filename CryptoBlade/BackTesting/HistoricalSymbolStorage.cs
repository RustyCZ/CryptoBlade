using System.Runtime.InteropServices;
using CryptoBlade.BackTesting.Model;
using FASTER.core;

namespace CryptoBlade.BackTesting
{
    public class HistoricalSymbolStorage : IDisposable
    {
        private readonly FasterKV<long, TradeStoreModel> m_trades;
        private readonly FasterKV<long, DayStoreModel> m_days;

        public HistoricalSymbolStorage(string symbol, string directory)
        {
            var settings = new FasterKVSettings<long, TradeStoreModel>($"{directory}/{symbol.ToUpperInvariant()}/Trades");
            settings.TryRecoverLatest = true;
            settings.PageSize = 1024 * 1024 * 64;
            settings.SegmentSize = 1024 * 1024 * 64;
            settings.MemorySize = 1024 * 1024 * 1024;
            settings.IndexSize = 1024 * 1024 * 64;
            m_trades = new FasterKV<long, TradeStoreModel>(settings);

            var settings2 = new FasterKVSettings<long, DayStoreModel>($"{directory}/{symbol.ToUpperInvariant()}/Days");
            settings2.TryRecoverLatest = true;
            settings2.PageSize = 1024 * 1024 * 4;
            settings2.SegmentSize = 1024 * 1024 * 4;
            settings2.MemorySize = 1024 * 1024 * 4;
            settings2.IndexSize = 1024 * 1024 * 4;
            m_days = new FasterKV<long, DayStoreModel>(settings2);
        }

        public async Task<Trade[]> ReadAsync(DateTime day)
        {
            var result = new List<Trade>();
            var funcs = new SimpleFunctions<long, DayStoreModel>((_, d2) => d2);
            using var session = m_days.NewSession(funcs);
            var dayModel = await session.ReadAsync(day.Ticks);
            if (!dayModel.Status.Found)
                return result.ToArray();
            var funcs2 = new SimpleFunctions<long, TradeStoreModel>((_, d2) => d2);
            using var session2 = m_trades.NewSession(funcs2);
            var current = dayModel.Output.StartIndex;
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
            return result.ToArray();
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

        public async Task StoreAsync(DateTime day, Trade[] trades, bool flush)
        {
            var funcs = new SimpleFunctions<long, TradeStoreModel>((_, current) => current);
            using var session = m_trades.NewSession(funcs);
            long startIndex = 0;
            long endIndex = 0;
            if (trades.Length > 0)
            {
                startIndex = trades[0].TimestampDateTime.Ticks;
                endIndex = trades[^1].TimestampDateTime.Ticks;
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
                await session.UpsertAsync(trade.TimestampDateTime.Ticks, tradeStoreModel);
            }

            if (flush)
            {
                m_trades.TryInitiateFullCheckpoint(out _, CheckpointType.Snapshot);
                await m_trades.CompleteCheckpointAsync();
                m_trades.Log.Flush(true);
            }

            var dayModel = new DayStoreModel
            {
                StartIndex = startIndex,
                EndIndex = endIndex,
                HasData = trades.Any(),
            };
            var dayFuncs = new SimpleFunctions<long, DayStoreModel>((_, current) => current);
            using var session2 = m_days.NewSession(dayFuncs);
            await session2.UpsertAsync(day.Ticks, dayModel);

            if (flush)
            {
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
            public long StartIndex;
            public long EndIndex;
            public bool HasData;
        }

        public void Dispose()
        {
            m_trades.Dispose();
            m_days.Dispose();
        }
    }
}