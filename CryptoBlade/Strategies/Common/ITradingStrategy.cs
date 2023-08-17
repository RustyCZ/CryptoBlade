using CryptoBlade.Models;

namespace CryptoBlade.Strategies.Common
{
    public interface ITradingStrategy
    {
        string Name { get; }

        bool IsInTrade { get; }
        
        bool IsInLongTrade { get; }
        
        bool IsInShortTrade { get; }

        string Symbol { get; }

        SymbolInfo SymbolInfo { get; }

        decimal? DynamicQtyShort { get; }

        decimal? DynamicQtyLong { get; }

        decimal? RecommendedMinBalance { get; }

        bool HasSellSignal { get; }

        bool HasBuySignal { get; }

        bool HasSellExtraSignal { get; }
        
        bool HasBuyExtraSignal { get; }

        bool ConsistentData { get; }

        decimal? UnrealizedLongPnlPercent { get; }

        decimal? UnrealizedShortPnlPercent { get; }

        Ticker? Ticker { get; }

        DateTime LastTickerUpdate { get; }

        DateTime LastCandleUpdate { get; }

        StrategyIndicator[] Indicators { get; }

        TimeFrameWindow[] RequiredTimeFrameWindows { get; }

        Task UpdateTradingStateAsync(Position? longPosition, Position? shortPosition, Order[] openOrders, CancellationToken cancel);

        Task SetupSymbolAsync(SymbolInfo symbol, CancellationToken cancel);

        Task InitializeAsync(Candle[] candles, Ticker ticker, CancellationToken cancel);

        Task ExecuteAsync(ExecuteParams executeParams, CancellationToken cancel);

        Task ExecuteUnstuckAsync(bool unstuckLong, bool unstuckShort, bool forceUnstuckLong, bool forceUnstuckShort, bool forceKill, CancellationToken cancel);

        Task AddCandleDataAsync(Candle candle, CancellationToken cancel);

        Task UpdatePriceDataSync(Ticker ticker, CancellationToken cancel);
    }
}