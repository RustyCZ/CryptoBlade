﻿using GeneticSharp;

namespace CryptoBlade.Configuration
{
    public class GeneticAlgorithmOptions
    {
        public int GenerationCount { get; set; } = 200;
        public float MutationProbability { get; set; } = 0.01f;
        public float CrossoverProbability { get; set; } = 0.95f;
        public int MinPopulationSize { get; set; } = 200;
        public int MaxPopulationSize { get; set; } = 250;
        public MutationStrategy MutationStrategy { get; set; } = MutationStrategy.UniformMutation;
        public SelectionStrategy SelectionStrategy { get; set; } = SelectionStrategy.RankSelection;
        public float MutationMultiplier { get; set; } = 2.0f;
        public float MaxMutationProbability { get; set; } = 0.8f;
        public FitnessOptions FitnessOptions { get; set; } = new FitnessOptions();
    }
}