using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoBlade.Helpers;

namespace CryptoBlade.Tests.Helpers
{
    public class GridHelpersTest
    {
        [Fact]
        public void CalcRecursiveReentryQtyTest()
        {
            double balance = 1000;
            double positionSize = 0;
            double entryPrice = 0.07222;
            bool inverse = false;
            double qtyStep = 1.0;
            double minQty = 1.0;
            double minCost = 5.0;
            double cMultiplier = 1.0;
            double initialQtyPct = 0.01;
            double ddownFactor = 2.1428512029160007;
            double walletExposureLimit = 1.0;
            var reentryQty = GridHelpers.CalcRecursiveReentryQty(balance, positionSize, entryPrice, inverse, qtyStep, minQty, minCost,
                cMultiplier, initialQtyPct, ddownFactor, walletExposureLimit);
            Assert.Equal(138.0, reentryQty);
        }

        [Fact]
        public void CalcRecursiveEntryLong()
        {
            double balance = 1000;
            double positionSize = 0;
            double entryPrice = 0.07222;
            bool inverse = false;
            double qtyStep = 1.0;
            double priceStep = 0.00001;
            double minQty = 1.0;
            double minCost = 5.0;
            double cMultiplier = 1.0;
            double initialQtyPct = 0.01;
            double ddownFactor = 2.1428512029160007;
            double walletExposureLimit = 1.0;
            double reentryPositionPriceDistance = 0.018514600084018253;
            double reentryPositionPriceDistanceWalletExposureWeighting = 2.119869195156612;
            var longEntry = GridHelpers.CalcRecursiveEntryLong(
                balance, 
                positionSize,
                entryPrice,
                entryPrice,
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
            Assert.Equal(0.07222, longEntry.Price);
            Assert.Equal(138.0, longEntry.Qty);
        }
    }
}