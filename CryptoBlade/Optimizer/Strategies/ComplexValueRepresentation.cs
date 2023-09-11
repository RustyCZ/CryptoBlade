namespace CryptoBlade.Optimizer.Strategies
{
    public readonly record struct ComplexValueRepresentation(
        double MinValue,
        double MaxValue,
        int TotalBits,
        int FractionDigits);
}