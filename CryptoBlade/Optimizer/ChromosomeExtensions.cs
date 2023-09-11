using CryptoBlade.Optimizer.Strategies;
using GeneticSharp;

namespace CryptoBlade.Optimizer
{
    public static class ChromosomeExtensions
    {
        public static ComplexGene ToComplexGene(this OptimizerBoolRange value)
        {
            return ComplexGene.FromValue(value.Min, value.Max);
        }

        public static ComplexGene ToComplexGene(this OptimizerIntRange value)
        {
            return ComplexGene.FromValue(value.Min, value.Max);
        }

        public static ComplexGene ToComplexGene(this OptimizerFloatRange value)
        {
            return ComplexGene.FromValue(value.Min, value.Max, value.FractionDigits);
        }
    }
}
