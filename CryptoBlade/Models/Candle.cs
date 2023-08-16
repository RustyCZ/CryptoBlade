using ProtoBuf;

namespace CryptoBlade.Models
{
    [ProtoContract]
    public class Candle
    {
        [ProtoMember(1)]
        public TimeFrame TimeFrame { get; set; }
        [ProtoMember(2)]
        public DateTime StartTime { get; set; }
        [ProtoMember(3)]
        public decimal Open { get; set; }
        [ProtoMember(4)]
        public decimal High { get; set; }
        [ProtoMember(5)]
        public decimal Low { get; set; }
        [ProtoMember(6)]
        public decimal Close { get; set; }
        [ProtoMember(7)]
        public decimal Volume { get; set; }

        public override string ToString()
        {
            return $"{TimeFrame} {StartTime} O:{Open} H:{High} L:{Low} C:{Close} V:{Volume}";
        }
    }
}