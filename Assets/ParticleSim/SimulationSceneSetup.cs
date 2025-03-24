using RosettaUI;
using UnityEngine;
using UnityEngine.UI;

// Add this to an empty GameObject in your scene
// It will set up all required components for the particle simulation
public class SimulationSceneSetup : MonoBehaviour
{
    // Prefabs
    public GameObject particlePrefab;
    public GameObject rosettaUIPrefab;

    // UI prefabs for the matrix visualizer
    public GameObject matrixContainerPrefab;
    public GameObject cellPrefab;
    public GameObject rowLabelPrefab;
    public GameObject columnLabelPrefab;
    public GameObject cornerLabelPrefab;

    void Reset()
    {
        // This gets called when the component is added or Reset is clicked in the editor
        Debug.Log("SimulationSceneSetup: Add required prefabs in the inspector");
    }

    // This method can be called from the editor via context menu to set up the scene
    [ContextMenu("Setup Simulation Scene")]
    public void SetupScene()
    {
        // Check for required prefabs
        if (particlePrefab == null || rosettaUIPrefab == null)
        {
            Debug.LogError("SimulationSceneSetup: Particle prefab and/or RosettaUI prefab is missing!");
            return;
        }

        // Create simulation object
        GameObject simulationObj = new GameObject("ParticleSimulation");

        // Add simulation components
        OptimizedParticleSimulation simulation = simulationObj.AddComponent<OptimizedParticleSimulation>();
        simulation.particlePrefab = particlePrefab;

        // Add matrix generator
        InteractionMatrixGenerator matrixGenerator = simulationObj.AddComponent<InteractionMatrixGenerator>();

        // Add UI
        ParticleSimulationUI ui = simulationObj.AddComponent<ParticleSimulationUI>();

        // Create RosettaUI root if not already in scene
        if (FindObjectOfType<RosettaUIRoot>() == null)
        {
            Instantiate(rosettaUIPrefab);
        }

        // Set up matrix visualizer if UI prefabs are available
        if (matrixContainerPrefab != null && cellPrefab != null &&
            rowLabelPrefab != null && columnLabelPrefab != null && cornerLabelPrefab != null)
        {
            SetupMatrixVisualizer(simulationObj, simulation, matrixGenerator);
        }

        Debug.Log("SimulationSceneSetup: Scene setup complete!");
    }

    private void SetupMatrixVisualizer(GameObject simulationObj, OptimizedParticleSimulation simulation, InteractionMatrixGenerator matrixGenerator)
    {
        // Create visualizer object
        GameObject visualizerObj = new GameObject("MatrixVisualizer");

        // Add Canvas
        Canvas canvas = visualizerObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Add Canvas Scaler
        CanvasScaler scaler = visualizerObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Add Graphic Raycaster
        visualizerObj.AddComponent<GraphicRaycaster>();

        // Create matrix container
        GameObject container = Instantiate(matrixContainerPrefab, visualizerObj.transform);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.7f, 0.5f);
        containerRect.anchorMax = new Vector2(1.0f, 1.0f);
        containerRect.offsetMin = new Vector2(10, 10);
        containerRect.offsetMax = new Vector2(-10, -10);

        // Add visualizer component
        InteractionMatrixVisualizer visualizer = visualizerObj.AddComponent<InteractionMatrixVisualizer>();
        visualizer.simulation = simulation;
        visualizer.matrixGenerator = matrixGenerator;
        visualizer.matrixContainer = containerRect;
        visualizer.cellPrefab = cellPrefab;
        visualizer.rowLabelPrefab = rowLabelPrefab;
        visualizer.columnLabelPrefab = columnLabelPrefab;
        visualizer.cornerLabelPrefab = cornerLabelPrefab;

        // Set canvas to not active by default (will be toggled by UI)
        canvas.gameObject.SetActive(false);
    }

    // Add this method for getting started quickly with a preset pattern
    [ContextMenu("Quick Start Simulation")]
    public void QuickStartSimulation()
    {
        SetupScene();

        // Get the components
        OptimizedParticleSimulation simulation = FindObjectOfType<OptimizedParticleSimulation>();
        InteractionMatrixGenerator matrixGenerator = FindObjectOfType<InteractionMatrixGenerator>();

        if (simulation == null || matrixGenerator == null)
        {
            Debug.LogError("QuickStartSimulation: Failed to find simulation components!");
            return;
        }

        // Configure a preset pattern
        matrixGenerator.patternType = InteractionMatrixGenerator.PatternType.PredatorPrey;
        matrixGenerator.generateParticleTypes = true;
        matrixGenerator.applyRecommendedSettings = true;

        // Generate the matrix
        matrixGenerator.GenerateMatrix();

        Debug.Log("QuickStartSimulation: Simulation started with Predator-Prey pattern!");
    }
}