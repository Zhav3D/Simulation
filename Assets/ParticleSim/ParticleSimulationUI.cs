using UnityEngine;
using RosettaUI;
using System.Collections.Generic;

[RequireComponent(typeof(OptimizedParticleSimulation), typeof(InteractionMatrixGenerator))]
public class ParticleSimulationUI : MonoBehaviour
{
    private OptimizedParticleSimulation simulation;
    private InteractionMatrixGenerator matrixGenerator;
    private RosettaUIRoot uiRoot;

    // Debug values for display
    private string particleCountText = "0";
    private string fpsText = "0";
    private string deltaTimeText = "0";

    void Start()
    {
        // Get components
        simulation = GetComponent<OptimizedParticleSimulation>();
        matrixGenerator = GetComponent<InteractionMatrixGenerator>();

        // Find UI root in scene
        uiRoot = FindObjectOfType<RosettaUIRoot>();
        if (uiRoot == null)
        {
            Debug.LogError("No RosettaUIRoot found in scene. Please add the RosettaUIRoot prefab.");
            return;
        }

        // Build and set the UI
        uiRoot.Build(CreateUI());
    }

    void Update()
    {
        // Update text fields for performance stats
        UpdateDebugTexts();
    }

    void UpdateDebugTexts()
    {
        particleCountText = simulation.GetParticleCount().ToString();
        fpsText = Mathf.Round(1.0f / Time.smoothDeltaTime).ToString();
        deltaTimeText = Time.deltaTime.ToString("F4");
    }

    Element CreateUI()
    {
        return UI.Window("Particle Simulation Controls", CreateSimulationUI());
    }

