// Main Simulation Controller and Visualization
// This script ties all components together and provides visualization options

using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class FruitFlySimulationController : MonoBehaviour
{
    // References to other components
    [Header("Simulation Components")]
    public FruitFlyCellSimulator cellSimulator;
    public CellBehaviorManager behaviorManager;
    public EnvironmentController environmentController;

    [Header("Visualization Settings")]
    public bool visualizeCells = true;
    public bool visualizeEnvironment = true;
    public bool visualizeGradients = true;
    public bool showDebugInfo = true;

    [Header("Rendering")]
    public Material cellRenderMaterial;
    public Material gradientRenderMaterial;
    public ComputeShader environmentCompute;

    [Header("UI Elements")]
    public TMP_Text statsText;
    public TMP_Text cellTypeCountText;
    public Slider timeScaleSlider;
    public Toggle pauseToggle;
    public TMP_Dropdown viewModeDropdown;
    public Button resetButton;

    // Visualization settings
    private enum ViewMode { All, Epithelial, Neurons, Muscle, Immune, FatBody }
    private ViewMode currentViewMode = ViewMode.All;

    // Runtime variables
    private float simulationTime = 0f;
    private bool isPaused = false;
    private float timeScale = 1f;

    // Statistics tracking
    private int[] cellTypeCounts = new int[System.Enum.GetValues(typeof(CellType)).Length];
    private int[] cellStateCounts = new int[System.Enum.GetValues(typeof(CellState)).Length];
    private float averageEnergy = 0f;
    private float averageAge = 0f;

    // Cell visualization
    private ComputeBuffer cellBuffer;
    private ComputeBuffer cellTypeBuffer;

    // Cell color scheme
    private Color[] cellTypeColors = new Color[]
    {
        new Color(0.8f, 0.2f, 0.2f), // Epithelial - Red
        new Color(0.2f, 0.8f, 0.2f), // Neuron - Green
        new Color(0.2f, 0.2f, 0.8f), // Muscle - Blue
        new Color(0.8f, 0.8f, 0.2f), // Immune - Yellow
        new Color(0.8f, 0.6f, 0.2f), // FatBody - Orange
        new Color(0.7f, 0.7f, 0.7f)  // Other - Gray
    };

    // Environment visualization
    private RenderTexture environmentTexture;
    private ComputeBuffer environmentBuffer;

    void Start()
    {
        InitializeSimulation();
        SetupUI();
        SetupVisualization();
    }

    void Update()
    {
        if (!isPaused)
        {
            // Update simulation time
            float deltaTime = Time.deltaTime * timeScale;
            simulationTime += deltaTime;

            // Set simulation parameters from UI
            UpdateSimulationParameters();
        }

        // Update statistics and UI every frame regardless of pause state
        UpdateStatistics();
        UpdateUI();

        // Update visualization
        if (visualizeCells)
        {
            UpdateCellVisualization();
        }

        if (visualizeEnvironment)
        {
            UpdateEnvironmentVisualization();
        }

        if (visualizeGradients)
        {
            UpdateGradientVisualization();
        }
    }

    private void InitializeSimulation()
    {
        // Initialize cell simulator if not already done
        if (cellSimulator == null)
        {
            cellSimulator = gameObject.AddComponent<FruitFlyCellSimulator>();
            cellSimulator.initialCellCount = 5000;
            cellSimulator.worldBounds = 100f;
        }

        // Initialize behavior manager
        if (behaviorManager == null)
        {
            behaviorManager = gameObject.AddComponent<CellBehaviorManager>();
            behaviorManager.mainSimulator = cellSimulator;
        }

        // Initialize environment controller
        if (environmentController == null)
        {
            environmentController = gameObject.AddComponent<EnvironmentController>();
            environmentController.mainSimulator = cellSimulator;
        }
    }

    private void SetupUI()
    {
        // Setup time scale slider
        if (timeScaleSlider != null)
        {
            timeScaleSlider.onValueChanged.AddListener(value => {
                timeScale = value;
                cellSimulator.simulationSpeed = value;
            });
        }

        // Setup pause toggle
        if (pauseToggle != null)
        {
            pauseToggle.onValueChanged.AddListener(value => {
                isPaused = value;
            });
        }

        // Setup view mode dropdown
        if (viewModeDropdown != null)
        {
            viewModeDropdown.onValueChanged.AddListener(value => {
                currentViewMode = (ViewMode)value;
            });
        }

        // Setup reset button
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(() => {
                ResetSimulation();
            });
        }
    }

    private void SetupVisualization()
    {
        // Setup cell visualization
        int maxCells = cellSimulator.initialCellCount;
        cellBuffer = new ComputeBuffer(maxCells, 32); // float3 position, float size, float4 color
        cellTypeBuffer = new ComputeBuffer(cellTypeColors.Length, 16); // float4 colors
        cellTypeBuffer.SetData(cellTypeColors);

        // Setup environment visualization
        environmentTexture = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat);
        environmentTexture.enableRandomWrite = true;
        environmentTexture.Create();

        environmentBuffer = new ComputeBuffer(environmentController.environmentRegionCount, 40);
    }

    private void UpdateSimulationParameters()
    {
        // Update parameters from UI
        cellSimulator.simulationSpeed = timeScale;
    }

    private void UpdateStatistics()
    {
        // Reset counters
        for (int i = 0; i < cellTypeCounts.Length; i++)
        {
            cellTypeCounts[i] = 0;
        }

        for (int i = 0; i < cellStateCounts.Length; i++)
        {
            cellStateCounts[i] = 0;
        }

        // Aggregate cell data
        float totalEnergy = 0f;
        float totalAge = 0f;

        for (int i = 0; i < cellSimulator.cellData.Length; i++)
        {
            CellData cell = cellSimulator.cellData[i];

            // Count by type
            cellTypeCounts[(int)cell.type]++;

            // Count by state
            cellStateCounts[(int)cell.state]++;

            // Sum energy and age
            totalEnergy += cell.energy;
            totalAge += cell.age;
        }

        // Calculate averages
        int cellCount = cellSimulator.cellData.Length;
        averageEnergy = totalEnergy / cellCount;
        averageAge = totalAge / cellCount;
    }

    private void UpdateUI()
    {
        if (statsText != null)
        {
            statsText.text = string.Format(
                "Simulation Time: {0:F1}s\n" +
                "Cells: {1}\n" +
                "Avg Energy: {2:P0}\n" +
                "Avg Age: {3:P0}\n" +
                "Alive: {4} | Stressed: {5} | Dying: {6}",
                simulationTime,
                cellSimulator.cellData.Length,
                averageEnergy,
                averageAge,
                cellStateCounts[(int)CellState.Alive],
                cellStateCounts[(int)CellState.Stressed],
                cellStateCounts[(int)CellState.Dying]
            );
        }

        if (cellTypeCountText != null)
        {
            cellTypeCountText.text = string.Format(
                "Epithelial: {0}\n" +
                "Neurons: {1}\n" +
                "Muscle: {2}\n" +
                "Immune: {3}\n" +
                "Fat Body: {4}\n" +
                "Other: {5}",
                cellTypeCounts[(int)CellType.Epithelial],
                cellTypeCounts[(int)CellType.Neuron],
                cellTypeCounts[(int)CellType.Muscle],
                cellTypeCounts[(int)CellType.Immune],
                cellTypeCounts[(int)CellType.FatBody],
                cellTypeCounts[(int)CellType.Other]
            );
        }
    }

    private void UpdateCellVisualization()
    {
        // Create render data for cells
        var cellRenderData = new NativeArray<CellRenderData>(cellSimulator.cellData.Length, Allocator.Temp);

        for (int i = 0; i < cellSimulator.cellData.Length; i++)
        {
            CellData cell = cellSimulator.cellData[i];

            // Skip cell if it doesn't match current view mode filter
            if (currentViewMode != ViewMode.All && (int)currentViewMode - 1 != (int)cell.type)
            {
                continue;
            }

            // Get color based on cell type
            Color color = cellTypeColors[(int)cell.type];

            // Modify color based on cell state
            switch (cell.state)
            {
                case CellState.Stressed:
                    color = Color.Lerp(color, Color.red, 0.5f);
                    break;
                case CellState.Dying:
                    color = Color.Lerp(color, Color.black, 0.7f);
                    break;
                case CellState.Dead:
                    color = Color.gray;
                    break;
            }

            // Create render data
            cellRenderData[i] = new CellRenderData
            {
                position = cell.position,
                color = new float4(color.r, color.g, color.b, color.a),
                size = GetCellSize(cell)
            };
        }

        // Update compute buffer with cell data
        cellBuffer.SetData(cellRenderData);

        // Set buffer for rendering
        cellRenderMaterial.SetBuffer("_CellBuffer", cellBuffer);
        cellRenderMaterial.SetInt("_CellCount", cellSimulator.cellData.Length);
        cellRenderMaterial.SetBuffer("_CellTypeColors", cellTypeBuffer);

        // Clean up
        cellRenderData.Dispose();
    }

    private float GetCellSize(CellData cell)
    {
        // Base size for each cell type
        float baseSize = 1.0f;

        switch (cell.type)
        {
            case CellType.Epithelial: baseSize = 0.8f; break;
            case CellType.Neuron: baseSize = 0.7f; break;
            case CellType.Muscle: baseSize = 1.2f; break;
            case CellType.Immune: baseSize = 0.9f; break;
            case CellType.FatBody: baseSize = 1.5f; break;
            default: baseSize = 1.0f; break;
        }

        // Adjust size based on energy and state
        baseSize *= 0.7f + (cell.energy * 0.6f);

        if (cell.state == CellState.Dividing)
        {
            baseSize *= 1.2f; // Dividing cells appear larger
        }
        else if (cell.state == CellState.Dying)
        {
            baseSize *= 0.7f; // Dying cells appear smaller
        }

        return baseSize;
    }

    private void UpdateEnvironmentVisualization()
    {
        // Create environment data for visualization
        var envData = new List<EnvironmentVisData>();

        foreach (var region in environmentController.environmentRegions)
        {
            // Create visualization data
            Color color = GetEnvironmentColor(region);

            envData.Add(new EnvironmentVisData
            {
                position = region.position,
                color = new float4(color.r, color.g, color.b, color.a),
                temperature = region.temperature,
                oxygen = region.oxygen,
                nutrients = region.nutrients
            });
        }

        // Update compute buffer
        environmentBuffer.SetData(envData.ToArray());

        // Run compute shader to render environment to texture
        int kernelIndex = environmentCompute.FindKernel("RenderEnvironment");
        environmentCompute.SetBuffer(kernelIndex, "environmentRegions", environmentBuffer);
        environmentCompute.SetInt("regionCount", envData.Count);
        environmentCompute.SetTexture(kernelIndex, "Result", environmentTexture);
        environmentCompute.SetFloat("worldBounds", cellSimulator.worldBounds);

        // Dispatch compute shader
        environmentCompute.Dispatch(kernelIndex, environmentTexture.width / 8, environmentTexture.height / 8, 1);

        // Set texture for rendering
        Shader.SetGlobalTexture("_EnvironmentTexture", environmentTexture);
    }

    private Color GetEnvironmentColor(EnvironmentData region)
    {
        // Base color based on environment type
        Color baseColor = Color.white;

        switch (region.type)
        {
            case EnvironmentType.Hemolymph: baseColor = new Color(0.8f, 0.9f, 1.0f); break; // Light blue
            case EnvironmentType.Epithelial: baseColor = new Color(1.0f, 0.8f, 0.8f); break; // Light red
            case EnvironmentType.Nervous: baseColor = new Color(0.8f, 1.0f, 0.8f); break;    // Light green
            case EnvironmentType.Digestive: baseColor = new Color(1.0f, 0.9f, 0.6f); break;  // Yellow/brown
            case EnvironmentType.Muscle: baseColor = new Color(0.9f, 0.7f, 0.7f); break;     // Pinkish
            case EnvironmentType.Air: baseColor = new Color(0.9f, 0.9f, 1.0f); break;        // Very light blue
        }

        // Modulate color based on environment properties

        // Temperature affects red channel (hotter = more red)
        float temperatureEffect = (region.temperature - 20) / 15f; // Normalize to 0-1 range
        baseColor.r = Mathf.Lerp(baseColor.r, 1.0f, temperatureEffect * 0.5f);

        // Oxygen affects blue channel (more oxygen = more blue)
        baseColor.b = Mathf.Lerp(baseColor.b, 1.0f, region.oxygen * 0.3f);

        // Nutrients affect green channel (more nutrients = more green)
        baseColor.g = Mathf.Lerp(baseColor.g, 1.0f, region.nutrients * 0.3f);

        return baseColor;
    }

    private void UpdateGradientVisualization()
    {
        // For now, we'll use the environment visualization
        // In a more complex implementation, we would render each chemical gradient separately
    }

    public void ResetSimulation()
    {
        // Reset simulation time
        simulationTime = 0f;

        // Destroy current components
        Destroy(cellSimulator);
        Destroy(behaviorManager);
        Destroy(environmentController);

        // Reinitialize
        InitializeSimulation();

        // Clean up old visualization
        if (cellBuffer != null) cellBuffer.Release();
        if (cellTypeBuffer != null) cellTypeBuffer.Release();
        if (environmentBuffer != null) environmentBuffer.Release();

        // Setup new visualization
        SetupVisualization();
    }

    void OnRenderObject()
    {
        if (visualizeCells && cellRenderMaterial != null)
        {
            // Set up material to render cells
            cellRenderMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, cellSimulator.cellData.Length);
        }
    }

    void OnDestroy()
    {
        // Clean up
        if (cellBuffer != null) cellBuffer.Release();
        if (cellTypeBuffer != null) cellTypeBuffer.Release();
        if (environmentBuffer != null) environmentBuffer.Release();

        if (environmentTexture != null) environmentTexture.Release();
    }
}

