namespace CryptoBlade.Models
{
    public class Candle
    {
        public TimeFrame TimeFrame { get; set; }
        public DateTime StartTime { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }

        public override string ToString()
        {
            return $"{TimeFrame} {StartTime} O:{Open} H:{High} L:{Low} C:{Close} V:{Volume}";
        }
    }
}