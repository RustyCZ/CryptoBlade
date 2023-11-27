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
            options.Strategies.Qiqi.QflAbovePercentEnterShort = complexGenes[(int)QiqiGene.QflAbovePercentEnterShort + basicGeneLength].FloatValue;
            options.Strategies.Qiqi.RsiTakeProfitShort = complexGenes[(int)QiqiGene.RsiTakeProfitShort + basicGeneLength].IntValue;
            options.Strategies.Qiqi.TakeProfitPercentLong = complexGenes[(int)QiqiGene.TakeProfitPercentLong + basicGeneLength].FloatValue;
            options.Strategies.Qiqi.TakeProfitPercentShort = complexGenes[(int)QiqiGene.TakeProfitPercentShort + basicGeneLength].FloatValue;
        }

        private static ComplexGene[] CreateComplexGenes(IOptions<QiqiChromosomeOptions> options)
        {
            int geneLength = GetComplexGeneLength();
            int tradingBotGeneLength = GetGeneLength<RecursiveGridTradingBotGene>();
            var genes = new ComplexGene[geneLength];
            AddTradingBotGenesFromOptions(options, genes, 0);
            genes[(int)QiqiGene.QflBellowPercentEnterLong + tradingBotGeneLength] = options.Value.QflBellowPercentEnterLong.ToComplexGene();
            genes[(int)QiqiGene.RsiTakeProfitLong + tradingBotGeneLength] = options.Value.RsiTakeProfitLong.ToComplexGene();
            genes[(int)QiqiGene.QflAbovePercentEnterShort + tradingBotGeneLength] = options.Value.QflAbovePercentEnterShort.ToComplexGene();
            genes[(int)QiqiGene.RsiTakeProfitShort + tradingBotGeneLength] = options.Value.RsiTakeProfitShort.ToComplexGene();
            genes[(int)QiqiGene.TakeProfitPercentLong + tradingBotGeneLength] = options.Value.TakeProfitPercentLong.ToComplexGene();
            genes[(int)QiqiGene.TakeProfitPercentShort + tradingBotGeneLength] = options.Value.TakeProfitPercentShort.ToComplexGene();
            return genes;
        }

        private static int GetComplexGeneLength()
        {
            return GetGeneLength<RecursiveGridTradingBotGene>() + GetGeneLength<QiqiGene>();
        }
    }
}
