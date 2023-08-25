namespace CryptoBlade.Configuration
{
    public class CriticalMode
    {
        public bool EnableCriticalModeLong { get; set; }

        public bool EnableCriticalModeShort { get; set; }

        public decimal WalletExposureThresholdLong { get; set; } = 0.3m;

        public decimal WalletExposureThresholdShort { get; set; } = 0.3m;
    }
}