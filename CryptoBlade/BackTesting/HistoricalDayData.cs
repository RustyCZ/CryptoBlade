using CryptoBlade.BackTesting.Model;
using CryptoBlade.Models;

namespace CryptoBlade.BackTesting
{
    public class HistoricalDayData
    { 
        public DateTime Day { get; set; }
        public Trade[] Trades { get; set; } = Array.Empty<Trade>();
        public Candle[] Candles { get; set; } = Array.Empty<Candle>();
    }
}