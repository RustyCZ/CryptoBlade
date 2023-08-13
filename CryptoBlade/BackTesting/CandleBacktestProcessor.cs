using CryptoBlade.Models;

namespace CryptoBlade.BackTesting
{
    public class CandleBacktestProcessor
    {
        private readonly Dictionary<TimeFrame, Queue<Candle>> m_candles;
        private static readonly TimeFrame[] s_dailyTimeFrames = new[]
        {
            TimeFrame.OneMinute,
            TimeFrame.FiveMinutes,
            TimeFrame.FifteenMinutes,
            TimeFrame.ThirtyMinutes,
            TimeFrame.OneHour,
            TimeFrame.FourHours,
            TimeFrame.OneDay,
        };

        public CandleBacktestProcessor(HistoricalDayData dayData)
        {
            m_candles = new Dictionary<TimeFrame, Queue<Candle>>();
            var candlesPerTimeFrame = dayData.Candles.GroupBy(x => x.TimeFrame);
            foreach (var candles in candlesPerTimeFrame)
            {
                var orderedCandles = candles.OrderBy(x => x.StartTime);
                m_candles.Add(candles.Key, new Queue<Candle>(orderedCandles));
            }
        }

        public Candle[] MoveNext(DateTime currentTime)
        {
            List<Candle> candles = new List<Candle>();
            foreach (var timeFrame in s_dailyTimeFrames)
            {
                if(!m_candles.TryGetValue(timeFrame, out var candleQueue))
                    continue;
                DequeueOldCandles(candleQueue, currentTime);
                var currentCandle = TryDequeueCurrentCandle(candleQueue, currentTime);
                if(currentCandle != null)
                    candles.Add(currentCandle);
            }
            return candles.ToArray();
        }

        private static void DequeueOldCandles(Queue<Candle> queue, DateTime currentTime)
        {
            while (queue.TryPeek(out var nextCandle) && nextCandle.StartTime < currentTime)
                queue.Dequeue();
        }

        private static Candle? TryDequeueCurrentCandle(Queue<Candle> queue, DateTime currentTime)
        {
            if (!queue.TryPeek(out var nextCandle))
                return null;
            if (nextCandle.StartTime > currentTime)
                return null;
            return queue.Dequeue();
        }
    }
}