// Struct for environment visualization
public struct EnvironmentVisData
{
    public float3 position;
    public float4 color;
    public float temperature;
    public float oxygen;
    public float nutrients;
}

// Custom cell renderer shader (to be used with cellRenderMaterial)
/*
Shader "Custom/CellRenderer" {
    Properties {
        _PointSize ("Point Size", Float) = 10
    }
    
    SubShader {
        Pass {
            Tags { "RenderType"="Opaque" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            
            #include "UnityCG.cginc"
            
            struct CellRenderData {
                float3 position;
                float4 color;
                float size;
            };
            
            StructuredBuffer<CellRenderData> _CellBuffer;
            StructuredBuffer<float4> _CellTypeColors;
            float _PointSize;
            int _CellCount;
            
            struct v2f {
                float4 pos : SV_POSITION;
                float4 col : COLOR0;
                float size : PSIZE;
            };
            
            v2f vert(uint id : SV_VertexID) {
                v2f o;
                
                // Get cell data
                CellRenderData cell = _CellBuffer[id];
                
                // Transform position
                o.pos = UnityObjectToClipPos(float4(cell.position, 1.0));
                
                // Set color
                o.col = cell.color;
                
                // Set point size
                o.size = _PointSize * cell.size;
                
                return o;
            }
            
            float4 frag(v2f i) : SV_Target {
                return i.col;
            }
            
            ENDCG
        }
    }
}
*/