    Element CreateSimulationUI()
    {
        return UI.Page(
            UI.Box(UI.Label("Simulation Settings")),

            UI.Space(),
            UI.Row(
                UI.Label("Simulation Speed:"),
                UI.Slider(() => simulation.simulationSpeed, v => simulation.simulationSpeed = v, 0f, 5f)
            ),

            UI.Row(
                UI.Label("Collision Elasticity:"),
                UI.Slider(() => simulation.collisionElasticity, v => simulation.collisionElasticity = v, 0f, 1f)
            ),

            UI.Row(
                UI.Label("Simulation Bounds:"),
                UI.Slider(() => simulation.simulationBounds.x, v => simulation.simulationBounds = new Vector3(v, v, v), 3f, 2000f)
            ),

            UI.Space(),
            UI.Fold("Physics Parameters",
                UI.Row(
                    UI.Label("Dampening:"),
                    UI.Slider(() => simulation.dampening, v => simulation.dampening = v, 0.5f, 1f)
                ),
                UI.Row(
                    UI.Label("Interaction Strength:"),
                    UI.Slider(() => simulation.interactionStrength, v => simulation.interactionStrength = v, 0f, 5f)
                ),
                UI.Row(
                    UI.Label("Min Distance:"),
                    UI.Slider(() => simulation.minDistance, v => simulation.minDistance = v, 0.01f, 5f)
                ),
                UI.Row(
                    UI.Label("Bounce Force:"),
                    UI.Slider(() => simulation.bounceForce, v => simulation.bounceForce = v, 0f, 1f)
                ),
                UI.Row(
                    UI.Label("Max Force:"),
                    UI.Slider(() => simulation.maxForce, v => simulation.maxForce = v, 1f, 1000f)
                ),
                UI.Row(
                    UI.Label("Max Velocity:"),
                    UI.Slider(() => simulation.maxVelocity, v => simulation.maxVelocity = v, 1f, 100f)
                ),
                UI.Row(
                    UI.Label("Interaction Radius:"),
                    UI.Slider(() => simulation.interactionRadius, v => simulation.interactionRadius = v, 1f, 50f)
                )
            ),

            UI.Space(),
            UI.Fold("Optimization Settings",
                UI.Row(
                    UI.Label("Cell Size:"),
                    UI.Slider(() => simulation.cellSize, v => simulation.cellSize = v, 0.1f, 5f)
                ),
                UI.Row(
                    UI.Label("Use Grid Partitioning:"),
                    UI.Toggle(() => simulation.useGridPartitioning, v => simulation.useGridPartitioning = v)
                ),
                UI.Row(
                    UI.Label("Use Job System:"),
                    UI.Toggle(() => simulation.useJobSystem, v => simulation.useJobSystem = v)
                )
            ),

            UI.Space(),
            UI.Row(
                UI.Button("Reset Simulation", ResetSimulation),
                UI.Button("Restart Simulation", RestartSimulation)
            ),

            UI.Space(),
            UI.Label("Particle Types"),
            UI.Button("Add New Particle Type", AddNewParticleType),

            UI.Space(),
            CreateParticleTypesList(),

            UI.Space(),
            UI.Button("Apply Particle Changes", ApplyParticleChanges),

            UI.Space(),
            UI.Label("Interaction Rules"),
            UI.Button("Add New Rule", AddNewInteractionRule),

            UI.Space(),
            CreateInteractionRulesList(),

            UI.Space(),
            UI.Button("Apply Rule Changes", ApplyRuleChanges),

            UI.Space(),
            UI.Label("Matrix Generator"),
            UI.Row(
                UI.Label("Pattern Type:"),
                UI.Field(() => (int)matrixGenerator.patternType, v => matrixGenerator.patternType = (InteractionMatrixGenerator.PatternType)v)
            ),

            UI.Space(),
            UI.Fold("Generator Options",
                UI.Row(
                    UI.Label("Generate On Awake:"),
                    UI.Toggle(() => matrixGenerator.generateOnAwake, v => matrixGenerator.generateOnAwake = v)
                ),
                UI.Row(
                    UI.Label("Generate Particle Types:"),
                    UI.Toggle(() => matrixGenerator.generateParticleTypes, v => matrixGenerator.generateParticleTypes = v)
                ),
                UI.Row(
                    UI.Label("Apply Recommended Settings:"),
                    UI.Toggle(() => matrixGenerator.applyRecommendedSettings, v => matrixGenerator.applyRecommendedSettings = v)
                ),

                UI.Space(),
                UI.Label("Particle Scaling:"),
                UI.Row(
                    UI.Label("Spawn Multiplier:"),
                    UI.Slider(() => matrixGenerator.particleSpawnMultiplier, v => matrixGenerator.particleSpawnMultiplier = v, 0.1f, 100f)
                ),
                UI.Row(
                    UI.Label("Radius Multiplier:"),
                    UI.Slider(() => matrixGenerator.particleRadiusMultiplier, v => matrixGenerator.particleRadiusMultiplier = v, 0.1f, 3f)
                ),

                UI.Space(),
                UI.Label("Matrix Configuration:"),
                UI.Row(
                    UI.Label("Attraction Bias:"),
                    UI.Slider(() => matrixGenerator.attractionBias, v => matrixGenerator.attractionBias = v, -1f, 1f)
                ),
                UI.Row(
                    UI.Label("Symmetry Factor:"),
                    UI.Slider(() => matrixGenerator.symmetryFactor, v => matrixGenerator.symmetryFactor = v, 0f, 1f)
                ),
                UI.Row(
                    UI.Label("Sparsity:"),
                    UI.Slider(() => matrixGenerator.sparsity, v => matrixGenerator.sparsity = v, 0f, 1f)
                ),
                UI.Row(
                    UI.Label("Noise Factor:"),
                    UI.Slider(() => matrixGenerator.noiseFactor, v => matrixGenerator.noiseFactor = v, 0f, 1f)
                )
            ),

            UI.Space(),
            UI.Label("Matrix Visualization:"),
            UI.Box(
                UI.Label("(Generate matrix to see visualization)")
            ),

            UI.Space(),
            UI.Button("Generate Matrix", GenerateMatrix),

            UI.Space(),
            UI.Label("Debug Information"),
            UI.Row(
                UI.Label("Particle Count:"),
                UI.Label(particleCountText)
            ),

            UI.Space(),
            UI.Label("Performance:"),
            UI.Row(
                UI.Label("FPS:"),
                UI.Label(fpsText)
            ),
            UI.Row(
                UI.Label("Delta Time:"),
                UI.Label(deltaTimeText)
            ),

            UI.Space(),
            UI.Button("Log Debug Info", LogDebugInfo)
        );
    }

    Element CreateParticleTypesList()
    {
        var elements = new List<Element>();

        for (int i = 0; i < simulation.particleTypes.Count; i++)
        {
            var index = i; // Capture index for lambda
            var type = simulation.particleTypes[i];

            elements.Add(
                UI.Fold($"Type {i}: {type.name}",
                    UI.Row(
                        UI.Label("Name:"),
                        UI.Field(() => type.name, v => type.name = v)
                    ),
                    UI.Row(
                        UI.Label("Color:"),
                        UI.Field(() => type.color, v => type.color = v)
                    ),
                    UI.Row(
                        UI.Label("Mass:"),
                        UI.Slider(() => type.mass, v => type.mass = v, 0.01f, 1000f)
                    ),
                    UI.Row(
                        UI.Label("Radius:"),
                        UI.Slider(() => type.radius, v => type.radius = v, 0.05f, 50f)
                    ),
                    UI.Row(
                        UI.Label("Spawn Amount:"),
                        UI.Slider(() => type.spawnAmount, v => type.spawnAmount = v, 1f, 100000f)
                    ),
                    UI.Space(),
                    UI.Row(
                        UI.Button("Remove", () => RemoveParticleType(index))
                    )
                )
            );
        }

        return UI.Page(elements.ToArray());
    }

