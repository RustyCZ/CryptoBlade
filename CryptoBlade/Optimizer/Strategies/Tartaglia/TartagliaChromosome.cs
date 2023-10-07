using CryptoBlade.Configuration;
using CryptoBlade.Strategies;
using GeneticSharp;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer.Strategies.Tartaglia
{
    public sealed class TartagliaChromosome : TradingBotChromosome
    {
        private readonly IOptions<TartagliaChromosomeOptions> m_options;

        public TartagliaChromosome(IOptions<TartagliaChromosomeOptions> options) : base(CreateComplexGenes(options))
        {
            m_options = options;
        }

        public override IChromosome CreateNew()
        {
            return new TartagliaChromosome(m_options);
        }

        public override void ApplyGenesToTradingBotOptions(TradingBotOptions options)
        {
            var complexGenes = ToComplexGeneValues();
            ApplyTradingBotGenes(options, complexGenes, 0);
            int basicGeneLength = GetGeneLength<TradingBotGene>();
            options.StrategyName = StrategyNames.Tartaglia;
            options.Strategies.Tartaglia.ChannelLengthLong = complexGenes[(int)TartagliaGene.ChannelLengthLong + basicGeneLength].IntValue;
            options.Strategies.Tartaglia.ChannelLengthShort = complexGenes[(int)TartagliaGene.ChannelLengthShort + basicGeneLength].IntValue;
            options.Strategies.Tartaglia.StandardDeviationLong = complexGenes[(int)TartagliaGene.StandardDeviationLong + basicGeneLength].FloatValue;
            options.Strategies.Tartaglia.StandardDeviationShort = complexGenes[(int)TartagliaGene.StandardDeviationShort + basicGeneLength].FloatValue;
            options.Strategies.Tartaglia.MinReentryPositionDistanceLong = Convert.ToDecimal(complexGenes[(int)TartagliaGene.MinReentryPositionDistanceLong + basicGeneLength].FloatValue);
            options.Strategies.Tartaglia.MinReentryPositionDistanceShort = Convert.ToDecimal(complexGenes[(int)TartagliaGene.MinReentryPositionDistanceShort + basicGeneLength].FloatValue);
        }

        private static ComplexGene[] CreateComplexGenes(IOptions<TartagliaChromosomeOptions> options)
        {
            int geneLength = GetComplexGeneLength();
            int tradingBotGeneLength = GetGeneLength<TradingBotGene>();
            var genes = new ComplexGene[geneLength];
            AddTradingBotGenesFromOptions(options, genes, 0);
            genes[(int)TartagliaGene.ChannelLengthLong + tradingBotGeneLength] = options.Value.ChannelLengthLong.ToComplexGene();
            genes[(int)TartagliaGene.ChannelLengthShort + tradingBotGeneLength] = options.Value.ChannelLengthShort.ToComplexGene();
            genes[(int)TartagliaGene.StandardDeviationLong + tradingBotGeneLength] = options.Value.StandardDeviationLong.ToComplexGene();
            genes[(int)TartagliaGene.StandardDeviationShort + tradingBotGeneLength] = options.Value.StandardDeviationShort.ToComplexGene();
            genes[(int)TartagliaGene.MinReentryPositionDistanceLong + tradingBotGeneLength] = options.Value.MinReentryPositionDistanceLong.ToComplexGene();
            genes[(int)TartagliaGene.MinReentryPositionDistanceShort + tradingBotGeneLength] = options.Value.MinReentryPositionDistanceShort.ToComplexGene();
            return genes;
        }
        
        private static int GetComplexGeneLength()
        {
            return GetGeneLength<TradingBotGene>() + GetGeneLength<TartagliaGene>();
        }
    }
}
