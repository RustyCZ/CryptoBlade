using CryptoBlade.Mapping;
using Skender.Stock.Indicators;

namespace CryptoBlade.Models
{
    public class QuoteQueue
    {
        private readonly Queue<Quote> m_queue;
        private readonly int m_maxSize;
        private readonly object m_lock = new();
        private readonly TimeFrame m_timeFrame;
        private Quote? m_lastQuote;

        public QuoteQueue(int maxSize, TimeFrame timeFrame)
        {
            m_maxSize = maxSize;
            m_timeFrame = timeFrame;
            m_queue = new Queue<Quote>();
        }

        public bool Enqueue(Quote candle)
        {
            lock (m_lock)
            {
                bool consistent = true;
                if (m_lastQuote != null)
                {
                    if (m_lastQuote.Date.Equals(candle.Date))
                        return true; // do not add duplicate
                    if (m_lastQuote.Date > candle.Date)
                        return true; // do not add out of order
                    var timeSpan = candle.Date - m_lastQuote.Date;
                    var tfTimespan = m_timeFrame.ToTimeSpan();
                    if (!timeSpan.Equals(tfTimespan))
                        consistent = false;
                }
                m_queue.Enqueue(candle);
                m_lastQuote = candle;

                if (m_queue.Count > m_maxSize)
                {
                    m_queue.Dequeue();
                }

                return consistent;
            }
        }

        public Quote[] GetQuotes()
        {
            lock (m_lock)
            {
                return m_queue.ToArray();
            }
        }

        public void Clear()
        {
            lock (m_lock)
            {
                m_lastQuote = null;
                m_queue.Clear();
            }
        }
    }
}
