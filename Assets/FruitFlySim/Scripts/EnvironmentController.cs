// Environment Simulation System for Fruit Fly Cell Simulator
// Handles physical environment and chemical gradients

using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using System.Collections.Generic;

// Environment Types that cells might exist in
public enum EnvironmentType
{
    Hemolymph,  // Insect blood/fluid
    Epithelial, // Surface tissue
    Nervous,    // Brain/nervous system
    Digestive,  // Gut/digestive system
    Muscle,     // Muscular tissue
    Air         // External environment
}

// Environment parameters - chemical composition of local environment
public struct EnvironmentData
{
    public EnvironmentType type;
    public float3 position;
    public float temperature;
    public float pH;
    public float oxygen;
    public float nutrients;
    public float wasteProducts;
    public NativeArray<float> signalConcentrations; // Chemical signals in environment
}

// Gradient field for representing chemical concentrations across space
public class ChemicalGradient
{
    public string chemicalName;
    public float diffusionRate;
    public float decayRate;
    public List<GradientSource> sources = new List<GradientSource>();

    // Represents a source of the chemical
    public struct GradientSource
    {
        public float3 position;
        public float strength;
        public float radius;
        public float duration;
    }

    public ChemicalGradient(string name, float diffusion, float decay)
    {
        chemicalName = name;
        diffusionRate = diffusion;
        decayRate = decay;
    }

    public void AddSource(float3 position, float strength, float radius)
    {
        sources.Add(new GradientSource
        {
            position = position,
            strength = strength,
            radius = radius,
            duration = 0f
        });
    }

    public void Update(float deltaTime)
    {
        // Update all sources
        for (int i = 0; i < sources.Count; i++)
        {
            var source = sources[i];

            // Age the source
            source.duration += deltaTime;

            // Decay strength
            source.strength *= (1f - (decayRate * deltaTime));

            // Expand radius based on diffusion
            source.radius += diffusionRate * deltaTime;

            // Remove if strength is too low
            if (source.strength < 0.05f)
            {
                sources.RemoveAt(i);
                i--;
                continue;
            }

            // Update in list
            sources[i] = source;
        }
    }

    // Calculate the concentration at a specific position
    public float GetConcentrationAt(float3 position)
    {
        float concentration = 0f;

        // Sum contributions from all sources
        foreach (var source in sources)
        {
            float distance = math.distance(position, source.position);

            if (distance <= source.radius)
            {
                // Calculate falloff based on distance
                float falloff = 1f - (distance / source.radius);
                concentration += source.strength * falloff;
            }
        }

        return math.min(concentration, 1f); // Cap at 1.0
    }
}

// Main environment controller
public class EnvironmentController : MonoBehaviour
{
    // Reference to main simulation
    public FruitFlyCellSimulator mainSimulator;

    [Header("Environment Parameters")]
    public float baseTemperature = 25f; // Celsius
    public float basePH = 6.8f;         // Slightly acidic
    public float baseOxygen = 0.8f;     // Relative concentration
    public float baseNutrients = 0.7f;  // Relative concentration

    [Header("Environment Regions")]
    public int environmentRegionCount = 10;
    public float regionSize = 20f;

    // Chemical gradients in the environment
    private Dictionary<string, ChemicalGradient> chemicalGradients = new Dictionary<string, ChemicalGradient>();

    // Environment regions
    [HideInInspector] public List<EnvironmentData> environmentRegions = new List<EnvironmentData>();

    // Diffusion job data
    private NativeArray<float3> regionPositions;
    private NativeArray<float> regionTemperatures;
    private NativeArray<float> regionOxygen;
    private NativeArray<float> regionNutrients;

    void Start()
    {
        InitializeChemicalGradients();
        InitializeEnvironmentRegions();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;

        // Update chemical gradients
        foreach (var gradient in chemicalGradients.Values)
        {
            gradient.Update(deltaTime);
        }

        // Run diffusion simulation
        SimulateDiffusion(deltaTime);

        // Update environment regions
        UpdateEnvironmentRegions();

        // Apply environment effects to cells
        ApplyEnvironmentToCells();
    }

    private void InitializeChemicalGradients()
    {
        // Create standard gradients found in insect physiology

        // Ecdysone - growth and development hormone
        chemicalGradients.Add("ecdysone", new ChemicalGradient("ecdysone", 5f, 0.1f));

        // Insulin-like peptides - energy metabolism
        chemicalGradients.Add("insulin", new ChemicalGradient("insulin", 8f, 0.2f));

        // Juvenile hormone - maintains larval state
        chemicalGradients.Add("juvenile_hormone", new ChemicalGradient("juvenile_hormone", 3f, 0.05f));

        // Octopamine - neurotransmitter (like norepinephrine)
        chemicalGradients.Add("octopamine", new ChemicalGradient("octopamine", 10f, 0.3f));

        // Serotonin - neurotransmitter
        chemicalGradients.Add("serotonin", new ChemicalGradient("serotonin", 12f, 0.25f));

        // Add some initial sources
        float3 center = new float3(0, 0, 0);
        chemicalGradients["ecdysone"].AddSource(center + new float3(20, 0, 0), 1f, 30f);
        chemicalGradients["insulin"].AddSource(center + new float3(-20, 10, 0), 0.8f, 25f);
        chemicalGradients["juvenile_hormone"].AddSource(center + new float3(0, -20, 10), 0.9f, 40f);
    }

