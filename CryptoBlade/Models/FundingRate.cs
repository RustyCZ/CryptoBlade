using ProtoBuf;

namespace CryptoBlade.Models
{
    [ProtoContract]
    public class FundingRate
    {
        [ProtoMember(1)]
        public DateTime Time { get; set; }
        
        [ProtoMember(2)]
        public decimal Rate { get; set; }
    }
}