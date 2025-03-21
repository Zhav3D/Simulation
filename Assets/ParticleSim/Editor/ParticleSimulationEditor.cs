using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
[CustomEditor(typeof(OptimizedParticleSimulation))]
public class OptimizedParticleSimulationEditor : Editor
{
    private bool showInteractionMatrix = false;
    private bool showPerformanceSettings = false;
    private bool showDebugInfo = false;

    // Performance monitoring
    private float lastFrameTime = 0f;
    private float avgFrameTime = 0f;
    private float minFrameTime = float.MaxValue;
    private float maxFrameTime = 0f;
    private int frameCount = 0;
    private readonly int frameWindow = 60; // Number of frames to average
    private readonly Queue<float> frameTimes = new Queue<float>();

    public override void OnInspectorGUI()
    {
        OptimizedParticleSimulation simulation = (OptimizedParticleSimulation)target;

        // Draw default inspector for most properties
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Performance settings foldout
        showPerformanceSettings = EditorGUILayout.Foldout(showPerformanceSettings, "Performance Optimization Settings");
        if (showPerformanceSettings)
        {
            EditorGUI.indentLevel++;

            // Spatial partitioning toggle with explanation
            EditorGUILayout.BeginHorizontal();
            simulation.useGridPartitioning = EditorGUILayout.Toggle("Use Spatial Partitioning", simulation.useGridPartitioning);
            if (GUILayout.Button("?", GUILayout.Width(25)))
            {
                EditorUtility.DisplayDialog("Spatial Partitioning",
                    "Divides the space into a grid to reduce the number of particle comparisons.\n\n" +
                    "ON: O(n×k) complexity where k is average neighbors per cell\n" +
                    "OFF: O(n²) complexity (slower with many particles)\n\n" +
                    "Adjust 'Cell Size' based on particle density and interaction radius.", "OK");
            }
            EditorGUILayout.EndHorizontal();

            // Only show cell size if grid is enabled
            if (simulation.useGridPartitioning)
            {
                EditorGUI.indentLevel++;
                simulation.cellSize = EditorGUILayout.FloatField("Cell Size", simulation.cellSize);

                if (simulation.cellSize < simulation.interactionRadius * 0.25f)
                {
                    EditorGUILayout.HelpBox("Cell size is very small, which may increase memory usage and reduce performance.", MessageType.Warning);
                }
                else if (simulation.cellSize > simulation.interactionRadius)
                {
                    EditorGUILayout.HelpBox("Cell size is larger than interaction radius, which may cause missed interactions.", MessageType.Warning);
                }
                EditorGUI.indentLevel--;
            }

            // Jobs system toggle with explanation
            EditorGUILayout.BeginHorizontal();
            simulation.useJobSystem = EditorGUILayout.Toggle("Use Jobs System", simulation.useJobSystem);
            if (GUILayout.Button("?", GUILayout.Width(25)))
            {
                EditorUtility.DisplayDialog("Unity Jobs System",
                    "Uses multi-threading to parallelize particle calculations.\n\n" +
                    "ON: Utilizes multiple CPU cores and SIMD instructions\n" +
                    "OFF: Single-threaded calculations (simpler but slower)\n\n" +
                    "Best for simulations with 1000+ particles.", "OK");
            }
            EditorGUILayout.EndHorizontal();

            // Warning about combining features
            if (simulation.useJobSystem && simulation.useGridPartitioning)
            {
                EditorGUILayout.HelpBox("Note: The current implementation uses either Jobs OR Grid, not both together. When both are enabled, Jobs takes precedence.", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        // Debug information foldout
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            showDebugInfo = EditorGUILayout.Foldout(showDebugInfo, "Performance Diagnostics");

            if (showDebugInfo)
            {
                EditorGUI.indentLevel++;

                // Update frame time stats
                UpdateFrameTimeStats();

                // Display particle count
                int particleCount = simulation.GetParticleCount();
                EditorGUILayout.LabelField("Particle Count", particleCount.ToString());

                // Display frame time stats
                EditorGUILayout.LabelField("Current Frame Time", $"{lastFrameTime * 1000f:F2} ms ({1f / Mathf.Max(0.001f, lastFrameTime):F1} FPS)");
                EditorGUILayout.LabelField("Average Frame Time", $"{avgFrameTime * 1000f:F2} ms ({1f / Mathf.Max(0.001f, avgFrameTime):F1} FPS)");
                EditorGUILayout.LabelField("Min/Max Frame Time", $"{minFrameTime * 1000f:F2} ms / {maxFrameTime * 1000f:F2} ms");

                // Current optimizations in use
                string optimizations = "";
                optimizations += simulation.useGridPartitioning ? "Spatial Grid, " : "";
                optimizations += simulation.useJobSystem ? "Jobs System, " : "";
                optimizations = optimizations.TrimEnd(' ', ',');

                if (string.IsNullOrEmpty(optimizations))
                {
                    optimizations = "None (unoptimized)";
                }

                EditorGUILayout.LabelField("Active Optimizations", optimizations);

                // Performance recommendations
                if (particleCount > 1000 && !simulation.useJobSystem && !simulation.useGridPartitioning)
                {
                    EditorGUILayout.HelpBox("Recommendation: Enable Jobs System or Spatial Grid for better performance with this many particles.", MessageType.Info);
                }

                if (1f / Mathf.Max(0.001f, avgFrameTime) < 30f)
                {
                    EditorGUILayout.HelpBox("Performance is below 30 FPS. Consider reducing particle count or enabling additional optimizations.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
        }

        // Custom matrix editor for interactions
        EditorGUILayout.Space(10);
        showInteractionMatrix = EditorGUILayout.Foldout(showInteractionMatrix, "Interaction Matrix Editor");

        if (showInteractionMatrix && simulation.particleTypes.Count > 0)
        {
            EditorGUILayout.HelpBox("Set attraction values between particle types. Positive values attract, negative values repel.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Create a dictionary for quick lookup
            Dictionary<(int, int), float> interactionValues = new Dictionary<(int, int), float>();
            foreach (var rule in simulation.interactionRules)
            {
                interactionValues[(rule.typeIndexA, rule.typeIndexB)] = rule.attractionValue;
            }

            // Matrix header row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Effect of ↓ on →", GUILayout.Width(100));

            for (int i = 0; i < simulation.particleTypes.Count; i++)
            {
                EditorGUILayout.LabelField(simulation.particleTypes[i].name, EditorStyles.boldLabel, GUILayout.Width(80));
            }

            EditorGUILayout.EndHorizontal();

            // Matrix rows (now supporting asymmetric relationships)
            for (int i = 0; i < simulation.particleTypes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(simulation.particleTypes[i].name, EditorStyles.boldLabel, GUILayout.Width(100));

                for (int j = 0; j < simulation.particleTypes.Count; j++)
                {
                    float value = 0;
                    interactionValues.TryGetValue((i, j), out value);

                    // Use custom colored field to indicate attraction/repulsion
                    EditorGUI.BeginChangeCheck();

                    // Gradient colors: red (-1.0) to white (0.0) to green (1.0)
                    Color fieldColor = value > 0
                        ? Color.Lerp(Color.white, new Color(0.7f, 1f, 0.7f), Mathf.Abs(value))
                        : Color.Lerp(Color.white, new Color(1f, 0.7f, 0.7f), Mathf.Abs(value));

                    GUI.color = fieldColor;
                    float newValue = EditorGUILayout.FloatField(value, GUILayout.Width(80));
                    GUI.color = Color.white;

                    if (EditorGUI.EndChangeCheck())
                    {
                        // Don't mirror values anymore, only set the specific direction
                        SetDirectionalInteractionValue(simulation, i, j, newValue);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            // Buttons for matrix operations
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset All Interactions"))
            {
                if (EditorUtility.DisplayDialog("Reset Interactions",
                    "Are you sure you want to clear all interaction rules?", "Yes", "Cancel"))
                {
                    simulation.interactionRules.Clear();
                    EditorUtility.SetDirty(simulation);
                }
            }

            if (GUILayout.Button("Randomize Interactions"))
            {
                if (EditorUtility.DisplayDialog("Randomize Interactions",
                    "Are you sure you want to randomize all interaction values?", "Yes", "Cancel"))
                {
                    RandomizeInteractions(simulation);
                    EditorUtility.SetDirty(simulation);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Preset buttons
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Presets:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Orbit System"))
            {
                CreateOrbitSystemPreset(simulation);
            }

            if (GUILayout.Button("Galaxy Formation"))
            {
                CreateGalaxyPreset(simulation);
            }

            if (GUILayout.Button("Fluid Simulation"))
            {
                CreateFluidPreset(simulation);
            }
            EditorGUILayout.EndHorizontal();
        }

        // Apply changes
        if (GUI.changed)
        {
            EditorUtility.SetDirty(simulation);
        }

        // Force repaint for live updates
        if (Application.isPlaying && showDebugInfo)
        {
            Repaint();
        }
    }

    private void SetDirectionalInteractionValue(OptimizedParticleSimulation simulation, int typeA, int typeB, float value)
    {
        // Look for existing rule
        for (int i = 0; i < simulation.interactionRules.Count; i++)
        {
            var rule = simulation.interactionRules[i];
            if (rule.typeIndexA == typeA && rule.typeIndexB == typeB)
            {
                // Update existing rule
                rule.attractionValue = value;
                simulation.interactionRules[i] = rule;
                return;
            }
        }

        // Create new rule
        var newRule = new OptimizedParticleSimulation.InteractionRule
        {
            typeIndexA = typeA,
            typeIndexB = typeB,
            attractionValue = value
        };

        simulation.interactionRules.Add(newRule);
    }

    private void UpdateFrameTimeStats()
    {
        lastFrameTime = Time.deltaTime;

        // Add to queue and maintain window size
        frameTimes.Enqueue(lastFrameTime);
        if (frameTimes.Count > frameWindow)
        {
            frameTimes.Dequeue();
        }

        // Calculate stats
        float sum = 0f;
        minFrameTime = float.MaxValue;
        maxFrameTime = 0f;

        foreach (float time in frameTimes)
        {
            sum += time;
            minFrameTime = Mathf.Min(minFrameTime, time);
            maxFrameTime = Mathf.Max(maxFrameTime, time);
        }

        avgFrameTime = sum / frameTimes.Count;
    }

    private void SetInteractionValue(OptimizedParticleSimulation simulation, int typeA, int typeB, float value)
    {
        // Look for existing rule
        for (int i = 0; i < simulation.interactionRules.Count; i++)
        {
            var rule = simulation.interactionRules[i];
            if ((rule.typeIndexA == typeA && rule.typeIndexB == typeB) ||
                (rule.typeIndexA == typeB && rule.typeIndexB == typeA))
            {
                // Update existing rule
                rule.attractionValue = value;
                simulation.interactionRules[i] = rule;
                return;
            }
        }

        // Create new rule
        var newRule = new OptimizedParticleSimulation.InteractionRule
        {
            typeIndexA = typeA,
            typeIndexB = typeB,
            attractionValue = value
        };

        simulation.interactionRules.Add(newRule);
    }

    private void RandomizeInteractions(OptimizedParticleSimulation simulation)
    {
        simulation.interactionRules.Clear();

        for (int i = 0; i < simulation.particleTypes.Count; i++)
        {
            for (int j = i; j < simulation.particleTypes.Count; j++)
            {
                float value = Random.Range(-1f, 1f);

                var rule = new OptimizedParticleSimulation.InteractionRule
                {
                    typeIndexA = i,
                    typeIndexB = j,
                    attractionValue = value
                };

                simulation.interactionRules.Add(rule);
            }
        }
    }

    private void CreateOrbitSystemPreset(OptimizedParticleSimulation simulation)
    {
        // Ensure we have at least 2 particle types
        while (simulation.particleTypes.Count < 2)
        {
            simulation.particleTypes.Add(new OptimizedParticleSimulation.ParticleType
            {
                name = simulation.particleTypes.Count == 0 ? "Star" : "Planet"
            });
        }

        // Configure types
        simulation.particleTypes[0].name = "Star";
        simulation.particleTypes[0].color = new Color(1f, 0.8f, 0.2f); // Yellow
        simulation.particleTypes[0].mass = 100f;
        simulation.particleTypes[0].radius = 2f;
        simulation.particleTypes[0].spawnAmount = 1;

        simulation.particleTypes[1].name = "Planet";
        simulation.particleTypes[1].color = new Color(0.2f, 0.4f, 0.8f); // Blue
        simulation.particleTypes[1].mass = 1f;
        simulation.particleTypes[1].radius = 0.5f;
        simulation.particleTypes[1].spawnAmount = 100;

        // Set up rules
        simulation.interactionRules.Clear();

        // Star attracts planets
        SetDirectionalInteractionValue(simulation, 0, 1, 10f);
        // Planets are attracted to star (set both directions)
        SetDirectionalInteractionValue(simulation, 1, 0, 10f);

        // Star self-interaction (none)
        SetDirectionalInteractionValue(simulation, 0, 0, 0f);

        // Planet self-interaction (slight repulsion)
        SetDirectionalInteractionValue(simulation, 1, 1, -0.1f);

        // Adjust simulation parameters
        simulation.interactionStrength = 0.1f;
        simulation.dampening = 1.0f; // No energy loss
        simulation.minDistance = 1.0f;

        EditorUtility.SetDirty(simulation);
    }

    private void CreateGalaxyPreset(OptimizedParticleSimulation simulation)
    {
        // Ensure we have at least 3 particle types
        while (simulation.particleTypes.Count < 3)
        {
            simulation.particleTypes.Add(new OptimizedParticleSimulation.ParticleType
            {
                name = "Type " + simulation.particleTypes.Count
            });
        }

        // Configure types
        simulation.particleTypes[0].name = "Black Hole";
        simulation.particleTypes[0].color = new Color(0.1f, 0.0f, 0.2f); // Dark purple
        simulation.particleTypes[0].mass = 500f;
        simulation.particleTypes[0].radius = 3f;
        simulation.particleTypes[0].spawnAmount = 1;

        simulation.particleTypes[1].name = "Stars";
        simulation.particleTypes[1].color = new Color(0.9f, 0.9f, 1.0f); // White
        simulation.particleTypes[1].mass = 1f;
        simulation.particleTypes[1].radius = 0.3f;
        simulation.particleTypes[1].spawnAmount = 200;

        simulation.particleTypes[2].name = "Dust";
        simulation.particleTypes[2].color = new Color(0.5f, 0.3f, 0.7f); // Purple
        simulation.particleTypes[2].mass = 0.1f;
        simulation.particleTypes[2].radius = 0.1f;
        simulation.particleTypes[2].spawnAmount = 300;

        // Set up rules
        simulation.interactionRules.Clear();

        // Black hole strongly attracts everything
        SetInteractionValue(simulation, 0, 1, 5.0f);
        SetInteractionValue(simulation, 0, 2, 5.0f);

        // Stars weakly attract each other and dust
        SetInteractionValue(simulation, 1, 1, 0.2f);
        SetInteractionValue(simulation, 1, 2, 0.3f);

        // Dust weakly attracts dust
        SetInteractionValue(simulation, 2, 2, 0.1f);

        // Adjust simulation parameters
        simulation.interactionStrength = 0.2f;
        simulation.dampening = 0.99f;
        simulation.minDistance = 0.5f;
        simulation.interactionRadius = 20f;

        EditorUtility.SetDirty(simulation);
    }

    private void CreateFluidPreset(OptimizedParticleSimulation simulation)
    {
        // Ensure we have at least 3 particle types
        while (simulation.particleTypes.Count < 3)
        {
            simulation.particleTypes.Add(new OptimizedParticleSimulation.ParticleType
            {
                name = "Type " + simulation.particleTypes.Count
            });
        }

        // Configure types
        simulation.particleTypes[0].name = "Water";
        simulation.particleTypes[0].color = new Color(0.2f, 0.5f, 0.9f); // Blue
        simulation.particleTypes[0].mass = 1f;
        simulation.particleTypes[0].radius = 0.3f;
        simulation.particleTypes[0].spawnAmount = 300;

        simulation.particleTypes[1].name = "Oil";
        simulation.particleTypes[1].color = new Color(0.8f, 0.6f, 0.2f); // Amber
        simulation.particleTypes[1].mass = 0.7f;
        simulation.particleTypes[1].radius = 0.3f;
        simulation.particleTypes[1].spawnAmount = 200;

        simulation.particleTypes[2].name = "Gas";
        simulation.particleTypes[2].color = new Color(0.7f, 0.7f, 0.7f, 0.5f); // Light gray
        simulation.particleTypes[2].mass = 0.2f;
        simulation.particleTypes[2].radius = 0.2f;
        simulation.particleTypes[2].spawnAmount = 150;

        // Set up rules
        simulation.interactionRules.Clear();

        // Water attracts water, repels oil
        SetInteractionValue(simulation, 0, 0, 0.4f);
        SetInteractionValue(simulation, 0, 1, -0.2f);
        SetInteractionValue(simulation, 0, 2, -0.05f);

        // Oil attracts oil, repels water
        SetInteractionValue(simulation, 1, 1, 0.4f);
        SetInteractionValue(simulation, 1, 2, -0.05f);

        // Gas weakly attracts itself, is otherwise neutral
        SetInteractionValue(simulation, 2, 2, 0.1f);

        // Adjust simulation parameters
        simulation.interactionStrength = 0.3f;
        simulation.dampening = 0.8f; // More friction
        simulation.minDistance = 0.2f;
        simulation.maxVelocity = 10f;
        simulation.interactionRadius = 5f;

        EditorUtility.SetDirty(simulation);
    }
}
#endif