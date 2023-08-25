namespace CryptoBlade.Strategies.Common
{
    public record struct ExecuteParams(bool AllowLongOpen, bool AllowShortOpen, bool AllowExtraLong, bool AllowExtraShort, bool LongUnstucking, bool ShortUnstucking);
}