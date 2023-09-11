using GeneticSharp;

namespace CryptoBlade.Optimizer.Strategies
{
    public class ComplexChromosome : FloatingPointChromosome
    {
        public ComplexChromosome(ComplexGene[] complexGenes) :
            base(
                GetMinValues(complexGenes),
                GetMaxValues(complexGenes),
                GetTotalBits(complexGenes),
                GetFractionDigits(complexGenes))
        {
            ComplexGenes = complexGenes;
        }

        protected ComplexGene[] ComplexGenes { get; }

        public ComplexGeneValue[] ToComplexGeneValues()
        {
            var floatingPoints = ToFloatingPoints();
            var values = new ComplexGeneValue[ComplexGenes.Length];
            for (int i = 0; i < ComplexGenes.Length; i++)
            {
                var gene = ComplexGenes[i];
                var value = floatingPoints[i];
                values[i] = ComplexGeneValue.FromValue(gene, value);
            }
            return values;
        }

        public override IChromosome CreateNew()
        {
            return new ComplexChromosome(ComplexGenes);
        }

        private static double[] GetMinValues(ComplexGene[] complexGenes)
        {
            var minValues = new double[complexGenes.Length];
            for (int i = 0; i < complexGenes.Length; i++)
                minValues[i] = complexGenes[i].ValueRepresentation.MinValue;
            return minValues;
        }

        private static double[] GetMaxValues(ComplexGene[] complexGenes)
        {
            var maxValues = new double[complexGenes.Length];
            for (int i = 0; i < complexGenes.Length; i++)
                maxValues[i] = complexGenes[i].ValueRepresentation.MaxValue;
            return maxValues;
        }

        private static int[] GetTotalBits(ComplexGene[] complexGenes)
        {
            var totalBits = new int[complexGenes.Length];
            for (int i = 0; i < complexGenes.Length; i++)
                totalBits[i] = complexGenes[i].ValueRepresentation.TotalBits;
            return totalBits;
        }

        private static int[] GetFractionDigits(ComplexGene[] complexGenes)
        {
            var fractionDigits = new int[complexGenes.Length];
            for (int i = 0; i < complexGenes.Length; i++)
                fractionDigits[i] = complexGenes[i].ValueRepresentation.FractionDigits;
            return fractionDigits;
        }
    }
}
