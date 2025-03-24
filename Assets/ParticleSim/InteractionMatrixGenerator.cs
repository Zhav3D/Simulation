using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Random = UnityEngine.Random;

[RequireComponent(typeof(OptimizedParticleSimulation))]
public class InteractionMatrixGenerator : MonoBehaviour
{
    // Pattern type to generate
    public enum PatternType
    {
        Random,
        Clusters,
        Chains,
        PredatorPrey,
        Crystalline,
        Flocking,
        Lenia,
        Segregation
    }

    [Header("Generation Settings")]
    public PatternType patternType = PatternType.PredatorPrey;
    public bool generateOnAwake = true;
    public bool generateParticleTypes = true;
    public bool applyRecommendedSettings = true;

    [Header("Particle Scaling")]
    [Range(0.1f, 100f)] public float particleSpawnMultiplier = 1.0f;
    [Range(0.1f, 3f)] public float particleRadiusMultiplier = 1.0f;

    [Header("Matrix Configuration")]
    [Range(-1f, 1f)] public float attractionBias = 0f;  // Bias toward attraction (1) or repulsion (-1)
    [Range(0f, 1f)] public float symmetryFactor = 0.1f; // How symmetric should the matrix be
    [Range(0f, 1f)] public float sparsity = 0.2f;       // Proportion of neutral (0) interactions
    [Range(0f, 1f)] public float noiseFactor = 0.1f;    // Add some noise to deterministic patterns

    private OptimizedParticleSimulation simulation;

    void Awake()
    {
        simulation = GetComponent<OptimizedParticleSimulation>();

        if (generateOnAwake)
        {
            GenerateMatrix();
        }
    }

    // Call this method to regenerate the matrix
    public void GenerateMatrix()
    {
        if (simulation == null)
        {
            simulation = GetComponent<OptimizedParticleSimulation>();
        }

        // Generate particle types if requested
        if (generateParticleTypes)
        {
            GenerateParticleTypes();
        }

        // Apply recommended simulation settings if requested
        if (applyRecommendedSettings)
        {
            ParticlePatternPresets.ApplyRecommendedSettings(simulation, patternType);
        }

        // Clear existing interaction rules
        simulation.interactionRules.Clear();

        // Generate new rules based on pattern type
        switch (patternType)
        {
            case PatternType.Random:
                GenerateRandomMatrix();
                break;
            case PatternType.Clusters:
                GenerateClusterMatrix();
                break;
            case PatternType.Chains:
                GenerateChainMatrix();
                break;
            case PatternType.PredatorPrey:
                GeneratePredatorPreyMatrix();
                break;
            case PatternType.Crystalline:
                GenerateCrystallineMatrix();
                break;
            case PatternType.Flocking:
                GenerateFlockingMatrix();
                break;
            case PatternType.Lenia:
                GenerateLeniaMatrix();
                break;
            case PatternType.Segregation:
                GenerateSegregationMatrix();
                break;
        }
    }