    private void InitializeEnvironmentRegions()
    {
        regionPositions = new NativeArray<float3>(environmentRegionCount, Allocator.Persistent);
        regionTemperatures = new NativeArray<float>(environmentRegionCount, Allocator.Persistent);
        regionOxygen = new NativeArray<float>(environmentRegionCount, Allocator.Persistent);
        regionNutrients = new NativeArray<float>(environmentRegionCount, Allocator.Persistent);

        // Create environment regions
        for (int i = 0; i < environmentRegionCount; i++)
        {
            // Create positions in 3D space - distribute throughout simulation volume
            float3 position = new float3(
                UnityEngine.Random.Range(-mainSimulator.worldBounds / 2, mainSimulator.worldBounds / 2),
                UnityEngine.Random.Range(-mainSimulator.worldBounds / 2, mainSimulator.worldBounds / 2),
                UnityEngine.Random.Range(-mainSimulator.worldBounds / 2, mainSimulator.worldBounds / 2)
            );

            // Determine environment type based on position
            EnvironmentType envType = DetermineEnvironmentType(position);

            // Set initial environment parameters
            float temperature = baseTemperature + UnityEngine.Random.Range(-2f, 2f);
            float pH = basePH + UnityEngine.Random.Range(-0.5f, 0.5f);
            float oxygen = baseOxygen;
            float nutrients = baseNutrients;

            // Adjust based on environment type
            AdjustEnvironmentParameters(ref temperature, ref pH, ref oxygen, ref nutrients, envType);

            // Create signal concentration array
            NativeArray<float> signalConcentrations = new NativeArray<float>(
                System.Enum.GetValues(typeof(SignalType)).Length,
                Allocator.Persistent
            );

            // Create environment data
            EnvironmentData envData = new EnvironmentData
            {
                type = envType,
                position = position,
                temperature = temperature,
                pH = pH,
                oxygen = oxygen,
                nutrients = nutrients,
                wasteProducts = 0f,
                signalConcentrations = signalConcentrations
            };

            environmentRegions.Add(envData);

            // Store data for jobs
            regionPositions[i] = position;
            regionTemperatures[i] = temperature;
            regionOxygen[i] = oxygen;
            regionNutrients[i] = nutrients;
        }
    }

    private EnvironmentType DetermineEnvironmentType(float3 position)
    {
        // Simplified environment type determination based on position
        float distFromCenter = math.length(position);

        if (distFromCenter < mainSimulator.worldBounds * 0.2f)
        {
            return EnvironmentType.Nervous; // Center is nervous system
        }
        else if (position.y > mainSimulator.worldBounds * 0.3f)
        {
            return EnvironmentType.Epithelial; // Top is epithelial
        }
        else if (position.y < -mainSimulator.worldBounds * 0.3f)
        {
            return EnvironmentType.Digestive; // Bottom is digestive
        }
        else if (math.abs(position.x) > mainSimulator.worldBounds * 0.3f)
        {
            return EnvironmentType.Muscle; // Sides are muscle
        }
        else
        {
            return EnvironmentType.Hemolymph; // Default is hemolymph (insect blood)
        }
    }

    private void AdjustEnvironmentParameters(ref float temperature, ref float pH, ref float oxygen, ref float nutrients, EnvironmentType envType)
    {
        // Adjust environment parameters based on tissue type
        switch (envType)
        {
            case EnvironmentType.Nervous:
                temperature += 0.5f; // Slightly warmer
                oxygen += 0.1f;      // More oxygen for brain function
                nutrients += 0.1f;    // Higher nutrients
                break;

            case EnvironmentType.Digestive:
                pH -= 0.8f;          // More acidic
                nutrients += 0.3f;    // Much higher nutrients
                break;

            case EnvironmentType.Muscle:
                oxygen += 0.05f;      // Slight oxygen increase
                nutrients -= 0.1f;    // Lower nutrients
                break;

            case EnvironmentType.Epithelial:
                temperature -= 0.3f;  // Slightly cooler (exposed)
                oxygen += 0.2f;       // More oxygen (near air)
                break;

            case EnvironmentType.Air:
                temperature -= 1.0f;  // Much cooler
                oxygen += 0.5f;       // Much more oxygen
                nutrients -= 0.5f;    // Much fewer nutrients
                break;
        }
    }

