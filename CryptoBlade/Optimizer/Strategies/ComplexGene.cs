namespace CryptoBlade.Optimizer.Strategies
{
    public readonly struct ComplexGene
    {
        public ComplexGeneType GeneType { get; private init; }

        public bool MinBoolValue { get; private init; }

        public bool MaxBoolValue { get; private init; }

        public int MinIntValue { get; private init; }

        public int MaxIntValue { get; private init; }

        public float MinFloatValue { get; private init; }

        public float MaxFloatValue { get; private init; }

        public int FractionDigits { get; private init; }

        public ComplexValueRepresentation ValueRepresentation
        {
            get
            {
                switch (GeneType)
                {
                    case ComplexGeneType.Bool:
                        return new ComplexValueRepresentation(MinBoolValue ? 1 : 0, MaxBoolValue ? 1 : 0, 1, 0);
                    case ComplexGeneType.Int:
                        int minIntValue = MinIntValue;
                        int maxIntValue = MaxIntValue;
                        if (minIntValue < 0)
                        {
                            maxIntValue += Math.Abs(minIntValue);
                            minIntValue = 0;
                        }
                        int totalBits = (int)Math.Ceiling(Math.Log(maxIntValue + 1, 2));
                        if (totalBits == 0)
                            totalBits = 1;
                        if (totalBits > 64)
                            totalBits = 64;
                        return new ComplexValueRepresentation(minIntValue, maxIntValue, totalBits, 0);
                    case ComplexGeneType.Float:
                        float minFloatValue = MinFloatValue;
                        float maxFloatValue = MaxFloatValue;
                        if (minFloatValue < 0)
                        {
                            maxFloatValue += Math.Abs(minFloatValue);
                            minFloatValue = 0;
                        }
                        var maxLongValue = Convert.ToInt64(maxFloatValue * Math.Pow(10, FractionDigits));
                        var totalBits2 = (int)Math.Ceiling(Math.Log(maxLongValue + 1, 2));
                        if (totalBits2 == 0)
                            totalBits2 = 1;
                        if (totalBits2 > 64)
                            totalBits2 = 64;
                        return new ComplexValueRepresentation(minFloatValue, maxFloatValue, totalBits2, FractionDigits);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public override string ToString()
        {
            switch (GeneType)
            {
                case ComplexGeneType.Bool:
                    return $"({MinBoolValue} {MaxBoolValue})";
                case ComplexGeneType.Int:
                    return $"({MinIntValue} {MaxIntValue})";
                case ComplexGeneType.Float:
                    return $"({MinFloatValue} {MaxFloatValue}) : {FractionDigits})";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ComplexGene FromValue(bool minValue, bool maxValue)
        {
            return new ComplexGene
            {
                GeneType = ComplexGeneType.Bool,
                MinBoolValue = minValue,
                MaxBoolValue = maxValue
            };
        }

        public static ComplexGene FromValue(int minValue, int maxValue)
        {
            return new ComplexGene
            {
                GeneType = ComplexGeneType.Int,
                MinIntValue = minValue,
                MaxIntValue = maxValue
            };
        }

        public static ComplexGene FromValue(float minValue, float maxValue, int fractionDigits)
        {
            return new ComplexGene
            {
                GeneType = ComplexGeneType.Float,
                MinFloatValue = minValue,
                MaxFloatValue = maxValue,
                FractionDigits = fractionDigits,
            };
        }
    }
}