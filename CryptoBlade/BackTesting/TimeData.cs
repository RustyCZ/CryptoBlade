using CryptoBlade.Models;

namespace CryptoBlade.BackTesting
{
    public readonly record struct TimeData(DateTime CurrentTime, Candle[] Candles, FundingRate? CurrentFundingRate, FundingRate? LastFundingRate);
}