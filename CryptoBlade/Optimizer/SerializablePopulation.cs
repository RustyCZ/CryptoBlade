using System.Text.Json;
using GeneticSharp;
using Microsoft.Extensions.Options;

namespace CryptoBlade.Optimizer
{
    public class SerializablePopulationOptions
    {
        public required string PopulationFile { get; set; }
    }

    public class SerializablePopulation : Population
    {
        private readonly IOptions<SerializablePopulationOptions> m_options;
        private readonly IChromosome m_adamChromosome;

        public SerializablePopulation(IOptions<SerializablePopulationOptions> options,
            int minSize, 
            int maxSize, 
            IChromosome adamChromosome) : base(minSize, maxSize, adamChromosome)
        {
            m_adamChromosome = adamChromosome;
            m_options = options;
        }

        public override void CreateInitialGeneration()
        {
            if (!DeserializePopulation())
            {
                base.CreateInitialGeneration();
            }
        }

        public override void CreateNewGeneration(IList<IChromosome> chromosomes)
        {
            base.CreateNewGeneration(chromosomes);
            SerializePopulation();
        }

        public void SerializePopulation(string populationFile)
        {
            var chromosomes = CurrentGeneration.Chromosomes
                .Select(x => new ChromosomeModel
                {
                    Chromosome = x.ToString() ?? string.Empty,
                    Fitness = x.Fitness,
                }).ToArray();
            if (File.Exists(populationFile))
                File.Delete(populationFile);
            GenerationModel generationModel = new GenerationModel
            {
                Chromosomes = chromosomes,
                Number = CurrentGeneration.Number,
            };
            var serialized = JsonSerializer.Serialize(generationModel, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(populationFile, serialized);
        }

        private void SerializePopulation()
        {
            SerializePopulation(m_options.Value.PopulationFile);
        }

        private bool DeserializePopulation()
        { 
            var file = m_options.Value.PopulationFile;
            if (!File.Exists(file))
                return false;
            string content = File.ReadAllText(file);
            if (string.IsNullOrEmpty(content))
                return false;
            try
            {
                GenerationModel? generationModel = JsonSerializer.Deserialize<GenerationModel>(content);
                if(generationModel == null)
                    return false;
                bool validChromosomes = generationModel.Chromosomes.All(x => x.Chromosome.All(y => y == '0' || y == '1'));
                if (!validChromosomes)
                    return false;

                List<IChromosome> chromosomes = new List<IChromosome>();
            
                foreach (var chromosome in generationModel.Chromosomes)
                {
                    var c = m_adamChromosome.CreateNew();
                    for (int i = 0; i < chromosome.Chromosome.Length; i++)
                    {
                        int value = chromosome.Chromosome[i] == '1' ? 1 : 0;
                        c.ReplaceGene(i, new Gene(value));
                    }
                    chromosomes.Add(c);
                }
                chromosomes.ValidateGenes();
                var generation = new Generation(generationModel.Number, chromosomes);
                CurrentGeneration = generation;
                Generations.Add(CurrentGeneration);
                GenerationStrategy.RegisterNewGeneration(this);
                GenerationsNumber = generation.Number;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private class GenerationModel
        {
            public int Number { get; set; }
            public ChromosomeModel[] Chromosomes { get; set; } = Array.Empty<ChromosomeModel>();
        }

        public class ChromosomeModel
        {
            public string Chromosome { get; set; } = string.Empty;
            public double? Fitness { get; set; }
        }
    }
}
