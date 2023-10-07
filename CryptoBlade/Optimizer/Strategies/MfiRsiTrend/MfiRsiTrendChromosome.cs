using CryptoBlade.Configuration;
using CryptoBlade.Strategies;
using GeneticSharp;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer.Strategies.MfiRsiTrend
{
    public class MfiRsiTrendChromosome : TradingBotChromosome
    {
        private readonly IOptions<MfiRsiTrendChromosomeOptions> m_options;

        public MfiRsiTrendChromosome(IOptions<MfiRsiTrendChromosomeOptions> options) : base(CreateComplexGenes(options))
        {
            m_options = options;
        }

        public override IChromosome CreateNew()
        {
            return new MfiRsiTrendChromosome(m_options);
        }

        public override void ApplyGenesToTradingBotOptions(TradingBotOptions options)
        {
            var complexGenes = ToComplexGeneValues();
            ApplyTradingBotGenes(options, complexGenes, 0);
            int basicGeneLength = GetGeneLength<TradingBotGene>();
            options.StrategyName = StrategyNames.MfiRsiEriTrend;
            options.Strategies.MfiRsiEriTrend.MinReentryPositionDistanceLong = Convert.ToDecimal(complexGenes[(int)MfiRsiTrendGene.MinReentryPositionDistanceLong + basicGeneLength].FloatValue);
            options.Strategies.MfiRsiEriTrend.MinReentryPositionDistanceShort = Convert.ToDecimal(complexGenes[(int)MfiRsiTrendGene.MinReentryPositionDistanceShort + basicGeneLength].FloatValue);
            options.Strategies.MfiRsiEriTrend.MfiRsiLookbackPeriod = complexGenes[(int)MfiRsiTrendGene.MfiRsiLookbackPeriod + basicGeneLength].IntValue;
            options.Strategies.MfiRsiEriTrend.UseEriOnly = complexGenes[(int)MfiRsiTrendGene.UseEriOnly + basicGeneLength].BoolValue;
        }

        private static ComplexGene[] CreateComplexGenes(IOptions<MfiRsiTrendChromosomeOptions> options)
        {
            int geneLength = GetComplexGeneLength();
            int tradingBotGeneLength = GetGeneLength<TradingBotGene>();
            var genes = new ComplexGene[geneLength];
            AddTradingBotGenesFromOptions(options, genes, 0);
            genes[(int)MfiRsiTrendGene.MinReentryPositionDistanceLong + tradingBotGeneLength] = options.Value.MinReentryPositionDistanceLong.ToComplexGene();
            genes[(int)MfiRsiTrendGene.MinReentryPositionDistanceShort + tradingBotGeneLength] = options.Value.MinReentryPositionDistanceShort.ToComplexGene();
            genes[(int)MfiRsiTrendGene.MfiRsiLookbackPeriod + tradingBotGeneLength] = options.Value.MfiRsiLookbackPeriod.ToComplexGene();
            genes[(int)MfiRsiTrendGene.UseEriOnly + tradingBotGeneLength] = options.Value.UseEriOnly.ToComplexGene();
            return genes;
        }

        private static int GetComplexGeneLength()
        {
            return GetGeneLength<TradingBotGene>() + GetGeneLength<MfiRsiTrendGene>();
        }
    }
}
