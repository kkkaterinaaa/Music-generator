using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Midi;

class MusicGenerator
{
    //load melody from a MIDI file
    private static List<int> LoadMelody(string midiFile)
    {
        var melody = new List<int>();
        var midiFileReader = new MidiFile(midiFile, false);

        foreach (var midiTrack in midiFileReader.Events)
        {
            foreach (var midiEvent in midiTrack)
            {
                if (midiEvent.CommandCode == MidiCommandCode.NoteOn)
                {
                    var noteEvent = (NoteOnEvent)midiEvent;
                    melody.Add(noteEvent.NoteNumber);
                }
            }
        }

        return melody;
    }

    // Build the Markov chain for melody generation
    private static Dictionary<(int, int), List<int>> BuildMarkovChain(List<int> melody)
    {
        var melodyChain = new Dictionary<(int, int), List<int>>();

        // Iterate through the melody notes to create pairs and predict the next note
        for (int i = 0; i < melody.Count - 2; i++)
        {
            var key = (melody[i], melody[i + 1]);
            int nextNote = melody[i + 2];

            if (!melodyChain.ContainsKey(key))
            {
                melodyChain[key] = new List<int>();
            }

            melodyChain[key].Add(nextNote);
        }

        return melodyChain;
    }

    // Generate a new melody using the Markov chain
    private static List<int> GenerateMelody(Dictionary<(int, int), List<int>> melodyChain, List<int> melody, int length = 200)
    {
        var random = new Random();
        var generatedMelody = new List<int> { melody[0], melody[1] }; // Start with the first two notes
        var currentKey = (melody[0], melody[1]);

        for (int i = 0; i < length - 2; i++)
        {
            if (melodyChain.ContainsKey(currentKey))
            {
                var nextNotes = melodyChain[currentKey];
                int nextNote = nextNotes[random.Next(nextNotes.Count)];
                generatedMelody.Add(nextNote);
                currentKey = (currentKey.Item2, nextNote);
            }
            else
            {
                break; // Stop if no further predictions can be made
            }
        }

        return generatedMelody;
    }

    // Generate chords for a given key
    public static List<int[]> GenerateChords(int key, bool isMinor)
    {
        var chords = new List<int[]>();
        // Define triads for chord generation
        int[] majorTriad = { 0, 4, 7 };
        int[] minorTriad = { 0, 3, 7 };
        int[] diminishedTriad = { 0, 3, 6 };

        for (int i = 0; i < 7; i++)
        {
            if (isMinor)
            {
                if (i == 0 || i == 3 || i == 4)
                {
                    chords.Add(minorTriad.Select(p => (p + key + i) % 12).ToArray());
                }
                else if (i == 2 || i == 5)
                {
                    chords.Add(majorTriad.Select(p => (p + key + i) % 12).ToArray());
                }
                else if (i == 6)
                {
                    chords.Add(diminishedTriad.Select(p => (p + key + i) % 12).ToArray());
                }
            }
            else
            {
                if (i == 0 || i == 3 || i == 4)
                {
                    chords.Add(majorTriad.Select(p => (p + key + i) % 12).ToArray());
                }
                else if (i == 1 || i == 2 || i == 5)
                {
                    chords.Add(minorTriad.Select(p => (p + key + i) % 12).ToArray());
                }
                else if (i == 6)
                {
                    chords.Add(diminishedTriad.Select(p => (p + key + i) % 12).ToArray());
                }
            }
        }

        return chords;
    }

    // Calculate fitness for a chord sequence
    public static int CalculateFitness(List<int[]> individual, List<int> melody)
    {
        int fitness = 0;

        // Reward if melody notes are part of the chords
        foreach (var chord in individual)
        {
            if (melody.Any(note => chord.Contains(note % 12)))
            {
                fitness += 10;
            }
        }
        // Penalize repetition of consecutive chords
        for (int i = 1; i < individual.Count; i++)
        {
            if (individual[i].SequenceEqual(individual[i - 1]))
            {
                fitness -= 5;
            }
        }

        return fitness;
    }

