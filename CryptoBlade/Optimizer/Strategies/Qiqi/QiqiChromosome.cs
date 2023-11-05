using CryptoBlade.Configuration;
using CryptoBlade.Strategies;
using GeneticSharp;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer.Strategies.Qiqi
{
    public class QiqiChromosome : RecursiveGridTradingBotChromosome
    {
        private readonly IOptions<QiqiChromosomeOptions> m_options;

        public QiqiChromosome(IOptions<QiqiChromosomeOptions> options) : base(CreateComplexGenes(options))
        {
            m_options = options;
        }

        public override IChromosome CreateNew()
        {
            return new QiqiChromosome(m_options);
        }

        public override void ApplyGenesToTradingBotOptions(TradingBotOptions options)
        {
            var complexGenes = ToComplexGeneValues();
            ApplyTradingBotGenes(options, complexGenes, 0);
            int basicGeneLength = GetGeneLength<RecursiveGridTradingBotGene>();
            options.StrategyName = StrategyNames.Qiqi;
            options.Strategies.Qiqi.QflBellowPercentEnterLong = complexGenes[(int)QiqiGene.QflBellowPercentEnterLong + basicGeneLength].FloatValue;
            options.Strategies.Qiqi.RsiTakeProfitLong = complexGenes[(int)QiqiGene.RsiTakeProfitLong + basicGeneLength].IntValue;
        }

        private static ComplexGene[] CreateComplexGenes(IOptions<QiqiChromosomeOptions> options)
        {
            int geneLength = GetComplexGeneLength();
            int tradingBotGeneLength = GetGeneLength<RecursiveGridTradingBotGene>();
            var genes = new ComplexGene[geneLength];
            AddTradingBotGenesFromOptions(options, genes, 0);
            genes[(int)QiqiGene.QflBellowPercentEnterLong + tradingBotGeneLength] = options.Value.QflBellowPercentEnterLong.ToComplexGene();
            genes[(int)QiqiGene.RsiTakeProfitLong + tradingBotGeneLength] = options.Value.RsiTakeProfitLong.ToComplexGene();
            return genes;
        }

        private static int GetComplexGeneLength()
        {
            return GetGeneLength<RecursiveGridTradingBotGene>() + GetGeneLength<QiqiGene>();
        }
    }
}
