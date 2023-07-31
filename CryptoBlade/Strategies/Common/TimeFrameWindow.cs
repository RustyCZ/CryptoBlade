using CryptoBlade.Models;

namespace CryptoBlade.Strategies.Common
{
    public readonly record struct TimeFrameWindow(TimeFrame TimeFrame, int WindowSize, bool Primary);
}