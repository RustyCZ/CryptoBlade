namespace CryptoBlade.BackTesting
{
    public class ExternalBackTestIdProvider : IBackTestIdProvider
    {
        private readonly string m_backtestId;

        public ExternalBackTestIdProvider(string backtestId)
        {
            m_backtestId = backtestId;
        }

        public string GetTestId()
        {
            return m_backtestId;
        }
    }
}