    private void SimulateDiffusion(float deltaTime)
    {
        // Run diffusion job for environment parameters
        var diffusionJob = new EnvironmentDiffusionJob
        {
            positions = regionPositions,
            temperatures = regionTemperatures,
            oxygen = regionOxygen,
            nutrients = regionNutrients,
            deltaTime = deltaTime,
            diffusionRate = 5f
        };

        JobHandle jobHandle = diffusionJob.Schedule();
        jobHandle.Complete();

        // Update region data from job
        for (int i = 0; i < environmentRegionCount; i++)
        {
            EnvironmentData region = environmentRegions[i];
            region.temperature = regionTemperatures[i];
            region.oxygen = regionOxygen[i];
            region.nutrients = regionNutrients[i];
            environmentRegions[i] = region;
        }
    }

    private void UpdateEnvironmentRegions()
    {
        // Update chemical concentrations in each region
        for (int i = 0; i < environmentRegions.Count; i++)
        {
            EnvironmentData region = environmentRegions[i];

            // Update signal concentrations based on cell activities
            for (int signalIndex = 0; signalIndex < region.signalConcentrations.Length; signalIndex++)
            {
                // Get current concentration
                float concentration = region.signalConcentrations[signalIndex];

                // Decay over time
                concentration *= 0.98f; // 2% decay per update

                // Update concentration
                region.signalConcentrations[signalIndex] = concentration;
            }

            // Update waste products
            region.wasteProducts *= 0.99f; // Natural breakdown

            // Update the region
            environmentRegions[i] = region;
        }
    }

    private void ApplyEnvironmentToCells()
    {
        // For each cell, find nearest environment region and apply effects
        for (int i = 0; i < mainSimulator.cellData.Length; i++)
        {
            CellData cell = mainSimulator.cellData[i];

            // Find nearest environment region
            EnvironmentData nearestEnv = FindNearestEnvironmentRegion(cell.position);

            // Apply environment effects
            ApplyEnvironmentToCell(ref cell, nearestEnv);

            // Update cell
            mainSimulator.cellData[i] = cell;
        }
    }

    private EnvironmentData FindNearestEnvironmentRegion(float3 position)
    {
        float minDistance = float.MaxValue;
        int nearestIndex = 0;

        for (int i = 0; i < environmentRegions.Count; i++)
        {
            float distance = math.distance(position, environmentRegions[i].position);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        return environmentRegions[nearestIndex];
    }

    private void ApplyEnvironmentToCell(ref CellData cell, EnvironmentData env)
    {
        // Temperature effects
        if (env.temperature > baseTemperature + 5f)
        {
            // Too hot - stress the cell
            cell.energy -= 0.01f;
            if (cell.state == CellState.Alive)
            {
                cell.state = CellState.Stressed;
            }
        }
        else if (env.temperature < baseTemperature - 5f)
        {
            // Too cold - slow metabolism
            cell.velocity *= 0.8f;
        }

        // Nutrient effects
        if (env.nutrients > 0.5f)
        {
            // Abundant nutrients - cells can gain energy
            cell.energy = math.min(1.0f, cell.energy + 0.005f);
        }
        else if (env.nutrients < 0.2f)
        {
            // Scarce nutrients - cells lose energy
            cell.energy -= 0.01f;
        }

        // Oxygen effects
        if (env.oxygen < 0.3f)
        {
            // Low oxygen - stress and energy loss
            cell.energy -= 0.02f;
            if (cell.state == CellState.Alive)
            {
                cell.state = CellState.Stressed;
            }
        }

        // Chemical effects from gradients
        foreach (var gradient in chemicalGradients)
        {
            float concentration = gradient.Value.GetConcentrationAt(cell.position);

            // Apply chemical-specific effects
            ApplyChemicalEffect(ref cell, gradient.Key, concentration);
        }
    }

    private void ApplyChemicalEffect(ref CellData cell, string chemical, float concentration)
    {
        // Skip if minimal concentration
        if (concentration < 0.1f)
            return;

        switch (chemical)
        {
            case "ecdysone": // Growth hormone
                // Increases energy and promotes division
                if (cell.energy < 0.9f)
                {
                    cell.energy += concentration * 0.01f;
                }

                // Cell type specific responses
                if (cell.type == CellType.Epithelial)
                {
                    // Epithelial cells are very responsive to ecdysone during development
                    cell.signalStrength = math.max(cell.signalStrength, concentration);
                }
                break;

            case "insulin":
                // Affects metabolism
                if (cell.type == CellType.FatBody)
                {
                    // Fat body cells store energy in response to insulin
                    cell.energy += concentration * 0.02f;
                }
                else
                {
                    // Other cells use energy faster with insulin
                    cell.energy -= concentration * 0.005f;
                }
                break;

            case "juvenile_hormone":
                // Maintains larval state, inhibits differentiation
                if (cell.type == CellType.Epithelial || cell.type == CellType.Muscle)
                {
                    // These cells remain less specialized with JH
                    cell.velocity *= (1.0f + (concentration * 0.2f)); // More active
                }
                break;

            case "octopamine": // Neurotransmitter
                if (cell.type == CellType.Neuron)
                {
                    // Neurons respond strongly
                    cell.signalStrength = math.max(cell.signalStrength, concentration * 1.5f);
                }
                else if (cell.type == CellType.Muscle)
                {
                    // Muscles contract with octopamine
                    float contraction = concentration * 2.0f;
                    cell.velocity = new float3(
                        UnityEngine.Random.Range(-contraction, contraction),
                        UnityEngine.Random.Range(-contraction, contraction),
                        UnityEngine.Random.Range(-contraction, contraction)
                    );
                }
                break;

            case "serotonin":
                if (cell.type == CellType.Neuron)
                {
                    // Neurons respond
                    cell.signalStrength = math.max(cell.signalStrength, concentration);
                }
                break;
        }
    }

    // Method to add a new chemical source (can be called by other systems)
    public void AddChemicalSource(string chemical, float3 position, float strength, float radius)
    {
        if (chemicalGradients.ContainsKey(chemical))
        {
            chemicalGradients[chemical].AddSource(position, strength, radius);
        }
    }

    void OnDestroy()
    {
        // Clean up native arrays
        if (regionPositions.IsCreated) regionPositions.Dispose();
        if (regionTemperatures.IsCreated) regionTemperatures.Dispose();
        if (regionOxygen.IsCreated) regionOxygen.Dispose();
        if (regionNutrients.IsCreated) regionNutrients.Dispose();

        foreach (var region in environmentRegions)
        {
            if (region.signalConcentrations.IsCreated)
                region.signalConcentrations.Dispose();
        }
    }
}

// Job for simulating diffusion of environment parameters
[Unity.Burst.BurstCompile]
public struct EnvironmentDiffusionJob : IJob
{
    [ReadOnly] public NativeArray<float3> positions;
    public NativeArray<float> temperatures;
    public NativeArray<float> oxygen;
    public NativeArray<float> nutrients;

