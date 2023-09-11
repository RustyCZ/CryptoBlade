using CryptoBlade.Configuration;

namespace CryptoBlade.BackTesting
{
    public class BackTestPerformanceTrackerOptions
    {
        public string BackTestsDirectory { get; set; } = ConfigConstants.BackTestsDirectory;
    }
}