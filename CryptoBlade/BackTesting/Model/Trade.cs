using System.Runtime.InteropServices;

namespace CryptoBlade.BackTesting.Model
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Trade
    {
        public decimal Timestamp;
        public decimal Size;
        public decimal Price;

        public DateTime TimestampDateTime
        {
            get => DateTime.UnixEpoch.AddSeconds((double)Timestamp);
            set => Timestamp = (decimal)(value - DateTime.UnixEpoch).TotalSeconds;
        }
    }
}