    public float deltaTime;
    public float diffusionRate;

    public void Execute()
    {
        int count = positions.Length;

        // Create temporary arrays for updated values
        NativeArray<float> newTemperatures = new NativeArray<float>(count, Allocator.Temp);
        NativeArray<float> newOxygen = new NativeArray<float>(count, Allocator.Temp);
        NativeArray<float> newNutrients = new NativeArray<float>(count, Allocator.Temp);

        // Copy current values
        for (int i = 0; i < count; i++)
        {
            newTemperatures[i] = temperatures[i];
            newOxygen[i] = oxygen[i];
            newNutrients[i] = nutrients[i];
        }

        // For each region, calculate diffusion with nearby regions
        for (int i = 0; i < count; i++)
        {
            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;

                float3 posI = positions[i];
                float3 posJ = positions[j];

                // Calculate distance
                float distance = math.distance(posI, posJ);

                // Only interact with nearby regions
                if (distance < 30f)
                {
                    // Calculate diffusion rate based on distance
                    float rate = diffusionRate * deltaTime * (1.0f - (distance / 30f));

                    // Temperature diffusion
                    float tempDiff = temperatures[j] - temperatures[i];
                    newTemperatures[i] += tempDiff * rate * 0.1f; // Temperature diffuses slowly

                    // Oxygen diffusion
                    float oxygenDiff = oxygen[j] - oxygen[i];
                    newOxygen[i] += oxygenDiff * rate * 0.5f; // Oxygen diffuses faster

                    // Nutrient diffusion
                    float nutrientDiff = nutrients[j] - nutrients[i];
                    newNutrients[i] += nutrientDiff * rate * 0.3f; // Medium diffusion rate
                }
            }
        }

        // Update actual values
        for (int i = 0; i < count; i++)
        {
            temperatures[i] = newTemperatures[i];
            oxygen[i] = newOxygen[i];
            nutrients[i] = newNutrients[i];

            // Ensure values stay in valid ranges
            temperatures[i] = math.clamp(temperatures[i], 15f, 35f);
            oxygen[i] = math.clamp(oxygen[i], 0.1f, 1.0f);
            nutrients[i] = math.clamp(nutrients[i], 0.1f, 1.0f);
        }

        // Clean up
        newTemperatures.Dispose();
        newOxygen.Dispose();
        newNutrients.Dispose();
    }
}