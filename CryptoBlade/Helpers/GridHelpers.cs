namespace CryptoBlade.Helpers
{
    /// <summary>
    /// Helper methods taken from https://github.com/enarjord/passivbot
    /// </summary>
    public static class GridHelpers
    {
        public static double CalcMinEntryQty(double price, bool inverse, double qtyStep, double minQty, double minCost)
        {
            if (inverse)
            {
                return minQty;
            }
            else
            {
                double calculatedQty =
                    (price > 0.0f) ? Math.Ceiling(minCost / price / qtyStep) * qtyStep : 0.0f;
                return Math.Max(minQty, calculatedQty);
            }
        }

        public static double QtyToCost(double qty, double price, bool inverse, double cMultiplier)
        {
            double cost;
            if (price > 0.0f)
            {
                cost = (inverse ? Math.Abs(qty / price) : Math.Abs(qty * price)) * cMultiplier;
            }
            else
            {
                cost = 0.0f;
            }

            return cost;
        }

        public static (double, double) CalcNewPSizePPrice(double positionSize, double positionPrice, double qty,
            double price, double qtyStep)
        {
            if (qty == 0.0f)
            {
                return (positionSize, positionPrice);
            }

            double newPSize = Round(positionSize + qty, qtyStep);

            if (newPSize == 0.0f)
            {
                return (0.0f, 0.0f);
            }

            return (newPSize, NaNToZero(positionPrice) * (positionSize / newPSize) + price * (qty / newPSize));
        }

        public static double Round(double value, double step)
        {
            return Math.Round(value / step) * step;
        }

        public static double NaNToZero(double value)
        {
            return double.IsNaN(value) ? 0.0f : value;
        }

        public static double CalcWalletExposureIfFilled(double balance, double positionSize, double positionPrice,
            double qty,
            double price, bool inverse, double cMultiplier, double qtyStep)
        {
            positionSize = Round(Math.Abs(positionSize), qtyStep);
            qty = Round(Math.Abs(qty), qtyStep);

            (double newPSize, double newPPrice) = CalcNewPSizePPrice(positionSize, positionPrice, qty, price, qtyStep);

            return QtyToCost(newPSize, newPPrice, inverse, cMultiplier) / balance;
        }

        public static double CalcRecursiveReentryQty(
            double balance,
            double positionSize,
            double entryPrice,
            bool inverse,
            double qtyStep,
            double minQty,
            double minCost,
            double cMultiplier,
            double initialQtyPct,
            double ddownFactor,
            double walletExposureLimit)
        {
            double minEntryQty = CalcMinEntryQty(entryPrice, inverse, qtyStep, minQty, minCost);
            double costToQtyResult = CostToQty(balance, entryPrice, inverse, cMultiplier);

            double reentryQty = Math.Max(positionSize * ddownFactor,
                CustomRound(Math.Max(costToQtyResult * walletExposureLimit * initialQtyPct, minEntryQty), qtyStep));

            return reentryQty;
        }


        public static double CostToQty(double cost, double price, bool inverse, double cMultiplier)
        {
            if (inverse)
            {
                return (price > 0.0) ? (cost * price) / cMultiplier : 0.0;
            }
            else
            {
                return (price > 0.0) ? (cost / price) / cMultiplier : 0.0;
            }
        }

        public static double CustomRound(double value, double step)
        {
            if (step == 0.0)
            {
                return value;
            }
            else
            {
                return Math.Round(value / step) * step;
            }
        }

        public static double FindEntryQtyBringingWalletExposureToTarget(
            double balance,
            double positionSize,
            double positionPrice,
            double walletExposureTarget,
            double entryPrice,
            bool inverse,
            double qtyStep,
            double cMultiplier)
        {
            if (walletExposureTarget == 0.0)
            {
                return 0.0;
            }

            double walletExposure = QtyToCost(positionSize, positionPrice, inverse, cMultiplier) / balance;

            if (walletExposure >= walletExposureTarget * 0.99)
            {
                // Return zero if walletExposure is already within 1% of the target
                return 0.0;
            }

            List<double> guesses = new List<double>();
            List<double> values = new List<double>();
            List<double> evaluations = new List<double>();

            guesses.Add(Round(Math.Abs(positionSize) * walletExposureTarget / Math.Max(0.01, walletExposure), qtyStep));
            values.Add(CalcWalletExposureIfFilled(balance, positionSize, positionPrice, guesses.Last(), entryPrice,
                inverse, cMultiplier,
                qtyStep));
            evaluations.Add(Math.Abs(values.Last() - walletExposureTarget) / walletExposureTarget);

            guesses.Add(Math.Max(0.0, Round(Math.Max(guesses.Last() * 1.2, guesses.Last() + qtyStep), qtyStep)));
            values.Add(CalcWalletExposureIfFilled(balance, positionSize, positionPrice, guesses.Last(), entryPrice,
                inverse, cMultiplier,
                qtyStep));
            evaluations.Add(Math.Abs(values.Last() - walletExposureTarget) / walletExposureTarget);

            for (int i = 0; i < 15; i++)
            {
                // ReSharper disable CompareOfFloatsByEqualityOperator
                if (guesses.Last() == guesses[^2])
                    // ReSharper restore CompareOfFloatsByEqualityOperator
                {
                    guesses[^1] =
                        Math.Abs(Round(Math.Max(guesses[^2] * 1.1, guesses[^2] + qtyStep),
                            qtyStep));
                    values[^1] = CalcWalletExposureIfFilled(balance, positionSize, positionPrice,
                        guesses[^1], entryPrice, inverse, cMultiplier, qtyStep);
                }

                guesses.Add(Math.Max(0.0,
                    Round(
                        Interpolate(walletExposureTarget, values.GetRange(values.Count - 2, 2).ToArray(),
                            guesses.GetRange(guesses.Count - 2, 2).ToArray()), qtyStep)));
                values.Add(CalcWalletExposureIfFilled(balance, positionSize, positionPrice, guesses.Last(), entryPrice,
                    inverse, cMultiplier,
                    qtyStep));
                evaluations.Add(Math.Abs(values.Last() - walletExposureTarget) / walletExposureTarget);

                if (evaluations.Last() < 0.01)
                {
                    // Close enough
                    break;
                }
            }

            List<(double, double)> evaluationGuesses = evaluations.Zip(guesses, (e, g) => (e, g)).ToList();
            evaluationGuesses.Sort();

            return evaluationGuesses[0].Item2;
        }

        public static double Interpolate(double target, double[] values, double[] guesses)
        {
            return guesses[0] + (target - values[0]) * (guesses[1] - guesses[0]) / (values[1] - values[0]);
        }

        public static GridPosition CalcRecursiveEntryLong(
            double balance,
            double positionSize,
            double positionPrice,
            double highestBid,
            bool inverse,
            double qtyStep,
            double priceStep,
            double minQty,
            double minCost,
            double cMultiplier,
            double initialQtyPct,
            double ddownFactor,
            double reentryPositionPriceDistance,
            double reentryPositionPriceDistanceWalletExposureWeighting,
            double walletExposureLimit)
        {
            if (walletExposureLimit == 0.0)
                return new GridPosition(0.0, 0.0);

            double initialEntryPrice = Math.Max(
                priceStep,
                highestBid);

            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (initialEntryPrice == priceStep) 
            // ReSharper restore CompareOfFloatsByEqualityOperator
            {
                return new GridPosition(0.0, initialEntryPrice);
            }

            double minEntryQty = CalcMinEntryQty(initialEntryPrice, inverse, qtyStep, minQty, minCost);
            double initialEntryQty = Math.Max(
                minEntryQty,
                Round(
                    CostToQty(balance, initialEntryPrice, inverse, cMultiplier)
                    * walletExposureLimit
                    * initialQtyPct,
                    qtyStep
                )
            );

            if (positionSize == 0.0)
            {
                // Normal initial entry
                return new GridPosition(initialEntryQty, initialEntryPrice);
            }
            else if (positionSize < initialEntryQty * 0.8)
            {
                // Partial initial entry
                double entryQty = Math.Max(minEntryQty, Round(initialEntryQty - positionSize, qtyStep));
                return new GridPosition(entryQty, initialEntryPrice);
            }
            else
            {
                double walletExposure = QtyToCost(positionSize, positionPrice, inverse, cMultiplier) / balance;

                if (walletExposure >= walletExposureLimit * 0.999)
                {
                    // No entry if walletExposure is within 0.1% of the limit
                    return new GridPosition(0.0, 0.0);
                }

                // Normal reentry
                double multiplier = (walletExposure / walletExposureLimit) * reentryPositionPriceDistanceWalletExposureWeighting;
                double entryPrice = RoundDn(positionPrice * (1 - reentryPositionPriceDistance * (1 + multiplier)), priceStep);

                if (entryPrice <= priceStep)
                {
                    return new GridPosition(0.0, priceStep);
                }

                entryPrice = Math.Min(highestBid, entryPrice);
                double entryQty = CalcRecursiveReentryQty(
                    balance,
                    positionSize,
                    entryPrice,
                    inverse,
                    qtyStep,
                    minQty,
                    minCost,
                    cMultiplier,
                    initialQtyPct,
                    ddownFactor,
                    walletExposureLimit
                );

                double walletExposureIfFilled = CalcWalletExposureIfFilled(
                    balance,
                    positionSize,
                    positionPrice,
                    entryQty,
                    entryPrice,
                    inverse,
                    cMultiplier,
                    qtyStep
                );

                bool adjust = false;

                if (walletExposureIfFilled > walletExposureLimit * 1.01)
                {
                    // Reentry too big
                    adjust = true;
                }
                else
                {
                    // Preview next reentry
                    (double newPSize, double newPPrice) =
                        CalcNewPSizePPrice(positionSize, positionPrice, entryQty, entryPrice, qtyStep);
                    double newWalletExposure = QtyToCost(newPSize, newPPrice, inverse, cMultiplier) / balance;
                    double newMultiplier = (newWalletExposure / walletExposureLimit) *
                                           reentryPositionPriceDistanceWalletExposureWeighting;
                    double newEntryPrice = RoundDn(newPPrice * (1 - reentryPositionPriceDistance * (1 + newMultiplier)), priceStep);
                    double newEntryQty = CalcRecursiveReentryQty(
                        balance,
                        newPSize,
                        newEntryPrice,
                        inverse,
                        qtyStep,
                        minQty,
                        minCost,
                        cMultiplier,
                        initialQtyPct,
                        ddownFactor,
                        walletExposureLimit
                    );
                    double walletExposureIfNextFilled = CalcWalletExposureIfFilled(
                        balance,
                        newPSize,
                        newPPrice,
                        newEntryQty,
                        newEntryPrice,
                        inverse,
                        cMultiplier,
                        qtyStep
                    );

                    if (walletExposureIfNextFilled > walletExposureLimit * 1.2)
                    {
                        // Reentry too small
                        adjust = true;
                    }
                }

                if (adjust)
                {
                    // Increase qty if next reentry is too big
                    // Decrease qty if current reentry is too big
                    entryQty = FindEntryQtyBringingWalletExposureToTarget(
                        balance,
                        positionSize,
                        positionPrice,
                        walletExposureLimit,
                        entryPrice,
                        inverse,
                        qtyStep,
                        cMultiplier
                    );
                    entryQty = Math.Max(
                        entryQty,
                        CalcMinEntryQty(entryPrice, inverse, qtyStep, minQty, minCost)
                    );
                }

                return new GridPosition(entryQty, entryPrice);
            }
        }

        public static double RoundDn(double value, double step)
        {
            return Math.Floor(value / step) * step;
        }
    }
}