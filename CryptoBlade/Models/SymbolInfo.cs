namespace CryptoBlade.Models
{
    public readonly record struct SymbolInfo(string Name, decimal PriceScale, string QuoteAsset, string BaseAsset, decimal? MinOrderQty, decimal? QtyStep, decimal? MaxLeverage);
}