    // Visualize the matrix in the inspector (editor only)
    public float[,] VisualizeMatrix()
    {
        int typeCount = simulation.particleTypes.Count;
        float[,] matrix = new float[typeCount, typeCount];

        // Initialize with zeros
        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                matrix[i, j] = 0f;
            }
        }

        // Fill from interaction rules
        foreach (var rule in simulation.interactionRules)
        {
            matrix[rule.typeIndexA, rule.typeIndexB] = rule.attractionValue;
        }

        return matrix;
    }

    private void AddInteractionRule(int typeA, int typeB, float value)
    {
        // Add a rule with the specified values
        var rule = new OptimizedParticleSimulation.InteractionRule
        {
            typeIndexA = typeA,
            typeIndexB = typeB,
            attractionValue = value
        };

        simulation.interactionRules.Add(rule);
    }

    private void GenerateRandomMatrix()
    {
        int typeCount = simulation.particleTypes.Count;

        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                // Skip self-interactions if needed
                if (i == j) continue;

                // Determine if this interaction should be neutral
                if (Random.value < sparsity)
                {
                    // Skip this interaction (will be 0 by default)
                    continue;
                }

                // Generate interaction with bias
                float threshold = (1f + attractionBias) / 2f; // Convert -1,1 to 0,1 range
                float value = Random.value < threshold ? 1f : -1f;

                // Add interaction rule
                AddInteractionRule(i, j, value);

                // Apply symmetry factor
                if (Random.value < symmetryFactor)
                {
                    // Make the reverse interaction the same
                    AddInteractionRule(j, i, value);
                }
                else if (Random.value < symmetryFactor / 2f)
                {
                    // Sometimes make it antisymmetric (opposite)
                    AddInteractionRule(j, i, -value);
                }
            }
        }
    }

    private void GenerateClusterMatrix()
    {
        int typeCount = simulation.particleTypes.Count;
        int groups = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(typeCount)));

        // Assign particles to groups
        int[] particleGroups = new int[typeCount];
        for (int i = 0; i < typeCount; i++)
        {
            particleGroups[i] = Random.Range(0, groups);
        }

        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                // Skip self-interactions
                if (i == j) continue;

                // Determine if this interaction should be neutral
                if (Random.value < sparsity)
                {
                    continue;
                }

                // Same group attracts, different group repels
                float value = (particleGroups[i] == particleGroups[j]) ? 1f : -1f;

                // Add some noise
                if (Random.value < noiseFactor)
                {
                    value *= -1f;
                }

                AddInteractionRule(i, j, value);
            }
        }
    }

    private void GenerateChainMatrix()
    {
        int typeCount = simulation.particleTypes.Count;

        // Create a random ordering of particles
        List<int> order = new List<int>();
        for (int i = 0; i < typeCount; i++)
        {
            order.Add(i);
        }

        // Shuffle the order
        for (int i = 0; i < order.Count; i++)
        {
            int j = Random.Range(i, order.Count);
            int temp = order[i];
            order[i] = order[j];
            order[j] = temp;
        }

        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                // Skip self-interactions
                if (i == j) continue;

                // Determine if this interaction should be neutral
                if (Random.value < sparsity)
                {
                    continue;
                }

                // Find positions in the chain
                int posI = order.IndexOf(i);
                int posJ = order.IndexOf(j);

                // Neighbors in the chain attract
                float value = (Mathf.Abs(posI - posJ) == 1) ? 1f : -1f;

                // Add some noise
                if (Random.value < noiseFactor)
                {
                    value *= -1f;
                }

                AddInteractionRule(i, j, value);
            }
        }
    }

    private void GeneratePredatorPreyMatrix()
    {
        int typeCount = simulation.particleTypes.Count;

        // Create a circular predator-prey relationship
        for (int i = 0; i < typeCount; i++)
        {
            int prey = (i + 1) % typeCount;
            int predator = (i - 1 + typeCount) % typeCount;

            // Predator attracts to prey
            AddInteractionRule(i, prey, 1f);

            // Prey repels from predator
            AddInteractionRule(i, predator, -1f);

            // Neutral or mild interactions with others
            for (int j = 0; j < typeCount; j++)
            {
                if (j != prey && j != predator && j != i)
                {
                    // Most interactions are neutral, some weak attraction/repulsion
                    float r = Random.value;
                    if (r < 0.7f)
                    {
                        // Leave as neutral (skip)
                    }
                    else if (r < 0.85f)
                    {
                        AddInteractionRule(i, j, 0.5f);
                    }
                    else
                    {
                        AddInteractionRule(i, j, -0.5f);
                    }
                }
            }
        }
    }

    private void GenerateCrystallineMatrix()
    {
        int typeCount = simulation.particleTypes.Count;

        // For crystalline structure, we want alternating attractions and repulsions
        // based on "distance" in type space

        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                if (i == j) continue;

                // Calculate "distance" between types (in a circular arrangement)
                int dist = Mathf.Min(Mathf.Abs(i - j), typeCount - Mathf.Abs(i - j));

                // Even distances attract, odd distances repel
                float value = (dist % 2 == 0) ? 1f : -1f;

                // Random neutral interactions
                if (Random.value < sparsity)
                {
                    continue;
                }

                AddInteractionRule(i, j, value);
            }
        }
    }

    private void GenerateFlockingMatrix()
    {
        int typeCount = simulation.particleTypes.Count;

        // For flocking, particles of the same type should align (weak attraction)
        // Different types should generally ignore each other with some exceptions

        // First, make all particles weakly attract their own type
        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                if (i == j) continue;

                if (i % 3 == j % 3) // Same "family"
                {
                    AddInteractionRule(i, j, 0.8f);
                }
                // Different families mostly neutral (skip)
            }
        }

        // Add some "leaders" that others follow
        int leaderCount = Mathf.Max(1, Mathf.FloorToInt(typeCount / 5));
        for (int k = 0; k < leaderCount; k++)
        {
            int leader = Random.Range(0, typeCount);

            for (int i = 0; i < typeCount; i++)
            {
                if (i != leader && Random.value < 0.7f)
                {
                    AddInteractionRule(i, leader, 1f); // Follow the leader
                }
            }
        }

        // Add some "avoiders" that others avoid
        int avoiderCount = Mathf.Max(1, Mathf.FloorToInt(typeCount / 5));
        for (int k = 0; k < avoiderCount; k++)
        {
            int avoider = Random.Range(0, typeCount);

            for (int i = 0; i < typeCount; i++)
            {
                if (i != avoider && Random.value < 0.7f)
                {
                    AddInteractionRule(i, avoider, -1f); // Avoid this type
                }
            }
        }
    }

    private void GenerateLeniaMatrix()
    {
        int typeCount = simulation.particleTypes.Count;

        // In Lenia, cells are influenced by their neighbors based on a kernel
        // We'll adapt this by creating "neighborhoods" of particle types

        // First create a circular distance matrix
        int[,] distance = new int[typeCount, typeCount];

        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                // Calculate circular distance
                distance[i, j] = Mathf.Min(Mathf.Abs(i - j), typeCount - Mathf.Abs(i - j));
            }
        }

        // Now set interactions based on distance
        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                if (i == j) continue;

                int dist = distance[i, j];

                // Create a pattern where close types attract, medium distances repel,
                // and far distances are neutral
                if (dist <= typeCount / 6)
                {
                    AddInteractionRule(i, j, 1f); // Close types attract
                }
                else if (dist <= typeCount / 3)
                {
                    AddInteractionRule(i, j, -1f); // Medium distances repel
                }
                else if (Random.value > 0.8f)
                {
                    // Far distances are mostly neutral with some noise
                    AddInteractionRule(i, j, Random.value < 0.5f ? 0.5f : -0.5f);
                }
            }
        }

        // Add some asymmetry for more interesting dynamics
        for (int i = 0; i < typeCount; i++)
        {
            for (int j = i + 1; j < typeCount; j++)
            {
                if (Random.value < 0.3f)
                {
                    // Find if there's an existing rule
                    OptimizedParticleSimulation.InteractionRule ruleIJ = null;
                    OptimizedParticleSimulation.InteractionRule ruleJI = null;

                    foreach (var rule in simulation.interactionRules)
                    {
                        if (rule.typeIndexA == i && rule.typeIndexB == j)
                        {
                            ruleIJ = rule;
                        }
                        if (rule.typeIndexA == j && rule.typeIndexB == i)
                        {
                            ruleJI = rule;
                        }
                    }

                    // 30% chance to flip one direction
                    if (Random.value < 0.5f && ruleIJ != null)
                    {
                        ruleIJ.attractionValue *= -1f;
                    }
                    else if (ruleJI != null)
                    {
                        ruleJI.attractionValue *= -1f;
                    }
                }
            }
        }
    }

    private void GenerateSegregationMatrix()
    {
        int typeCount = simulation.particleTypes.Count;

        // Create groups that self-attract and repel others
        int numGroups = Mathf.Min(Mathf.Max(2, Mathf.FloorToInt(typeCount / 3)), 5);
        int[] groups = new int[typeCount];

        for (int i = 0; i < typeCount; i++)
        {
            groups[i] = Random.Range(0, numGroups);
        }

        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                if (i == j) continue;

                if (groups[i] == groups[j])
                {
                    // Same group - strongly attract
                    AddInteractionRule(i, j, 1f);
                }
                else
                {
                    // Different group - strongly repel
                    AddInteractionRule(i, j, -1f);
                }
            }
        }

        // Add a few "bridge" particles that attract multiple groups
        int numBridges = Mathf.Max(1, Mathf.FloorToInt(typeCount / 10));
        for (int b = 0; b < numBridges; b++)
        {
            int bridge = Random.Range(0, typeCount);
            int attractsGroup = Random.Range(0, numGroups);

            for (int j = 0; j < typeCount; j++)
            {
                if (j == bridge) continue;

                if (groups[j] == attractsGroup)
                {
                    // Find and modify or add new rules
                    bool foundBridgeToJ = false;
                    bool foundJToBridge = false;

                    foreach (var rule in simulation.interactionRules)
                    {
                        if (rule.typeIndexA == bridge && rule.typeIndexB == j)
                        {
                            rule.attractionValue = 1f;
                            foundBridgeToJ = true;
                        }
                        if (rule.typeIndexA == j && rule.typeIndexB == bridge)
                        {
                            rule.attractionValue = 1f;
                            foundJToBridge = true;
                        }
                    }

                    if (!foundBridgeToJ)
                    {
                        AddInteractionRule(bridge, j, 1f);
                    }
                    if (!foundJToBridge)
                    {
                        AddInteractionRule(j, bridge, 1f);
                    }
                }
            }
        }
    }

    // Display the interaction matrix in-editor
    void OnGUI()
    {
        if (simulation == null || !Application.isEditor) return;

        // Display matrix only in edit mode for debugging
        float[,] matrix = VisualizeMatrix();
        int typeCount = simulation.particleTypes.Count;

        float cellSize = 20f;
        float startX = 10f;
        float startY = 10f;

        GUI.Label(new Rect(startX, startY - 30, 200, 30), "Interaction Matrix (" + patternType.ToString() + ")");

        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                float value = matrix[i, j];

                // Calculate color based on value
                Color cellColor;
                if (value > 0)
                {
                    // Green for attraction
                    cellColor = new Color(0, value, 0);
                }
                else if (value < 0)
                {
                    // Red for repulsion
                    cellColor = new Color(-value, 0, 0);
                }
                else
                {
                    // Gray for neutral
                    cellColor = new Color(0.5f, 0.5f, 0.5f);
                }

                GUI.backgroundColor = cellColor;
                GUI.Box(new Rect(startX + j * cellSize, startY + i * cellSize, cellSize, cellSize), "");
            }
        }

        // Reset color
        GUI.backgroundColor = Color.white;
    }

    // Add editor buttons to regenerate the matrix
    void OnValidate()
    {
        // Enforce valid ranges
        attractionBias = Mathf.Clamp(attractionBias, -1f, 1f);
        symmetryFactor = Mathf.Clamp01(symmetryFactor);
        sparsity = Mathf.Clamp01(sparsity);
        noiseFactor = Mathf.Clamp01(noiseFactor);

        // Ensure particle spawn multiplier stays positive
        particleSpawnMultiplier = Mathf.Max(0.1f, particleSpawnMultiplier);

        // Ensure particle radius multiplier stays positive
        particleRadiusMultiplier = Mathf.Max(0.2f, particleRadiusMultiplier);
    }

    // Generate appropriate particle types based on the pattern
    private void GenerateParticleTypes()
    {
        // Clear existing particle types
        simulation.particleTypes.Clear();

        // Number of particle types to generate (varies by pattern)
        int typeCount = 0;

        switch (patternType)
        {
            case PatternType.Random:
                typeCount = 5;
                CreateRandomParticleTypes(typeCount);
                break;

            case PatternType.Clusters:
                typeCount = 6;
                CreateClusterParticleTypes(typeCount);
                break;

            case PatternType.Chains:
                typeCount = 6;
                CreateChainParticleTypes(typeCount);
                break;

            case PatternType.PredatorPrey:
                typeCount = 5;
                CreatePredatorPreyParticleTypes(typeCount);
                break;

            case PatternType.Crystalline:
                typeCount = 8;
                CreateCrystallineParticleTypes(typeCount);
                break;

            case PatternType.Flocking:
                typeCount = 7;
                CreateFlockingParticleTypes(typeCount);
                break;

            case PatternType.Lenia:
                typeCount = 10;
                CreateLeniaParticleTypes(typeCount);
                break;

            case PatternType.Segregation:
                typeCount = 6;
                CreateSegregationParticleTypes(typeCount);
                break;
        }
    }

    // Helper method to add a particle type
    private void AddParticleType(string name, Color color, float mass, float radius, float spawnAmount)
    {
        var type = new OptimizedParticleSimulation.ParticleType
        {
            name = name,
            color = color,
            mass = mass,
            // Apply global radius multiplier
            radius = radius * particleRadiusMultiplier,
            // Apply global spawn multiplier
            spawnAmount = Mathf.Round(spawnAmount * particleSpawnMultiplier)
        };

        simulation.particleTypes.Add(type);
    }

    // Helper to generate a random color with good saturation and brightness
    private Color GenerateRandomColor(float saturation = 0.7f, float brightness = 0.9f)
    {
        return Color.HSVToRGB(Random.value, saturation, brightness);
    }

    // Create random particle types
    private void CreateRandomParticleTypes(int count)
    {
        for (int i = 0; i < count; i++)
        {
            string name = "Type" + i;
            Color color = GenerateRandomColor();
            float mass = Random.Range(0.8f, 1.2f);
            float radius = Random.Range(0.4f, 0.6f);
            float spawnAmount = Random.Range(40f, 60f);

            AddParticleType(name, color, mass, radius, spawnAmount);
        }
    }

    // Create cluster-optimized particle types
    private void CreateClusterParticleTypes(int count)
    {
        // Define group count (usually 2-3 groups work well)
        int groups = Mathf.Min(3, Mathf.FloorToInt(count / 2));

        // Generate colors for each group with similar hues within groups
        float[] groupHues = new float[groups];
        for (int g = 0; g < groups; g++)
        {
            groupHues[g] = Random.value;
        }

        for (int i = 0; i < count; i++)
        {
            int group = i % groups;

            // Create similar colors within group with slight variations
            float hue = groupHues[group] + Random.Range(-0.05f, 0.05f);
            hue = (hue + 1) % 1; // Ensure hue stays in 0-1 range

            string name = "Cluster" + group + "_" + i;
            Color color = Color.HSVToRGB(hue, 0.7f + Random.Range(-0.1f, 0.1f), 0.9f);
            float mass = 1.0f;
            float radius = 0.5f;
            float spawnAmount = 50f;

            AddParticleType(name, color, mass, radius, spawnAmount);
        }
    }

    // Create chain-optimized particle types
    private void CreateChainParticleTypes(int count)
    {
        // Create a gradient of colors for the chain
        for (int i = 0; i < count; i++)
        {
            float hue = (float)i / count; // Spread hues across the spectrum

            string name = "Link" + i;
            Color color = Color.HSVToRGB(hue, 0.8f, 0.9f);

            // Make each adjacent pair in the chain have similar mass
            float massVariation = Mathf.Sin((float)i / count * Mathf.PI * 2) * 0.3f;
            float mass = 1.0f + massVariation;

            float radius = 0.5f;
            float spawnAmount = 40f;

            AddParticleType(name, color, mass, radius, spawnAmount);
        }
    }

    // Create predator-prey optimized particle types
    private void CreatePredatorPreyParticleTypes(int count)
    {
        // Use a color wheel for predator-prey cycle
        // Predators are slightly larger than their prey
        for (int i = 0; i < count; i++)
        {
            float hue = (float)i / count;
            string name = "Species" + i;
            Color color = Color.HSVToRGB(hue, 0.9f, 0.9f);

            // Predators are slightly larger but slower (heavier)
            float mass = 1.0f + (i % 2) * 0.5f; // Alternating masses
            float radius = 0.4f + (i % 2) * 0.2f; // Alternating sizes

            // Fewer predators, more prey
            float spawnAmount = (i % 2 == 0) ? 60f : 40f;

            AddParticleType(name, color, mass, radius, spawnAmount);
        }
    }

    // Create crystalline optimized particle types
    private void CreateCrystallineParticleTypes(int count)
    {
        // For crystalline patterns, we want distinct particle types
        // with clear visual differentiation

        // Use primary and secondary colors with high contrast
        Color[] baseColors = new Color[] {
            Color.red, Color.green, Color.blue,
            Color.cyan, Color.magenta, Color.yellow,
            new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 1f)  // Purple
        };

        for (int i = 0; i < count; i++)
        {
            int colorIndex = i % baseColors.Length;
            Color baseColor = baseColors[colorIndex];

            // Add slight variations
            Color color = new Color(
                baseColor.r * Random.Range(0.9f, 1.0f),
                baseColor.g * Random.Range(0.9f, 1.0f),
                baseColor.b * Random.Range(0.9f, 1.0f)
            );

            string name = "Crystal" + i;
            // Uniform mass and size for crystalline structures
            float mass = 1.0f;
            float radius = 0.5f;
            float spawnAmount = 40f;

            AddParticleType(name, color, mass, radius, spawnAmount);
        }
    }

    // Create flocking optimized particle types
    private void CreateFlockingParticleTypes(int count)
    {
        // For flocking, group particles into 2-3 "species"
        int flockTypes = Mathf.Min(3, Mathf.CeilToInt(count / 3f));

        // Generate a base color for each flock
        Color[] flockColors = new Color[flockTypes];
        for (int i = 0; i < flockTypes; i++)
        {
            flockColors[i] = GenerateRandomColor(0.8f, 0.9f);
        }

        for (int i = 0; i < count; i++)
        {
            int flockIndex = i % flockTypes;
            Color baseColor = flockColors[flockIndex];

            // Slight color variation within a flock
            Color color = new Color(
                Mathf.Clamp01(baseColor.r + Random.Range(-0.1f, 0.1f)),
                Mathf.Clamp01(baseColor.g + Random.Range(-0.1f, 0.1f)),
                Mathf.Clamp01(baseColor.b + Random.Range(-0.1f, 0.1f))
            );

            string name = "Flock" + flockIndex + "_" + i;

            // One heavier "leader" particle per flock
            float mass = (i % flockTypes == 0) ? 2.0f : 1.0f;
            float radius = (i % flockTypes == 0) ? 0.8f : 0.5f;
            float spawnAmount = (i % flockTypes == 0) ? 10f : 60f;

            AddParticleType(name, color, mass, radius, spawnAmount);
        }
    }

    // Create Lenia optimized particle types
    private void CreateLeniaParticleTypes(int count)
    {
        // For Lenia patterns, a gradient of related colors works well
        // with particles of varying sizes
        float baseHue = Random.value; // Starting hue

        for (int i = 0; i < count; i++)
        {
            // Create a gradient around the color wheel
            float hue = (baseHue + (float)i / count * 0.6f) % 1.0f;

            string name = "Lenia" + i;
            Color color = Color.HSVToRGB(hue, 0.7f, 0.9f);

            // Lenia works well with varying particle sizes and masses
            float massVariation = Mathf.PerlinNoise(i * 0.5f, 0f) * 0.6f;
            float mass = 0.7f + massVariation;

            float radiusVariation = Mathf.PerlinNoise(i * 0.5f, 1f) * 0.3f;
            float radius = 0.4f + radiusVariation;

            // More small particles, fewer large ones
            float spawnAmount = 60f - (radius - 0.4f) * 50f;

            AddParticleType(name, color, mass, radius, spawnAmount);
        }
    }

    // Create segregation optimized particle types
    private void CreateSegregationParticleTypes(int count)
    {
        // For segregation, create 2-3 distinct groups with very different colors
        int groups = Mathf.Min(3, Mathf.CeilToInt(count / 2f));

        // Define highly contrasting colors for groups
        Color[] groupColors = new Color[groups];
        float hueStep = 1.0f / groups;

        for (int g = 0; g < groups; g++)
        {
            groupColors[g] = Color.HSVToRGB(g * hueStep, 1.0f, 1.0f);
        }

        // Add one "bridge" particle type per simulation
        bool addedBridge = false;

        for (int i = 0; i < count; i++)
        {
            if (i == count - 1 && !addedBridge)
            {
                // Last type is a "bridge" particle
                string name = "Bridge";
                Color color = Color.white; // Neutral color
                float mass = 1.5f;
                float radius = 0.7f;
                float spawnAmount = 5f; // Few bridge particles

                AddParticleType(name, color, mass, radius, spawnAmount);
                addedBridge = true;
            }
            else
            {
                // Regular group particles
                int group = i % groups;
                Color baseColor = groupColors[group];

                // Slight variation within group
                Color color = new Color(
                    Mathf.Clamp01(baseColor.r * Random.Range(0.9f, 1.0f)),
                    Mathf.Clamp01(baseColor.g * Random.Range(0.9f, 1.0f)),
                    Mathf.Clamp01(baseColor.b * Random.Range(0.9f, 1.0f))
                );

                string name = "Group" + group + "_" + i;
                float mass = 1.0f;
                float radius = 0.5f;
                float spawnAmount = 40f;

                AddParticleType(name, color, mass, radius, spawnAmount);
            }
        }
    }
}