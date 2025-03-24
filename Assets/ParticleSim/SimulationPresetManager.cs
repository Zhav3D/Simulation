using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class SimulationPreset
{
    public string name;
    public float simulationSpeed;
    public float collisionElasticity;
    public Vector3 simulationBounds;
    public float dampening;
    public float interactionStrength;
    public float minDistance;
    public float bounceForce;
    public float maxForce;
    public float maxVelocity;
    public float interactionRadius;
    public bool useGridPartitioning;
    public bool useJobSystem;
    public float cellSize;

    // Pattern Generator settings
    public InteractionMatrixGenerator.PatternType patternType;
    public bool generateParticleTypes;
    public bool applyRecommendedSettings;
    public float particleSpawnMultiplier;
    public float particleRadiusMultiplier;
    public float attractionBias;
    public float symmetryFactor;
    public float sparsity;
    public float noiseFactor;

    // Particle Types and Interaction Rules
    public List<ParticleTypeData> particleTypes = new List<ParticleTypeData>();
    public List<InteractionRuleData> interactionRules = new List<InteractionRuleData>();

    [System.Serializable]
    public class ParticleTypeData
    {
        public string name;
        public Color color;
        public float mass;
        public float radius;
        public float spawnAmount;
    }

    [System.Serializable]
    public class InteractionRuleData
    {
        public int typeIndexA;
        public int typeIndexB;
        public float attractionValue;
    }
}

public static class SimulationPresetManager
{
    private static string PresetDirectory => Path.Combine(Application.persistentDataPath, "Presets");

    // Create a preset from current simulation settings
    public static SimulationPreset CreatePreset(string name, OptimizedParticleSimulation simulation, InteractionMatrixGenerator matrixGenerator)
    {
        SimulationPreset preset = new SimulationPreset
        {
            name = name,
            simulationSpeed = simulation.simulationSpeed,
            collisionElasticity = simulation.collisionElasticity,
            simulationBounds = simulation.simulationBounds,
            dampening = simulation.dampening,
            interactionStrength = simulation.interactionStrength,
            minDistance = simulation.minDistance,
            bounceForce = simulation.bounceForce,
            maxForce = simulation.maxForce,
            maxVelocity = simulation.maxVelocity,
            interactionRadius = simulation.interactionRadius,
            useGridPartitioning = simulation.useGridPartitioning,
            useJobSystem = simulation.useJobSystem,
            cellSize = simulation.cellSize,

            // Pattern generator settings
            patternType = matrixGenerator.patternType,
            generateParticleTypes = matrixGenerator.generateParticleTypes,
            applyRecommendedSettings = matrixGenerator.applyRecommendedSettings,
            particleSpawnMultiplier = matrixGenerator.particleSpawnMultiplier,
            particleRadiusMultiplier = matrixGenerator.particleRadiusMultiplier,
            attractionBias = matrixGenerator.attractionBias,
            symmetryFactor = matrixGenerator.symmetryFactor,
            sparsity = matrixGenerator.sparsity,
            noiseFactor = matrixGenerator.noiseFactor
        };

        // Save particle types
        foreach (var particleType in simulation.particleTypes)
        {
            preset.particleTypes.Add(new SimulationPreset.ParticleTypeData
            {
                name = particleType.name,
                color = particleType.color,
                mass = particleType.mass,
                radius = particleType.radius,
                spawnAmount = particleType.spawnAmount
            });
        }

        // Save interaction rules
        foreach (var rule in simulation.interactionRules)
        {
            preset.interactionRules.Add(new SimulationPreset.InteractionRuleData
            {
                typeIndexA = rule.typeIndexA,
                typeIndexB = rule.typeIndexB,
                attractionValue = rule.attractionValue
            });
        }

        return preset;
    }

    // Apply a preset to the simulation
    public static void ApplyPreset(SimulationPreset preset, OptimizedParticleSimulation simulation, InteractionMatrixGenerator matrixGenerator)
    {
        // Apply simulation settings
        simulation.simulationSpeed = preset.simulationSpeed;
        simulation.collisionElasticity = preset.collisionElasticity;
        simulation.simulationBounds = preset.simulationBounds;
        simulation.dampening = preset.dampening;
        simulation.interactionStrength = preset.interactionStrength;
        simulation.minDistance = preset.minDistance;
        simulation.bounceForce = preset.bounceForce;
        simulation.maxForce = preset.maxForce;
        simulation.maxVelocity = preset.maxVelocity;
        simulation.interactionRadius = preset.interactionRadius;
        simulation.useGridPartitioning = preset.useGridPartitioning;
        simulation.useJobSystem = preset.useJobSystem;
        simulation.cellSize = preset.cellSize;

        // Apply matrix generator settings
        matrixGenerator.patternType = preset.patternType;
        matrixGenerator.generateParticleTypes = preset.generateParticleTypes;
        matrixGenerator.applyRecommendedSettings = preset.applyRecommendedSettings;
        matrixGenerator.particleSpawnMultiplier = preset.particleSpawnMultiplier;
        matrixGenerator.particleRadiusMultiplier = preset.particleRadiusMultiplier;
        matrixGenerator.attractionBias = preset.attractionBias;
        matrixGenerator.symmetryFactor = preset.symmetryFactor;
        matrixGenerator.sparsity = preset.sparsity;
        matrixGenerator.noiseFactor = preset.noiseFactor;

        // Apply particle types
        simulation.particleTypes.Clear();
        foreach (var typeData in preset.particleTypes)
        {
            simulation.particleTypes.Add(new OptimizedParticleSimulation.ParticleType
            {
                name = typeData.name,
                color = typeData.color,
                mass = typeData.mass,
                radius = typeData.radius,
                spawnAmount = typeData.spawnAmount
            });
        }

        // Apply interaction rules
        simulation.interactionRules.Clear();
        foreach (var ruleData in preset.interactionRules)
        {
            simulation.interactionRules.Add(new OptimizedParticleSimulation.InteractionRule
            {
                typeIndexA = ruleData.typeIndexA,
                typeIndexB = ruleData.typeIndexB,
                attractionValue = ruleData.attractionValue
            });
        }

        // Rebuild interaction lookup table
        simulation.RebuildInteractionLookup();
    }

