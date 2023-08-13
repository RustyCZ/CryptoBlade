using CryptoBlade.Helpers;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CryptoBlade.Tests
{
    public class TestBase
    {
        public TestBase(ITestOutputHelper testOutputHelper)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (ApplicationLogging.LoggerFactory == null)
            // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            {
                ApplicationLogging.LoggerFactory = LoggerFactory
                    .Create(builder =>
                    {
                        builder.SetMinimumLevel(LogLevel.Information);
                        builder.AddXunit(testOutputHelper);
                        builder.AddSimpleConsole(o =>
                        {
                            o.UseUtcTimestamp = true;
                            o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                        });
                    });
            }
        }
    }
}