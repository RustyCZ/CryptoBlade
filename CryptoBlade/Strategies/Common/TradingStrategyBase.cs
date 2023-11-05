using CryptoBlade.Exchanges;
using CryptoBlade.Helpers;
using CryptoBlade.Models;
using CryptoBlade.Strategies.Wallet;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Strategies.Common
{
    public abstract class TradingStrategyBase : TradingStrategyCommonBase
    {
        private readonly IOptions<TradingStrategyBaseOptions> m_options;

        protected TradingStrategyBase(IOptions<TradingStrategyBaseOptions> options,
            string symbol, 
            TimeFrameWindow[] requiredTimeFrames, 
            IWalletManager walletManager,
            ICbFuturesRestClient cbFuturesRestClient) 
            : base(options, symbol, requiredTimeFrames, walletManager, cbFuturesRestClient)
        {
            m_options = options;
        }

        protected abstract int DcaOrdersCount { get; }
        protected abstract bool ForceMinQty { get; }

        protected override Task<decimal?> CalculateMinBalanceAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.FromResult<decimal?>(null);
            var minExposure = Math.Min(WalletExposureLong, WalletExposureShort);
            if (minExposure == 0)
                minExposure = Math.Max(WalletExposureLong, WalletExposureShort);

            var recommendedMinBalance = SymbolInfo.CalculateMinBalance(ticker.BestAskPrice, minExposure, DcaOrdersCount);
            return Task.FromResult(recommendedMinBalance);
        }

        protected override async Task CalculateDynamicQtyAsync()
        {
            if (!m_options.Value.EnableRecursiveQtyFactorLong)
                await CalculateDynamicQtyLongFixedAsync();
            else
                await CalculateDynamicQtyLongFactorAsync();
            
            if(!m_options.Value.EnableRecursiveQtyFactorShort)
                await CalculateDynamicQtyShortFixedAsync();
            else
                await CalculateDynamicQtyShortFactorAsync();
            var dynamicQtyShort = DynamicQtyShort;
            var dynamicQtyLong = DynamicQtyLong;
            MaxQtyShort = null;
            MaxQtyLong = null;
            if (dynamicQtyShort.HasValue)
                MaxQtyShort = DcaOrdersCount * dynamicQtyShort.Value;
            if (dynamicQtyLong.HasValue)
                MaxQtyLong = DcaOrdersCount * dynamicQtyLong.Value;
        }

        protected virtual Task CalculateDynamicQtyShortFixedAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.CompletedTask;

            if (!DynamicQtyShort.HasValue || !IsInTrade)
                DynamicQtyShort = CalculateDynamicQty(ticker.BestAskPrice, WalletExposureShort);

            return Task.CompletedTask;
        }

        protected virtual Task CalculateDynamicQtyLongFixedAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.CompletedTask;

            if (!DynamicQtyLong.HasValue || !IsInTrade)
                DynamicQtyLong = CalculateDynamicQty(ticker.BestBidPrice, WalletExposureLong);

            return Task.CompletedTask;
        }

        protected virtual Task CalculateDynamicQtyLongFactorAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.CompletedTask; 
            var longPosition = LongPosition; 
                
            if (longPosition == null)
                DynamicQtyLong = CalculateDynamicQty(ticker.BestBidPrice, WalletExposureLong);
            else
            {
                var walletBalance = WalletManager.Contract.WalletBalance;
                var positionValue = longPosition.Quantity * longPosition.AveragePrice;
                var remainingExposure = (m_options.Value.WalletExposureLong * walletBalance) - positionValue;
                if (remainingExposure <= 0)
                    DynamicQtyLong = null;
                else
                {
                    var remainingQty = remainingExposure / ticker.BestBidPrice;
                    DynamicQtyLong = longPosition.Quantity * m_options.Value.QtyFactorLong;
                    if (DynamicQtyLong > remainingQty)
                        DynamicQtyLong = remainingQty;
                    var symbolInfo = SymbolInfo;
                    if (symbolInfo.QtyStep.HasValue)
                        DynamicQtyLong -= (DynamicQtyLong % symbolInfo.QtyStep.Value);
                    if (SymbolInfo.MinOrderQty > DynamicQtyLong)
                        DynamicQtyLong = SymbolInfo.MinOrderQty;
                }
            }
            
            return Task.CompletedTask;
        }

        protected virtual Task CalculateDynamicQtyShortFactorAsync()
        {
            var ticker = Ticker;
            if (ticker == null)
                return Task.CompletedTask;
            var shortPosition = ShortPosition;
            if (shortPosition == null)
                DynamicQtyShort = CalculateDynamicQty(ticker.BestAskPrice, WalletExposureShort);
            else
            {
                var walletBalance = WalletManager.Contract.WalletBalance;
                var positionValue = shortPosition.Quantity * shortPosition.AveragePrice;
                var remainingExposure = (m_options.Value.WalletExposureShort * walletBalance) - positionValue;
                if(remainingExposure <= 0)
                    DynamicQtyShort = null;
                else
                {
                    var remainingQty = remainingExposure / ticker.BestAskPrice;
                    DynamicQtyShort = shortPosition.Quantity * m_options.Value.QtyFactorShort;
                    if (DynamicQtyShort > remainingQty)
                        DynamicQtyShort = remainingQty;
                    var symbolInfo = SymbolInfo;
                    if(symbolInfo.QtyStep.HasValue)
                        DynamicQtyShort -= (DynamicQtyShort % symbolInfo.QtyStep.Value);
                    if (SymbolInfo.MinOrderQty > DynamicQtyShort)
                        DynamicQtyShort = SymbolInfo.MinOrderQty;
                }
            }

            return Task.CompletedTask;
        }

        protected override Task CalculateTakeProfitAsync(IList<StrategyIndicator> indicators)
        {
            var ticker = Ticker;
            if(ticker == null)
                return Task.CompletedTask;
            var quotes5Min = QuoteQueues[TimeFrame.FiveMinutes].GetQuotes();
            if (quotes5Min.Length < 1)
                return Task.CompletedTask;
            var quotes1Min = QuoteQueues[TimeFrame.OneMinute].GetQuotes();
            if (quotes1Min.Length < 1)
                return Task.CompletedTask;
            var spread5Min = TradeSignalHelpers.Get5MinSpread(quotes1Min);
            var longPosition = LongPosition;
            var shortPosition = ShortPosition;
            decimal? shortTakeProfit = null;
            if (shortPosition != null)
                shortTakeProfit = TradingHelpers.CalculateShortTakeProfit(
                    shortPosition,
                    SymbolInfo,
                    quotes5Min,
                    spread5Min,
                    ticker,
                    m_options.Value.FeeRate,
                    m_options.Value.MinProfitRate);
            ShortTakeProfitPrice = shortTakeProfit;
            if (shortTakeProfit.HasValue)
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.ShortTakeProfit), shortTakeProfit.Value));

            decimal? longTakeProfit = null;
            if (longPosition != null)
                longTakeProfit = TradingHelpers.CalculateLongTakeProfit(
                    longPosition,
                    SymbolInfo,
                    quotes5Min,
                    spread5Min,
                    ticker,
                    m_options.Value.FeeRate,
                    m_options.Value.MinProfitRate);
            LongTakeProfitPrice = longTakeProfit;
            if (longTakeProfit.HasValue)
                indicators.Add(new StrategyIndicator(nameof(IndicatorType.LongTakeProfit), longTakeProfit.Value));

            return Task.CompletedTask;
        }

        private decimal? CalculateDynamicQty(decimal price, decimal walletExposure)
        {
            var dynamicQty = SymbolInfo.CalculateQuantity(WalletManager, price, walletExposure, DcaOrdersCount);
            if (!dynamicQty.HasValue && ForceMinQty) // we could not calculate a quantity so we will use the minimum
                dynamicQty = SymbolInfo.MinOrderQty;

            if (dynamicQty.HasValue && dynamicQty.Value < SymbolInfo.MinOrderQty)
                dynamicQty = ForceMinQty ? SymbolInfo.MinOrderQty : null;

            bool isInTrade = IsInTrade;
            if (!dynamicQty.HasValue && isInTrade)
            {
                // we are in a trade and we could not calculate a quantity so we will use the minimum
                dynamicQty = SymbolInfo.MinOrderQty;
            }

            return dynamicQty;
        }
    }
}