// Environment renderer compute shader (to be used with environmentCompute)
/*
#pragma kernel RenderEnvironment

struct EnvironmentVisData {
    float3 position;
    float4 color;
    float temperature;
    float oxygen;
    float nutrients;
};

RWTexture2D<float4> Result;
StructuredBuffer<EnvironmentVisData> environmentRegions;
int regionCount;
float worldBounds;

[numthreads(8,8,1)]
void RenderEnvironment(uint3 id : SV_DispatchThreadID) {
    // Convert pixel coordinates to world space
    float2 uv = float2(id.xy) / float2(256, 256);
    float3 worldPos = float3((uv.x * 2 - 1) * worldBounds / 2, (uv.y * 2 - 1) * worldBounds / 2, 0);
    
    // Start with background color
    float4 color = float4(0.1, 0.1, 0.1, 1.0);
    float totalInfluence = 0.0;
    
    // For each environment region
    for (int i = 0; i < regionCount; i++) {
        EnvironmentVisData region = environmentRegions[i];
        
        // Calculate distance (2D for visualization)
        float2 regionPos2D = region.position.xy;
        float dist = distance(worldPos.xy, regionPos2D);
        
        // Define influence radius
        float radius = 20.0;
        
        if (dist < radius) {
            // Calculate influence factor
            float influence = 1.0 - (dist / radius);
            influence = influence * influence; // Square for smoother falloff
            
            // Add weighted color
            color += region.color * influence;
            totalInfluence += influence;
        }
    }
    
    // Normalize color if needed
    if (totalInfluence > 0.0) {
        color = color / (1.0 + totalInfluence);
    }
    
    Result[id.xy] = color;
}
*/