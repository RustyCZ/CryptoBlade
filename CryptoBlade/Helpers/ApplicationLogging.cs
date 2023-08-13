namespace CryptoBlade.Helpers
{
    /// <summary>
    /// Shared logger
    /// </summary>
    public static class ApplicationLogging
    {
        public static ILoggerFactory LoggerFactory { get; set; } = null!;
        public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
        public static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
    }
}
