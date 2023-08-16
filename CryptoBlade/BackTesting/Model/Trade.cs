using System.Runtime.InteropServices;
using ProtoBuf;

namespace CryptoBlade.BackTesting.Model
{
    [StructLayout(LayoutKind.Sequential)]
    [ProtoContract]
    public struct Trade
    {
        [ProtoMember(1)]
        public long Timestamp;
        [ProtoMember(2)]
        public decimal Size;
        [ProtoMember(3)]
        public decimal Price;

        public DateTime TimestampDateTime
        {
            get => new DateTime(Timestamp, DateTimeKind.Utc);
            set => Timestamp = value.Ticks;
        }
    }
}
