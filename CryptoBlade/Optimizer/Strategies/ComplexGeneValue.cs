using System.Globalization;

namespace CryptoBlade.Optimizer.Strategies
{
    public readonly struct ComplexGeneValue
    {
        public ComplexGeneType GeneType { get; private init; }

        public int IntValue { get; private init; }

        public float FloatValue { get; private init; }

        public bool BoolValue { get; private init; }

        public override string ToString()
        {
            switch (GeneType)
            {
                case ComplexGeneType.Bool:
                    return BoolValue.ToString();
                case ComplexGeneType.Int:
                    return IntValue.ToString();
                case ComplexGeneType.Float:
                    return FloatValue.ToString(CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ComplexGeneValue FromValue(ComplexGene complexGene, double value)
        {
            switch (complexGene.GeneType)
            {
                case ComplexGeneType.Bool:
                    return new ComplexGeneValue
                    {
                        GeneType = ComplexGeneType.Bool,
                        BoolValue = value > 0.5
                    };
                case ComplexGeneType.Int:
                    int maxValue = complexGene.MaxIntValue;
                    int minValue = complexGene.MinIntValue;
                    int intValue = (int)(value);
                    if (minValue < 0)
                        intValue -= Math.Abs(minValue);
                    else if (minValue > 0)
                        intValue += minValue;
                    if (intValue < minValue)
                        intValue = minValue;
                    if (intValue > maxValue)
                        intValue = maxValue;
                    return new ComplexGeneValue
                    {
                        GeneType = ComplexGeneType.Int,
                        IntValue = intValue,
                    };
                case ComplexGeneType.Float:
                    float maxFloatValue = complexGene.MaxFloatValue;
                    float minFloatValue = complexGene.MinFloatValue;
                    float floatValue = (float)(value);
                    if (minFloatValue < 0)
                        floatValue -= Math.Abs(minFloatValue);
                    else if (minFloatValue > 0)
                        floatValue += minFloatValue;
                    if (floatValue < minFloatValue)
                        floatValue = minFloatValue;
                    if (floatValue > maxFloatValue)
                        floatValue = maxFloatValue;
                    floatValue = (float)Math.Round(floatValue, complexGene.FractionDigits);
                    return new ComplexGeneValue
                    {
                        GeneType = ComplexGeneType.Float,
                        FloatValue = floatValue,
                    };
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}