    // Genetic algorithm implementation
    public static List<int[]> RunGeneticAlgorithm(List<int> melody, List<int[]> chords, int populationSize, int generations, double mutationProbability, double crossoverProbability)
    {
        var random = new Random();
        var currentGeneration = new List<List<int[]>>();

        // Initialize population
        for (int i = 0; i < populationSize; i++)
        {
            var individual = new List<int[]>();
            for (int j = 0; j < melody.Count / 4; j++)
            {
                individual.Add(chords[random.Next(chords.Count)]);
            }
            currentGeneration.Add(individual);
        }

        // Evolve generations
        for (int generation = 0; generation < generations; generation++)
        {
            var nextGeneration = new List<List<int[]>>();

            var fitnessScores = currentGeneration
                .Select(ind => CalculateFitness(ind, melody))
                .ToList();

            // Selection and crossover
            for (int i = 0; i < populationSize; i += 2)
            {
                var parent1 = SelectIndividual(currentGeneration, fitnessScores);
                var parent2 = SelectIndividual(currentGeneration, fitnessScores);

                if (random.NextDouble() < crossoverProbability)
                {
                    var (child1, child2) = Crossover(parent1, parent2);
                    nextGeneration.Add(child1);
                    nextGeneration.Add(child2);
                }
                else
                {
                    nextGeneration.Add(parent1);
                    nextGeneration.Add(parent2);
                }
            }

            // Mutation
            foreach (var individual in nextGeneration)
            {
                if (random.NextDouble() < mutationProbability)
                {
                    Mutate(individual, chords);
                }
            }

            currentGeneration = nextGeneration;
        }

        return currentGeneration
            .OrderByDescending(ind => CalculateFitness(ind, melody))
            .First();
    }

    private static List<int[]> SelectIndividual(List<List<int[]>> generation, List<int> fitnessScores)
    {
        var random = new Random();
        int totalFitness = fitnessScores.Sum();
        int rouletteWheel = random.Next(totalFitness);

        int cumulative = 0;
        for (int i = 0; i < fitnessScores.Count; i++)
        {
            cumulative += fitnessScores[i];
            if (cumulative >= rouletteWheel)
            {
                return generation[i];
            }
        }

        return generation[0];
    }

    private static (List<int[]>, List<int[]>) Crossover(List<int[]> parent1, List<int[]> parent2)
    {
        var random = new Random();
        int crossoverPoint = random.Next(1, parent1.Count - 1);

        var child1 = parent1.Take(crossoverPoint).Concat(parent2.Skip(crossoverPoint)).ToList();
        var child2 = parent2.Take(crossoverPoint).Concat(parent1.Skip(crossoverPoint)).ToList();

        return (child1, child2);
    }

    private static void Mutate(List<int[]> individual, List<int[]> chords)
    {
        var random = new Random();
        int index = random.Next(individual.Count);
        individual[index] = chords[random.Next(chords.Count)];
    }

    // Write the generated melody and chords to a MIDI file
    private static void WriteMidi(string outputFilePath, List<int> melody, List<int[]> accompaniment, int velocity, int bpm)
    {
        int tempo = 60000000 / bpm; // Convert BPM to microseconds per quarter note
        var midiEvents = new MidiEventCollection(1, 480);

        // Melody track
        var melodyTrack = new List<MidiEvent>
        {
            new TempoEvent(tempo, 0)
        };

        int melodyTime = 0;
        foreach (var note in melody)
        {
            melodyTrack.Add(new NoteOnEvent(melodyTime, 1, note, velocity, 240));
            melodyTrack.Add(new NoteOnEvent(melodyTime + 240, 1, note, 0, 240));
            melodyTime += 240;
        }
        midiEvents.AddTrack(melodyTrack);

        // Chord track
        var chordTrack = new List<MidiEvent>();
        int chordTime = 0;
        foreach (var chord in accompaniment)
        {
            foreach (var note in chord)
            {
                chordTrack.Add(new NoteOnEvent(chordTime, 2, note + 48, velocity, 960));
                chordTrack.Add(new NoteOnEvent(chordTime + 960, 2, note + 48, 0, 960));
            }
            chordTime += 960;
        }
        midiEvents.AddTrack(chordTrack);

        MidiFile.Export(outputFilePath, midiEvents);
        Console.WriteLine($"Generated MIDI file saved as {outputFilePath}");
    }

    // generate music
    public static void Main(string[] args)
    {
        string inputMidi = "gravity.mid";
        string outputMidi = "output_combined2.mid";

        // Load input melody
        var originalMelody = LoadMelody(inputMidi);

        // Generate melody using Markov chain
        var melodyChain = BuildMarkovChain(originalMelody);
        var generatedMelody = GenerateMelody(melodyChain, originalMelody);

        // Generate chords
        var chords = GenerateChords(0, false);

        // Run Genetic Algorithm for best accompaniment
        var bestAccompaniment = RunGeneticAlgorithm(generatedMelody, chords, 50, 100, 0.05, 0.8);

        // Write MIDI
        WriteMidi(outputMidi, generatedMelody, bestAccompaniment, 100, 120);

        Console.WriteLine("Music generation complete!");
    }
}
