using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Strategies.Common
{
    public abstract class RecursiveStrategyBase : TradingStrategyCommonBase
    {
        private readonly IOptions<RecursiveStrategyBaseOptions> m_options;

        protected RecursiveStrategyBase(IOptions<RecursiveStrategyBaseOptions> options, string symbol,
            TimeFrameWindow[] requiredTimeFrames, IWalletManager walletManager,
            ICbFuturesRestClient cbFuturesRestClient) : base(options, symbol, requiredTimeFrames, walletManager,
            cbFuturesRestClient)
        {
            m_options = options;
        }

        protected override Task<decimal?> CalculateMinBalanceAsync()
        {
            var minLong = CalculateMinBalanceLongAsync();
            var minShort = CalculateMinBalanceShortAsync();
            decimal? minBalance = null;
            if (minLong.Result.HasValue && minShort.Result.HasValue)
                minBalance = Math.Max(minLong.Result.Value, minShort.Result.Value);
            else if (minLong.Result.HasValue)
                minBalance = minLong.Result.Value;
            else if (minShort.Result.HasValue)
                minBalance = minShort.Result.Value;
            return Task.FromResult(minBalance);
        }

        protected Task<decimal?> CalculateMinBalanceLongAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.FromResult<decimal?>(null);
            if (!SymbolInfo.MinOrderQty.HasValue)
                return Task.FromResult<decimal?>(null);
            if (WalletExposureLong <= 0)
                return Task.FromResult<decimal?>(null);
            if (m_options.Value.InitialQtyPctLong <= 0)
                return Task.FromResult<decimal?>(null);

            var minBalance = (double)ticker.BestAskPrice
                             * (double)SymbolInfo.MinOrderQty.Value
                             / (m_options.Value.InitialQtyPctLong
                                * (double)WalletExposureLong);
            var minBalanceRounded = (decimal?)Math.Round(minBalance);
            return Task.FromResult(minBalanceRounded);
        }

        protected Task<decimal?> CalculateMinBalanceShortAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.FromResult<decimal?>(null);
            if (!SymbolInfo.MinOrderQty.HasValue)
                return Task.FromResult<decimal?>(null);
            if (WalletExposureShort <= 0)
                return Task.FromResult<decimal?>(null);
            if (m_options.Value.InitialQtyPctShort <= 0)
                return Task.FromResult<decimal?>(null);

            var minBalance = (double)ticker.BestAskPrice
                             * (double)SymbolInfo.MinOrderQty.Value
                             / (m_options.Value.InitialQtyPctShort
                                * (double)WalletExposureShort);
            var minBalanceRounded = (decimal?)Math.Round(minBalance);
            return Task.FromResult(minBalanceRounded);
        }

        protected override async Task CalculateDynamicQtyAsync()
        {
            DynamicQtyShort = null;
            DynamicQtyLong = null;

            var balance = WalletManager.Contract.WalletBalance;
            var existingLongPosition = LongPosition;
            var minLongBalance = await CalculateMinBalanceLongAsync();
            var shouldCalculateMaxQtyLong = existingLongPosition != null || balance >= minLongBalance;
            if (shouldCalculateMaxQtyLong)
            {
                var longPosition = await CalculateNextGridLongPositionAsync();
                if (longPosition.HasValue)
                    DynamicQtyLong = (decimal)longPosition.Value.Qty;
                MaxQtyLong = long.MaxValue; // this is handled by grid
            }

            var existingShortPosition = ShortPosition;
            var minShortBalance = await CalculateMinBalanceShortAsync();
            var shouldCalculateMaxQtyShort = existingShortPosition != null || balance >= minShortBalance;
            if (shouldCalculateMaxQtyShort)
            {
                var shortPosition = await CalculateNextGridShortPositionAsync();
                if (shortPosition.HasValue)
                    DynamicQtyShort = (decimal)shortPosition.Value.Qty;
                MaxQtyShort = long.MaxValue; // this is handled by grid
            }
        }

        protected virtual Task<ReentryMultiplier> CalculateReentryMultiplierLongAsync()
        {
            return Task.FromResult(new ReentryMultiplier(1.0, 1.0));
        }

        protected virtual Task<ReentryMultiplier> CalculateReentryMultiplierShortAsync()
        {
            return Task.FromResult(new ReentryMultiplier(1.0, 1.0));
        }

        protected async Task<GridPosition?> CalculateNextGridLongPositionAsync()
        {
            var longPosition = LongPosition;
            var balance = WalletManager.Contract.WalletBalance;
            if (!balance.HasValue)
                return null;
            var symbolInfo = SymbolInfo;
            if (!symbolInfo.QtyStep.HasValue)
                return null;
            if (!symbolInfo.MinOrderQty.HasValue)
                return null;
            var ticker = Ticker;
            if (ticker == null)
                return null;
            double positionSize = 0;
            double entryPrice = 0;
            if (longPosition != null)
            {
                positionSize = (double)longPosition.Quantity;
                entryPrice = (double)longPosition.AveragePrice;
            }

            bool inverse = false;
            double qtyStep = (double)symbolInfo.QtyStep.Value;
            if (qtyStep == 0)
                return null;
            var priceStep = 1 / Math.Pow(10, (int)symbolInfo.PriceScale);
            double minQty = (double)symbolInfo.MinOrderQty.Value;
            double minCost = 0.0;
            double cMultiplier = 1.0;
            double initialQtyPct = m_options.Value.InitialQtyPctLong;
            double ddownFactor = m_options.Value.DDownFactorLong;
            double walletExposureLimit = (double)m_options.Value.WalletExposureLong;
            var reentryMultiplier = await CalculateReentryMultiplierLongAsync();
            double reentryPositionPriceDistance = m_options.Value.ReentryPositionPriceDistanceLong;
            reentryPositionPriceDistance *= reentryMultiplier.DistanceMultiplier;
            double reentryPositionPriceDistanceWalletExposureWeighting = 
                m_options.Value.ReentryPositionPriceDistanceWalletExposureWeightingLong * reentryMultiplier.WeightMultiplier;
            var highestBid = (double)ticker.BestBidPrice;
            var longEntry = GridHelpers.CalcRecursiveEntryLong(
                (double)balance.Value,
                positionSize,
                entryPrice,
                highestBid,
                inverse,
                qtyStep,
                priceStep,
                minQty,
                minCost,
                cMultiplier,
                initialQtyPct,
                ddownFactor,
                reentryPositionPriceDistance,
                reentryPositionPriceDistanceWalletExposureWeighting,
                walletExposureLimit);

            return longEntry;
        }

        protected async Task<GridPosition?> CalculateNextGridShortPositionAsync()
        {
            var shortPosition = ShortPosition;
            var balance = WalletManager.Contract.WalletBalance;
            if (!balance.HasValue)
                return null;
            var symbolInfo = SymbolInfo;
            if (!symbolInfo.QtyStep.HasValue)
                return null;
            if (!symbolInfo.MinOrderQty.HasValue)
                return null;
            var ticker = Ticker;
            if (ticker == null)
                return null;
            double positionSize = 0;
            double entryPrice = 0;
            if (shortPosition != null)
            {
                positionSize = (double)shortPosition.Quantity;
                entryPrice = (double)shortPosition.AveragePrice;
            }

            bool inverse = false;
            double qtyStep = (double)symbolInfo.QtyStep.Value;
            if (qtyStep == 0)
                return null;
            var priceStep = 1 / Math.Pow(10, (int)symbolInfo.PriceScale);
            double minQty = (double)symbolInfo.MinOrderQty.Value;
            double minCost = 0.0;
            double cMultiplier = 1.0;
            double initialQtyPct = m_options.Value.InitialQtyPctShort;
            double ddownFactor = m_options.Value.DDownFactorShort;
            double walletExposureLimit = (double)m_options.Value.WalletExposureShort;
            var reentryMultiplier = await CalculateReentryMultiplierShortAsync();
            double reentryPositionPriceDistance = m_options.Value.ReentryPositionPriceDistanceShort;
            reentryPositionPriceDistance *= reentryMultiplier.DistanceMultiplier;
            double reentryPositionPriceDistanceWalletExposureWeighting =
                m_options.Value.ReentryPositionPriceDistanceWalletExposureWeightingShort * reentryMultiplier.WeightMultiplier;
            var lowestAsk = (double)ticker.BestAskPrice;
            var shortEntry = GridHelpers.CalcRecursiveEntryShort(
                (double)balance.Value,
                positionSize,
                entryPrice,
                lowestAsk,
                inverse,
                qtyStep,
                priceStep,
                minQty,
                minCost,
                cMultiplier,
                initialQtyPct,
                ddownFactor,
                reentryPositionPriceDistance,
                reentryPositionPriceDistanceWalletExposureWeighting,
                walletExposureLimit);

            return shortEntry;
        }
    }
}