    Element CreateInteractionRulesList()
    {
        var elements = new List<Element>();

        for (int i = 0; i < simulation.interactionRules.Count; i++)
        {
            var index = i; // Capture index for lambda
            var rule = simulation.interactionRules[i];

            elements.Add(
                UI.Box(
                    UI.Row(
                        UI.Label($"Rule {i}:"),
                        UI.Space(),
                        // Simple field for type A
                        UI.Field(() => rule.typeIndexA, v => rule.typeIndexA = v),
                        UI.Space(),
                        UI.Label(rule.attractionValue >= 0 ? "attracts" : "repels"),
                        UI.Space(),
                        // Simple field for type B
                        UI.Field(() => rule.typeIndexB, v => rule.typeIndexB = v)
                    ),
                    UI.Row(
                        UI.Label("Attraction Value:"),
                        UI.Slider(() => rule.attractionValue, v => rule.attractionValue = v, -1f, 1f)
                    ),
                    UI.Row(
                        UI.Button("Remove", () => RemoveInteractionRule(index))
                    )
                )
            );
        }

        return UI.Page(elements.ToArray());
    }

    void AddNewParticleType()
    {
        var newType = new OptimizedParticleSimulation.ParticleType
        {
            name = $"Type {simulation.particleTypes.Count}",
            color = Random.ColorHSV(),
            mass = 1.0f,
            radius = 0.5f,
            spawnAmount = 50f
        };

        simulation.particleTypes.Add(newType);
        RefreshUI();
    }

    void RemoveParticleType(int index)
    {
        if (index >= 0 && index < simulation.particleTypes.Count)
        {
            simulation.particleTypes.RemoveAt(index);
            RefreshUI();
        }
    }

    void AddNewInteractionRule()
    {
        if (simulation.particleTypes.Count < 2)
        {
            Debug.LogWarning("Need at least 2 particle types to create a rule");
            return;
        }

        var newRule = new OptimizedParticleSimulation.InteractionRule
        {
            typeIndexA = 0,
            typeIndexB = simulation.particleTypes.Count > 1 ? 1 : 0,
            attractionValue = Random.Range(-1f, 1f)
        };

        simulation.interactionRules.Add(newRule);
        RefreshUI();
    }

    void RemoveInteractionRule(int index)
    {
        if (index >= 0 && index < simulation.interactionRules.Count)
        {
            simulation.interactionRules.RemoveAt(index);
            RefreshUI();
        }
    }

    // Rebuild the entire UI when we need to update dynamic elements
    void RefreshUI()
    {
        if (uiRoot != null)
        {
            uiRoot.Build(CreateUI());
        }
    }

    void GenerateMatrix()
    {
        matrixGenerator.GenerateMatrix();
        RefreshUI();
    }

    void ResetSimulation()
    {
        // This would reset parameters to defaults
        Debug.Log("Reset Simulation (not implemented)");
    }

    void RestartSimulation()
    {
        // This would restart the simulation with current parameters
        Debug.Log("Restart Simulation requested");
        // We could implement this by destroying all particles and respawning them
        // For now, just a placeholder
    }

    void ApplyParticleChanges()
    {
        Debug.Log("Applying particle type changes");
        // This would typically require rebuilding the simulation
        // For now, just a placeholder
    }

    void ApplyRuleChanges()
    {
        Debug.Log("Applying interaction rule changes");
        // This would typically require rebuilding the interaction lookup table
        // For now, just a placeholder
    }

    void LogDebugInfo()
    {
        Debug.Log($"=== Particle Simulation Debug Info ===");
        Debug.Log($"Particle Count: {simulation.GetParticleCount()}");
        Debug.Log($"Particle Types: {simulation.particleTypes.Count}");
        Debug.Log($"Interaction Rules: {simulation.interactionRules.Count}");
        Debug.Log($"Simulation Speed: {simulation.simulationSpeed}");
        Debug.Log($"Using Grid Partitioning: {simulation.useGridPartitioning}");
        Debug.Log($"Using Job System: {simulation.useJobSystem}");
    }
}