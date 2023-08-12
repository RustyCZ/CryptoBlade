using System.Runtime.InteropServices;

namespace CryptoBlade.BackTesting.Model
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Trade
    {
        public long Timestamp;
        public decimal Size;
        public decimal Price;

        public DateTime TimestampDateTime
        {
            get => new DateTime(Timestamp, DateTimeKind.Utc);
            set => Timestamp = value.Ticks;
        }
    }
}
