using CryptoBlade.BackTesting.Model;
using CryptoBlade.Models;
using ProtoBuf;

namespace CryptoBlade.BackTesting
{
    [ProtoContract]
    public class HistoricalDayData
    {
        [ProtoMember(1)]
        public int Version { get; set; }
        
        [ProtoMember(2)]
        public DateTime Day { get; set; }
        
        [ProtoMember(3)]
        public Trade[] Trades { get; set; } = Array.Empty<Trade>();
        
        [ProtoMember(4)]
        public Candle[] Candles { get; set; } = Array.Empty<Candle>();
        
        [ProtoMember(5)]
        public FundingRate[] FundingRates { get; set; } = Array.Empty<FundingRate>();
    }
}