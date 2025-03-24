using UnityEngine;
using RosettaUI;
using System.Collections.Generic;

[RequireComponent(typeof(OptimizedParticleSimulation), typeof(InteractionMatrixGenerator))]
public class ParticleSimulationUI : MonoBehaviour
{
    private OptimizedParticleSimulation simulation;
    private InteractionMatrixGenerator matrixGenerator;
    private RosettaUIRoot uiRoot;

    // Window visibility states
    private bool showMainWindow = true;
    private bool showParticleTypesWindow = false;
    private bool showInteractionRulesWindow = false;
    private bool showPatternGeneratorWindow = false;
    private bool showDebugWindow = false;
    private bool showMatrixVisualizerWindow = false;

    // Reference to the matrix visualizer
    private InteractionMatrixVisualizer matrixVisualizer;

    // Debug values for display
    private string particleCountText = "0";
    private string fpsText = "0";
    private string deltaTimeText = "0";

    // Save/load states 
    private string savePresetName = "NewPreset";
    private List<string> availablePresets = new List<string>();

    void Start()
    {
        // Get components
        simulation = GetComponent<OptimizedParticleSimulation>();
        matrixGenerator = GetComponent<InteractionMatrixGenerator>();
        matrixVisualizer = FindObjectOfType<InteractionMatrixVisualizer>();

        // Find UI root in scene
        uiRoot = FindObjectOfType<RosettaUIRoot>();
        if (uiRoot == null)
        {
            Debug.LogError("No RosettaUIRoot found in scene. Please add the RosettaUIRoot prefab.");
            return;
        }

        // Create default presets if none exist
        SimulationPresetManager.CreateDefaultPresets();

        // Load available presets
        LoadAvailablePresets();

        // Build and set the UI
        BuildUI();
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

    void BuildUI()
    {
        if (uiRoot != null)
        {
            uiRoot.Build(CreateUI());
        }
    }

    Element CreateUI()
    {
        return UI.Row(
            // Main controls panel (always visible)
            CreateMainControlsWindow(),

            // Optional windows
            showParticleTypesWindow ? CreateParticleTypesWindow() : null,
            showInteractionRulesWindow ? CreateInteractionRulesWindow() : null,
            showPatternGeneratorWindow ? CreatePatternGeneratorWindow() : null,
            showDebugWindow ? CreateDebugWindow() : null,
            showMatrixVisualizerWindow ? CreateMatrixVisualizerWindow() : null
        );
    }

    #region Main Controls Window

    Element CreateMainControlsWindow()
    {
        return UI.Window("Particle Simulation",
            UI.Page(
                // Quick Controls
                UI.Box(
                    UI.Label("Simulation Controls"),
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
                    )
                ),

                // Simulation Actions
                UI.Space(),
                UI.Box(
                    UI.Label("Actions"),
                    UI.Space(),
                    UI.Row(
                        UI.Button("Reset", ResetSimulation),
                        UI.Button("Restart", RestartSimulation),
                        UI.Button("Pause/Resume", ToggleSimulation)
                    ),
                    UI.Space(),
                    UI.Row(
                        UI.Button("Save Preset", SavePreset),
                        UI.Field(() => savePresetName, v => savePresetName = v)
                    ),
                    UI.Space(),
                    UI.Label("Load Preset:"),
                    CreatePresetSelector()
                ),

                // Window toggles
                UI.Space(),
                UI.Box(
                    UI.Label("Open Windows"),
                    UI.Space(),
                    UI.Row(
                        UI.Button("Particle Types", ToggleParticleTypesWindow),
                        UI.Button("Interaction Rules", ToggleInteractionRulesWindow)
                    ),
                    UI.Row(
                        UI.Button("Pattern Generator", TogglePatternGeneratorWindow),
                        UI.Button("Debug Info", ToggleDebugWindow)
                    ),
                    UI.Row(
                        UI.Button("Matrix Visualizer", ToggleMatrixVisualizerWindow)
                    )
                ),

                // Basic stats
                UI.Space(),
                UI.Box(
                    UI.Label("Statistics"),
                    UI.Row(
                        UI.Label("Particles:"),
                        UI.Label(particleCountText)
                    ),
                    UI.Row(
                        UI.Label("FPS:"),
                        UI.Label(fpsText)
                    )
                )
            )
        );
    }

