namespace CryptoBlade.BackTesting
{
    public class BackTestDataDownloaderOptions
    {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }

        public string[] Symbols { get; set; } = Array.Empty<string>();
    }
}