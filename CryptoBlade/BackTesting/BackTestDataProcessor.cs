using CryptoBlade.Models;

namespace CryptoBlade.BackTesting
{
    public class BackTestDataProcessor
    {
        private readonly Dictionary<TimeFrame, Queue<Candle>> m_candles;
        private readonly Queue<FundingRate> m_fundingRates;
        private FundingRate? m_lastFundingRate;
        private static readonly TimeFrame[] s_dailyTimeFrames = {
            TimeFrame.OneMinute,
            TimeFrame.FiveMinutes,
            TimeFrame.FifteenMinutes,
            TimeFrame.ThirtyMinutes,
            TimeFrame.OneHour,
            TimeFrame.FourHours,
            TimeFrame.OneDay,
        };

        public BackTestDataProcessor(HistoricalDayData dayData)
        {
            m_candles = new Dictionary<TimeFrame, Queue<Candle>>();
            var candlesPerTimeFrame = dayData.Candles.GroupBy(x => x.TimeFrame);
            foreach (var candles in candlesPerTimeFrame)
            {
                var orderedCandles = candles.OrderBy(x => x.StartTime);
                m_candles.Add(candles.Key, new Queue<Candle>(orderedCandles));
            }
            var fundingRates = dayData.FundingRates.OrderBy(x => x.Time);
            m_fundingRates = new Queue<FundingRate>(fundingRates);
        }

        public TimeData AdvanceTime(DateTime currentTime)
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
            
            FundingRate? fundingRate = null;
            if (m_fundingRates.TryPeek(out var nextFundingRate) && nextFundingRate.Time == currentTime)
                fundingRate = m_fundingRates.Dequeue();
            var lastFundingRate = fundingRate ?? m_lastFundingRate;
            if(fundingRate != null)
                m_lastFundingRate = fundingRate;
            return new TimeData(currentTime, candles.ToArray(), fundingRate, lastFundingRate);
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