    Element CreatePresetSelector()
    {
        // Create buttons for presets
        var elements = new List<Element>();

        foreach (var preset in availablePresets)
        {
            // Local variable capture for closure
            string presetName = preset;
            elements.Add(UI.Button(presetName, () => LoadPreset(presetName)));
        }

        return UI.Fold("Available Presets", elements.ToArray());
    }

    void ToggleParticleTypesWindow() => showParticleTypesWindow = !showParticleTypesWindow;
    void ToggleInteractionRulesWindow() => showInteractionRulesWindow = !showInteractionRulesWindow;
    void TogglePatternGeneratorWindow() => showPatternGeneratorWindow = !showPatternGeneratorWindow;
    void ToggleDebugWindow() => showDebugWindow = !showDebugWindow;
    void ToggleMatrixVisualizerWindow() => showMatrixVisualizerWindow = !showMatrixVisualizerWindow;

    void ToggleSimulation()
    {
        // Implement pause/resume functionality
        Time.timeScale = Time.timeScale > 0 ? 0 : simulation.simulationSpeed;
    }

    #endregion

    #region Particle Types Window

    Element CreateParticleTypesWindow()
    {
        return UI.Window("Particle Types",
            UI.Page(
                UI.Row(
                    UI.Label("Particle Types"),
                    UI.Button("Add New Type", AddNewParticleType)
                ),
                UI.Space(),
                CreateParticleTypesList(),
                UI.Space(),
                UI.Row(
                    UI.Button("Apply Changes", ApplyParticleChanges),
                    UI.Button("Close", ToggleParticleTypesWindow)
                )
            )
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
                        UI.Button("Duplicate", () => DuplicateParticleType(index)),
                        UI.Button("Remove", () => RemoveParticleType(index))
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
        BuildUI();
    }

    void DuplicateParticleType(int index)
    {
        if (index >= 0 && index < simulation.particleTypes.Count)
        {
            var original = simulation.particleTypes[index];
            var duplicate = new OptimizedParticleSimulation.ParticleType
            {
                name = original.name + "_Copy",
                color = original.color,
                mass = original.mass,
                radius = original.radius,
                spawnAmount = original.spawnAmount
            };

            simulation.particleTypes.Add(duplicate);
            BuildUI();
        }
    }

    void RemoveParticleType(int index)
    {
        if (index >= 0 && index < simulation.particleTypes.Count)
        {
            simulation.particleTypes.RemoveAt(index);
            BuildUI();
        }
    }

    #endregion

    #region Interaction Rules Window

    Element CreateInteractionRulesWindow()
    {
        return UI.Window("Interaction Rules",
            UI.Page(
                UI.Row(
                    UI.Label("Interaction Rules"),
                    UI.Button("Add Rule", AddNewInteractionRule)
                ),
                UI.Space(),
                CreateInteractionRulesList(),
                UI.Space(),
                UI.Fold("Advanced Physics Settings",
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
                UI.Row(
                    UI.Button("Apply Changes", ApplyRuleChanges),
                    UI.Button("Close", ToggleInteractionRulesWindow)
                )
            )
        );
    }

    Element CreateInteractionRulesList()
    {
        var elements = new List<Element>();

        for (int i = 0; i < simulation.interactionRules.Count; i++)
        {
            var index = i; // Capture index for lambda
            var rule = simulation.interactionRules[i];

            string typeAName = (rule.typeIndexA >= 0 && rule.typeIndexA < simulation.particleTypes.Count) ?
                simulation.particleTypes[rule.typeIndexA].name : "Unknown";

            string typeBName = (rule.typeIndexB >= 0 && rule.typeIndexB < simulation.particleTypes.Count) ?
                simulation.particleTypes[rule.typeIndexB].name : "Unknown";

            elements.Add(
                UI.Box(
                    UI.Row(
                        UI.Label($"Rule {i}: {typeAName} → {typeBName}")
                    ),
                    UI.Row(
                        UI.Label("Type A:"),
                        CreateTypeSelector(rule.typeIndexA, (value) => rule.typeIndexA = value)
                    ),
                    UI.Row(
                        UI.Label("Type B:"),
                        CreateTypeSelector(rule.typeIndexB, (value) => rule.typeIndexB = value)
                    ),
                    UI.Row(
                        UI.Label("Attraction Value:"),
                        UI.Slider(() => rule.attractionValue, v => rule.attractionValue = v, -1f, 1f)
                    ),
                    UI.Row(
                        UI.Button("Invert", () => InvertRuleAttraction(index)),
                        UI.Button("Remove", () => RemoveInteractionRule(index))
                    )
                )
            );
        }

        return UI.Page(elements.ToArray());
    }

    Element CreateTypeSelector(int currentValue, System.Action<int> onChange)
    {
        var options = new List<Element>();

        for (int i = 0; i < simulation.particleTypes.Count; i++)
        {
            int index = i; // Capture for lambda
            options.Add(UI.Button(simulation.particleTypes[i].name, () => onChange(index)));
        }

        return UI.Fold($"Type: {(currentValue < simulation.particleTypes.Count ? simulation.particleTypes[currentValue].name : "Unknown")}", options.ToArray());
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
        BuildUI();
    }

    void InvertRuleAttraction(int index)
    {
        if (index >= 0 && index < simulation.interactionRules.Count)
        {
            simulation.interactionRules[index].attractionValue *= -1;
            BuildUI();
        }
    }

    void RemoveInteractionRule(int index)
    {
        if (index >= 0 && index < simulation.interactionRules.Count)
        {
            simulation.interactionRules.RemoveAt(index);
            BuildUI();
        }
    }

    #endregion

    #region Pattern Generator Window

    Element CreatePatternGeneratorWindow()
    {
        return UI.Window("Pattern Generator",
            UI.Page(
                UI.Box(
                    UI.Label("Pattern Type"),
                    UI.Space(),
                    CreatePatternTypeSelector(matrixGenerator.patternType, (value) => matrixGenerator.patternType = value)
                ),
                UI.Space(),
                UI.Box(
                    UI.Label("Generation Options"),
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
                    )
                ),
                UI.Space(),
                UI.Box(
                    UI.Label("Particle Scaling"),
                    UI.Row(
                        UI.Label("Spawn Multiplier:"),
                        UI.Slider(() => matrixGenerator.particleSpawnMultiplier, v => matrixGenerator.particleSpawnMultiplier = v, 0.1f, 100f)
                    ),
                    UI.Row(
                        UI.Label("Radius Multiplier:"),
                        UI.Slider(() => matrixGenerator.particleRadiusMultiplier, v => matrixGenerator.particleRadiusMultiplier = v, 0.1f, 3f)
                    )
                ),
                UI.Space(),
                UI.Box(
                    UI.Label("Matrix Configuration"),
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
                UI.Box(
                    UI.Label("Pattern Description"),
                    UI.Space(),
                    GetPatternDescription(matrixGenerator.patternType)
                ),
                UI.Space(),
                UI.Row(
                    UI.Button("Generate Matrix", GenerateMatrix),
                    UI.Button("Close", TogglePatternGeneratorWindow)
                )
            )
        );
    }

    Element CreatePatternTypeSelector(InteractionMatrixGenerator.PatternType currentType, System.Action<InteractionMatrixGenerator.PatternType> onChange)
    {
        var elements = new List<Element>();

        foreach (InteractionMatrixGenerator.PatternType patternType in System.Enum.GetValues(typeof(InteractionMatrixGenerator.PatternType)))
        {
            var type = patternType; // Capture for lambda
            elements.Add(UI.Button(patternType.ToString(), () => onChange(type)));
        }

        return UI.Fold($"Current: {currentType}", elements.ToArray());
    }

    Element GetPatternDescription(InteractionMatrixGenerator.PatternType patternType)
    {
        // Get description from the ParticlePatternPresets class
        string description = ParticlePatternPresets.GetPatternDescription(patternType);
        return UI.Label(description);
    }

    #endregion

    #region Matrix Visualizer Window

    Element CreateMatrixVisualizerWindow()
    {
        return UI.Window("Interaction Matrix Visualizer",
            UI.Page(
                UI.Box(
                    UI.Label("Interaction Matrix"),
                    UI.Space(),
                    UI.Label("This window provides a visual representation of the interaction matrix."),
                    UI.Label("Green cells indicate attraction, red cells indicate repulsion."),
                    UI.Label("The value shown is the attraction strength between particle types.")
                ),
                UI.Space(),
                UI.Row(
                    UI.Button("Refresh Visualization", RefreshMatrixVisualization),
                    UI.Button("Close", ToggleMatrixVisualizerWindow)
                )
            )
        );
    }

    void RefreshMatrixVisualization()
    {
        if (matrixVisualizer != null)
        {
            matrixVisualizer.Visualize();
        }
    }

    #endregion

    #region Debug Window

    Element CreateDebugWindow()
    {
        return UI.Window("Debug Information",
            UI.Page(
                UI.Box(
                    UI.Label("Performance Metrics"),
                    UI.Row(
                        UI.Label("Particle Count:"),
                        UI.Label(particleCountText)
                    ),
                    UI.Row(
                        UI.Label("FPS:"),
                        UI.Label(fpsText)
                    ),
                    UI.Row(
                        UI.Label("Delta Time:"),
                        UI.Label(deltaTimeText)
                    )
                ),
                UI.Space(),
                UI.Box(
                    UI.Label("Optimization Settings"),
                    UI.Row(
                        UI.Label("Use Grid Partitioning:"),
                        UI.Toggle(() => simulation.useGridPartitioning, v => simulation.useGridPartitioning = v)
                    ),
                    UI.Row(
                        UI.Label("Use Job System:"),
                        UI.Toggle(() => simulation.useJobSystem, v => simulation.useJobSystem = v)
                    ),
                    UI.Row(
                        UI.Label("Cell Size:"),
                        UI.Slider(() => simulation.cellSize, v => simulation.cellSize = v, 0.1f, 5f)
                    )
                ),
                UI.Space(),
                UI.Box(
                    UI.Label("Simulation Stats"),
                    UI.Label($"Particle Types: {simulation.particleTypes.Count}"),
                    UI.Label($"Interaction Rules: {simulation.interactionRules.Count}")
                ),
                UI.Space(),
                UI.Row(
                    UI.Button("Log Debug Info", LogDebugInfo),
                    UI.Button("Close", ToggleDebugWindow)
                )
            )
        );
    }

    #endregion

    #region Helper Functions

    void GenerateMatrix()
    {
        matrixGenerator.GenerateMatrix();

        // Update the matrix visualization if available
        if (matrixVisualizer != null)
        {
            matrixVisualizer.Visualize();
        }

        BuildUI();
    }

    void ResetSimulation()
    {
        // Reset parameters to defaults
        simulation.simulationSpeed = 1.0f;
        simulation.collisionElasticity = 0.5f;
        simulation.simulationBounds = new Vector3(10f, 10f, 10f);
        simulation.dampening = 0.95f;
        simulation.interactionStrength = 1f;
        simulation.minDistance = 0.5f;
        simulation.bounceForce = 0.8f;
        simulation.maxForce = 100f;
        simulation.maxVelocity = 20f;
        simulation.interactionRadius = 10f;

        BuildUI();
    }

    void RestartSimulation()
    {
        // This would restart the simulation with current parameters
        Debug.Log("Restarting simulation with current parameters");

        // In a real implementation, you would:
        // 1. Destroy all existing particles
        // 2. Re-initialize the simulation structures
        // 3. Spawn new particles

        // For now, just update the UI
        BuildUI();
    }

    void ApplyParticleChanges()
    {
        Debug.Log("Applying particle type changes");
        // This would require rebuilding parts of the simulation
        // For a complete implementation, you might need to:
        // 1. Update particle visuals (colors, sizes)
        // 2. Possibly adjust particle counts

        BuildUI();
    }

    void ApplyRuleChanges()
    {
        Debug.Log("Applying interaction rule changes");

        // Rebuild the interaction lookup table
        simulation.RebuildInteractionLookup();

        BuildUI();
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

    void SavePreset()
    {
        if (string.IsNullOrEmpty(savePresetName))
        {
            Debug.LogWarning("Cannot save preset with empty name");
            return;
        }

        // Create preset from current settings
        var preset = SimulationPresetManager.CreatePreset(savePresetName, simulation, matrixGenerator);

        // Save to file
        SimulationPresetManager.SavePreset(preset);

        // Update available presets list
        LoadAvailablePresets();

        // Update UI
        BuildUI();
    }

    void LoadPreset(string presetName)
    {
        // Load the preset
        var preset = SimulationPresetManager.LoadPreset(presetName);

        if (preset != null)
        {
            // Apply to simulation
            SimulationPresetManager.ApplyPreset(preset, simulation, matrixGenerator);

            // Update UI to reflect changes
            BuildUI();

            Debug.Log($"Loaded preset: {presetName}");
        }
    }

    void LoadAvailablePresets()
    {
        // Get list of available presets from the manager
        availablePresets = SimulationPresetManager.GetAvailablePresets();
    }

    #endregion
}