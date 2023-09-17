using CryptoBlade.Configuration;
using GeneticSharp;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer.Strategies.AutoHedge
{
    public class AutoHedgeChromosome : TradingBotChromosome
    {
        private readonly IOptions<AutoHedgeChromosomeOptions> m_options;

        public AutoHedgeChromosome(IOptions<AutoHedgeChromosomeOptions> options) : base(CreateComplexGenes(options))
        {
            m_options = options;
        }

        public override IChromosome CreateNew()
        {
            return new AutoHedgeChromosome(m_options);
        }

        public override void ApplyGenesToTradingBotOptions(TradingBotOptions options)
        {
            var complexGenes = ToComplexGeneValues();
            ApplyTradingBotGenes(options, complexGenes, 0);
            int basicGeneLength = GetGeneLength<TradingBotGene>();
            options.StrategyName = "AutoHedge";
            options.Strategies.AutoHedge.MinReentryPositionDistanceLong = Convert.ToDecimal(complexGenes[(int)AutoHedgeGene.MinReentryPositionDistanceLong + basicGeneLength].FloatValue);
            options.Strategies.AutoHedge.MinReentryPositionDistanceShort = Convert.ToDecimal(complexGenes[(int)AutoHedgeGene.MinReentryPositionDistanceShort + basicGeneLength].FloatValue);
        }

        private static ComplexGene[] CreateComplexGenes(IOptions<AutoHedgeChromosomeOptions> options)
        {
            int geneLength = GetComplexGeneLength();
            int tradingBotGeneLength = GetGeneLength<TradingBotGene>();
            var genes = new ComplexGene[geneLength];
            AddTradingBotGenesFromOptions(options, genes, 0);
            genes[(int)AutoHedgeGene.MinReentryPositionDistanceLong + tradingBotGeneLength] = options.Value.MinReentryPositionDistanceLong.ToComplexGene();
            genes[(int)AutoHedgeGene.MinReentryPositionDistanceShort + tradingBotGeneLength] = options.Value.MinReentryPositionDistanceShort.ToComplexGene();
            return genes;
        }

        private static int GetComplexGeneLength()
        {
            return GetGeneLength<TradingBotGene>() + GetGeneLength<AutoHedgeGene>();
        }
    }
}