    // Save preset to file
    public static void SavePreset(SimulationPreset preset)
    {
        // Create preset directory if it doesn't exist
        if (!Directory.Exists(PresetDirectory))
        {
            Directory.CreateDirectory(PresetDirectory);
        }

        string filePath = Path.Combine(PresetDirectory, preset.name + ".json");
        string jsonData = JsonUtility.ToJson(preset, true);
        File.WriteAllText(filePath, jsonData);

        Debug.Log($"Saved preset: {preset.name} to {filePath}");
    }

    // Load preset from file
    public static SimulationPreset LoadPreset(string presetName)
    {
        string filePath = Path.Combine(PresetDirectory, presetName + ".json");

        if (File.Exists(filePath))
        {
            string jsonData = File.ReadAllText(filePath);
            return JsonUtility.FromJson<SimulationPreset>(jsonData);
        }

        Debug.LogWarning($"Preset not found: {presetName}");
        return null;
    }

    // Get list of available presets
    public static List<string> GetAvailablePresets()
    {
        List<string> presets = new List<string>();

        if (Directory.Exists(PresetDirectory))
        {
            var files = Directory.GetFiles(PresetDirectory, "*.json");
            presets = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        }

        return presets;
    }

    // Create default presets
    public static void CreateDefaultPresets()
    {
        if (!Directory.Exists(PresetDirectory))
        {
            Directory.CreateDirectory(PresetDirectory);
        }

        // Only create defaults if no presets exist
        if (GetAvailablePresets().Count == 0)
        {
            // Create a few sample presets
            CreateFlockingPreset();
            CreateCrystalPreset();
            CreatePredatorPreyPreset();
        }
    }

    private static void CreateFlockingPreset()
    {
        SimulationPreset preset = new SimulationPreset
        {
            name = "Flocking Birds",
            simulationSpeed = 1.0f,
            collisionElasticity = 0.3f,
            simulationBounds = new Vector3(20f, 20f, 20f),
            dampening = 0.98f,
            interactionStrength = 1.2f,
            minDistance = 0.5f,
            bounceForce = 0.9f,
            maxForce = 100f,
            maxVelocity = 10f,
            interactionRadius = 8f,
            useGridPartitioning = true,
            useJobSystem = true,
            cellSize = 2.0f,

            patternType = InteractionMatrixGenerator.PatternType.Flocking,
            generateParticleTypes = true,
            applyRecommendedSettings = true,
            particleSpawnMultiplier = 1.5f,
            particleRadiusMultiplier = 0.8f,
            attractionBias = 0.2f,
            symmetryFactor = 0.7f,
            sparsity = 0.3f,
            noiseFactor = 0.1f
        };

        SavePreset(preset);
    }

    private static void CreateCrystalPreset()
    {
        SimulationPreset preset = new SimulationPreset
        {
            name = "Crystal Growth",
            simulationSpeed = 0.7f,
            collisionElasticity = 0.1f,
            simulationBounds = new Vector3(15f, 15f, 15f),
            dampening = 0.92f,
            interactionStrength = 2.0f,
            minDistance = 0.6f,
            bounceForce = 0.5f,
            maxForce = 50f,
            maxVelocity = 5f,
            interactionRadius = 6f,
            useGridPartitioning = true,
            useJobSystem = true,
            cellSize = 1.5f,

            patternType = InteractionMatrixGenerator.PatternType.Crystalline,
            generateParticleTypes = true,
            applyRecommendedSettings = true,
            particleSpawnMultiplier = 1.2f,
            particleRadiusMultiplier = 1.0f,
            attractionBias = 0.0f,
            symmetryFactor = 0.9f,
            sparsity = 0.1f,
            noiseFactor = 0.05f
        };

        SavePreset(preset);
    }

    private static void CreatePredatorPreyPreset()
    {
        SimulationPreset preset = new SimulationPreset
        {
            name = "Prey and Predators",
            simulationSpeed = 1.5f,
            collisionElasticity = 0.8f,
            simulationBounds = new Vector3(25f, 25f, 25f),
            dampening = 0.9f,
            interactionStrength = 1.8f,
            minDistance = 0.4f,
            bounceForce = 0.9f,
            maxForce = 120f,
            maxVelocity = 15f,
            interactionRadius = 10f,
            useGridPartitioning = true,
            useJobSystem = true,
            cellSize = 2.5f,

            patternType = InteractionMatrixGenerator.PatternType.PredatorPrey,
            generateParticleTypes = true,
            applyRecommendedSettings = true,
            particleSpawnMultiplier = 2.0f,
            particleRadiusMultiplier = 1.2f,
            attractionBias = -0.2f,
            symmetryFactor = 0.1f,
            sparsity = 0.2f,
            noiseFactor = 0.2f
        };

        SavePreset(preset);
    }
}