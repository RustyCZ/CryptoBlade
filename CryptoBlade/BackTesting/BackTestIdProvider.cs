namespace CryptoBlade.BackTesting
{
    public class BackTestIdProvider : IBackTestIdProvider
    {
        public string GetTestId()
        {
            return $"{DateTime.Now:yyyyMMddHHmm}-{Guid.NewGuid():N}";
        }
    }
}