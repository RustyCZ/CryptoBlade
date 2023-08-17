namespace CryptoBlade.Strategies.Common
{
    public record struct ExecuteParams(bool AllowLongOpen, bool AllowShortOpen, bool LongUnstucking, bool ShortUnstucking);
}