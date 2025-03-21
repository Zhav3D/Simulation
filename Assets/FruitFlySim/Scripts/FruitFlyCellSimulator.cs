// Fruit Fly Cell Simulator - Architecture Overview
// This is a high-level design for simulating fruit fly cells in Unity using the C# Jobs system

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

// Main controller class
public class FruitFlyCellSimulator : MonoBehaviour
{
    // Simulation parameters
    [Header("Simulation Parameters")]
    public int initialCellCount = 5000;  // Starting with a smaller subset
    public float worldBounds = 100f;     // Simulation space size
    public float simulationSpeed = 1f;   // Time multiplier
    public float interactionRadius = 2f; // Distance for cell interactions

    // Cell type ratios (simplified model)
    [Header("Cell Type Distribution")]
    [Range(0, 1)] public float epithelialCellRatio = 0.3f;
    [Range(0, 1)] public float neuronRatio = 0.2f;
    [Range(0, 1)] public float muscleCellRatio = 0.2f;
    [Range(0, 1)] public float immuneCellRatio = 0.1f;
    [Range(0, 1)] public float fatBodyCellRatio = 0.1f;
    [Range(0, 1)] public float otherCellRatio = 0.1f;

    // Cell data containers
    public NativeArray<CellData> cellData;
    private NativeArray<CellInteraction> cellInteractions;

    // Graphics rendering
    private ComputeBuffer cellBuffer;
    public Material cellMaterial;

    // Cell cluster visualization
    public GameObject epithelialClusterPrefab;
    public GameObject neuronClusterPrefab;
    public GameObject muscleClusterPrefab;
    public GameObject immuneClusterPrefab;
    public GameObject fatBodyClusterPrefab;
    public GameObject otherClusterPrefab;

    private List<GameObject> cellClusters = new List<GameObject>();

    void Start()
    {
        InitializeCellData();
        SetupGraphics();
    }

    void Update()
    {
        float deltaTime = Time.deltaTime * simulationSpeed;

        // Run the simulation jobs
        SimulateCells(deltaTime);

        // Update visualization
        UpdateCellVisualization();
    }

    private void InitializeCellData()
    {
        // Allocate memory for cells
        cellData = new NativeArray<CellData>(initialCellCount, Allocator.Persistent);
        cellInteractions = new NativeArray<CellInteraction>(initialCellCount * 10, Allocator.Persistent);

        // Initialize each cell
        for (int i = 0; i < initialCellCount; i++)
        {
            CellData cell = new CellData
            {
                position = new float3(
                    UnityEngine.Random.Range(-worldBounds / 2, worldBounds / 2),
                    UnityEngine.Random.Range(-worldBounds / 2, worldBounds / 2),
                    UnityEngine.Random.Range(-worldBounds / 2, worldBounds / 2)
                ),
                velocity = new float3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f)
                ) * 0.5f,
                energy = UnityEngine.Random.Range(0.5f, 1f),
                age = UnityEngine.Random.Range(0f, 1f),
                type = AssignCellType(i),
                state = CellState.Alive,
                signalStrength = UnityEngine.Random.Range(0f, 0.2f)
            };

            cellData[i] = cell;
        }
    }

    private CellType AssignCellType(int index)
    {
        // Distribute cell types according to ratios
        float random = UnityEngine.Random.value;
        float sum = 0f;

        sum += epithelialCellRatio;
        if (random <= sum) return CellType.Epithelial;

        sum += neuronRatio;
        if (random <= sum) return CellType.Neuron;

        sum += muscleCellRatio;
        if (random <= sum) return CellType.Muscle;

        sum += immuneCellRatio;
        if (random <= sum) return CellType.Immune;

        sum += fatBodyCellRatio;
        if (random <= sum) return CellType.FatBody;

        return CellType.Other;
    }

    private void SimulateCells(float deltaTime)
    {
        // 1. Cell Behavior Job - determines individual cell actions
        var cellBehaviorJob = new CellBehaviorJob
        {
            cellData = cellData,
            deltaTime = deltaTime,
            worldBounds = worldBounds
        };
        JobHandle behaviorHandle = cellBehaviorJob.Schedule(initialCellCount, 64);

        // 2. Cell Interaction Job - cellular interaction and signaling
        var cellInteractionJob = new CellInteractionJob
        {
            cellData = cellData,
            cellInteractions = cellInteractions,
            interactionRadius = interactionRadius,
            interactionCount = new NativeArray<int>(1, Allocator.TempJob)
        };
        JobHandle interactionHandle = cellInteractionJob.Schedule(behaviorHandle);

        // 3. Signal Processing Job - process signals between cells
        var signalProcessingJob = new SignalProcessingJob
        {
            cellData = cellData,
            cellInteractions = cellInteractions,
            interactionCount = cellInteractionJob.interactionCount,
            deltaTime = deltaTime
        };
        JobHandle signalHandle = signalProcessingJob.Schedule(interactionHandle);

        // Wait for all jobs to complete
        signalHandle.Complete();

        // Clean up
        cellInteractionJob.interactionCount.Dispose();
    }

    private void SetupGraphics()
    {
        // Create compute buffer for cells
        cellBuffer = new ComputeBuffer(initialCellCount, 32); // Size of CellRenderData struct

        // Set up cell clusters
        CreateCellClusters();
    }

    private void CreateCellClusters()
    {
        // Create visual representations for cell clusters
        // This is a simplification - in reality we would have more sophisticated visualization

        int epithelialCount = Mathf.FloorToInt(initialCellCount * epithelialCellRatio);
        int neuronCount = Mathf.FloorToInt(initialCellCount * neuronRatio);
        int muscleCount = Mathf.FloorToInt(initialCellCount * muscleCellRatio);
        int immuneCount = Mathf.FloorToInt(initialCellCount * immuneCellRatio);
        int fatBodyCount = Mathf.FloorToInt(initialCellCount * fatBodyCellRatio);
        int otherCount = initialCellCount - epithelialCount - neuronCount - muscleCount - immuneCount - fatBodyCount;

        // Create clusters for each cell type
        CreateCluster(epithelialClusterPrefab, epithelialCount, CellType.Epithelial);
        CreateCluster(neuronClusterPrefab, neuronCount, CellType.Neuron);
        CreateCluster(muscleClusterPrefab, muscleCount, CellType.Muscle);
        CreateCluster(immuneClusterPrefab, immuneCount, CellType.Immune);
        CreateCluster(fatBodyClusterPrefab, fatBodyCount, CellType.FatBody);
        CreateCluster(otherClusterPrefab, otherCount, CellType.Other);
    }

    private void CreateCluster(GameObject prefab, int count, CellType type)
    {
        GameObject cluster = Instantiate(prefab);
        cluster.GetComponent<CellClusterController>().Initialize(count, type);
        cellClusters.Add(cluster);
    }

    private void UpdateCellVisualization()
    {
        // Create render data from cell data
        var renderData = new NativeArray<CellRenderData>(initialCellCount, Allocator.Temp);

        for (int i = 0; i < initialCellCount; i++)
        {
            renderData[i] = new CellRenderData
            {
                position = cellData[i].position,
                color = GetCellColor(cellData[i].type),
                size = GetCellSize(cellData[i])
            };
        }

        // Update compute buffer
        cellBuffer.SetData(renderData);
        cellMaterial.SetBuffer("_CellBuffer", cellBuffer);

        // Update clusters
        foreach (var cluster in cellClusters)
        {
            cluster.GetComponent<CellClusterController>().UpdateCluster(cellData);
        }

        renderData.Dispose();
    }

    private float4 GetCellColor(CellType cellType)
    {
        switch (cellType)
        {
            case CellType.Epithelial: return new float4(0.8f, 0.2f, 0.2f, 0.8f); // Reddish
            case CellType.Neuron: return new float4(0.2f, 0.8f, 0.2f, 0.8f);     // Greenish
            case CellType.Muscle: return new float4(0.2f, 0.2f, 0.8f, 0.8f);     // Bluish
            case CellType.Immune: return new float4(0.8f, 0.8f, 0.2f, 0.8f);     // Yellow
            case CellType.FatBody: return new float4(0.8f, 0.6f, 0.2f, 0.8f);    // Orange
            default: return new float4(0.7f, 0.7f, 0.7f, 0.8f);                  // Gray
        }
    }

    private float GetCellSize(CellData cell)
    {
        // Cell size varies by type and state
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

        // Modify by energy
        baseSize *= 0.8f + (cell.energy * 0.4f);

        return baseSize;
    }

    void OnDestroy()
    {
        // Clean up native arrays
        if (cellData.IsCreated) cellData.Dispose();
        if (cellInteractions.IsCreated) cellInteractions.Dispose();

        // Clean up compute buffer
        if (cellBuffer != null) cellBuffer.Release();
    }
}

// Cell related data structures

public enum CellType
{
    Epithelial, // Barrier cells
    Neuron,     // Brain and nervous system
    Muscle,     // Movement
    Immune,     // Defense
    FatBody,    // Energy storage
    Other       // Other types
}

public enum CellState
{
    Alive,
    Dividing,
    Specialized,
    Stressed,
    Dying,
    Dead
}

// Struct for cell data in the simulation
public struct CellData
{
    public float3 position;     // Position in 3D space
    public float3 velocity;     // Movement direction and speed
    public float energy;        // Energy reserves (0-1)
    public float age;           // Cellular age (0-1)
    public CellType type;       // Cell type
    public CellState state;     // Current cell state
    public float signalStrength; // Cell signaling strength
}

// Struct to track cell interactions
public struct CellInteraction
{
    public int cellA;          // Index of first cell
    public int cellB;          // Index of second cell
    public float strength;     // Interaction strength
    public float duration;     // How long the interaction has been happening
}

// Struct for rendering
public struct CellRenderData
{
    public float3 position;
    public float4 color;
    public float size;
}

// Jobs for parallel processing

[BurstCompile]
public struct CellBehaviorJob : IJobParallelFor
{
    public NativeArray<CellData> cellData;
    public float deltaTime;
    public float worldBounds;

    public void Execute(int index)
    {
        CellData cell = cellData[index];

        // Update position
        cell.position += cell.velocity * deltaTime;

        // Simple boundary handling
        for (int i = 0; i < 3; i++)
        {
            if (cell.position[i] > worldBounds / 2)
            {
                cell.position[i] = worldBounds / 2;
                cell.velocity[i] = -cell.velocity[i] * 0.8f;
            }
            else if (cell.position[i] < -worldBounds / 2)
            {
                cell.position[i] = -worldBounds / 2;
                cell.velocity[i] = -cell.velocity[i] * 0.8f;
            }
        }

        // Update age and energy
        cell.age += deltaTime * 0.01f; // Aging

        // Different cell types have different energy consumption rates
        float energyUse = deltaTime * 0.005f;
        switch (cell.type)
        {
            case CellType.Neuron: energyUse *= 1.5f; break; // Neurons use more energy
            case CellType.Muscle: energyUse *= 1.2f; break; // Muscles use more energy
            case CellType.FatBody: energyUse *= 0.7f; break; // Fat cells use less energy
        }

        cell.energy -= energyUse;

        // Cell state transitions based on age and energy
        if (cell.energy <= 0.1f)
        {
            cell.state = CellState.Stressed;
        }
        else if (cell.age >= 0.9f)
        {
            cell.state = CellState.Dying;
        }
        else if (cell.energy >= 0.8f && cell.age < 0.5f)
        {
            cell.state = CellState.Dividing;
        }
        else
        {
            cell.state = CellState.Alive;
        }

        // Cell type-specific behaviors
        switch (cell.type)
        {
            case CellType.Epithelial:
                // Epithelial cells tend to form sheets/clusters
                cell.velocity *= 0.95f; // Reduce movement
                break;

            case CellType.Neuron:
                // Neurons connect to others and transmit signals
                cell.signalStrength = math.sin(cell.age * 10f) * 0.5f + 0.5f;
                break;

            case CellType.Muscle:
                // Muscles contract and relax
                float contraction = math.sin(cell.age * 5f);
                cell.velocity = new float3(contraction, contraction, contraction) * 0.2f;
                break;

            case CellType.Immune:
                // Immune cells move more actively
                if (UnityEngine.Random.value < 0.05f)
                {
                    cell.velocity = new float3(
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f)
                    ) * 2f;
                }
                break;

            case CellType.FatBody:
                // Fat cells store energy and move less
                cell.energy = math.min(cell.energy + deltaTime * 0.01f, 1f);
                cell.velocity *= 0.9f;
                break;
        }

        cellData[index] = cell;
    }
}

[BurstCompile]
public struct CellInteractionJob : IJob
{
    [ReadOnly] public NativeArray<CellData> cellData;
    public NativeArray<CellInteraction> cellInteractions;
    public NativeArray<int> interactionCount;
    public float interactionRadius;

    public void Execute()
    {
        int count = 0;

        // A simple n^2 approach for demo purposes
        // In a real simulation, spatial partitioning would be essential
        for (int i = 0; i < cellData.Length; i++)
        {
            for (int j = i + 1; j < cellData.Length; j++)
            {
                // Check if cells are close enough to interact
                float dist = math.distance(cellData[i].position, cellData[j].position);

                if (dist < interactionRadius)
                {
                    // Record interaction
                    if (count < cellInteractions.Length)
                    {
                        cellInteractions[count] = new CellInteraction
                        {
                            cellA = i,
                            cellB = j,
                            strength = 1.0f - (dist / interactionRadius),
                            duration = 0.0f
                        };

                        count++;
                    }
                }
            }
        }

        interactionCount[0] = count;
    }
}

[BurstCompile]
public struct SignalProcessingJob : IJob
{
    public NativeArray<CellData> cellData;
    [ReadOnly] public NativeArray<CellInteraction> cellInteractions;
    [ReadOnly] public NativeArray<int> interactionCount;
    public float deltaTime;

    public void Execute()
    {
        int count = interactionCount[0];

        for (int i = 0; i < count; i++)
        {
            CellInteraction interaction = cellInteractions[i];

            // Get cell data
            CellData cellA = cellData[interaction.cellA];
            CellData cellB = cellData[interaction.cellB];

            // Process interactions based on cell types
            ProcessCellInteraction(ref cellA, ref cellB, interaction, deltaTime);

            // Save back updated cells
            cellData[interaction.cellA] = cellA;
            cellData[interaction.cellB] = cellB;
        }
    }

    private void ProcessCellInteraction(ref CellData cellA, ref CellData cellB, CellInteraction interaction, float deltaTime)
    {
        // Different interaction rules based on cell types

        // Neurons signal to other cells
        if (cellA.type == CellType.Neuron && cellB.type != CellType.Neuron)
        {
            cellB.signalStrength = math.max(cellB.signalStrength, cellA.signalStrength * interaction.strength);
        }
        else if (cellB.type == CellType.Neuron && cellA.type != CellType.Neuron)
        {
            cellA.signalStrength = math.max(cellA.signalStrength, cellB.signalStrength * interaction.strength);
        }

        // Immune cells interact with other cells
        if (cellA.type == CellType.Immune)
        {
            if (cellB.state == CellState.Dying || cellB.state == CellState.Dead)
            {
                // Immune cells clear dying cells
                cellA.energy = math.min(1.0f, cellA.energy + 0.1f * deltaTime);
            }
        }
        else if (cellB.type == CellType.Immune)
        {
            if (cellA.state == CellState.Dying || cellA.state == CellState.Dead)
            {
                // Immune cells clear dying cells
                cellB.energy = math.min(1.0f, cellB.energy + 0.1f * deltaTime);
            }
        }

        // Epithelial cells form adhesions
        if (cellA.type == CellType.Epithelial && cellB.type == CellType.Epithelial)
        {
            // Create adhesion by adjusting velocities
            float3 direction = math.normalize(cellB.position - cellA.position);
            float targetDistance = 1.0f;
            float currentDistance = math.distance(cellA.position, cellB.position);
            float adjustment = (currentDistance - targetDistance) * 0.1f;

            cellA.velocity += direction * adjustment * deltaTime;
            cellB.velocity -= direction * adjustment * deltaTime;
        }

        // Energy transfer from fat body cells
        if (cellA.type == CellType.FatBody && cellB.energy < 0.3f)
        {
            float transfer = math.min(cellA.energy * 0.1f, 0.3f - cellB.energy) * deltaTime;
            cellA.energy -= transfer;
            cellB.energy += transfer;
        }
        else if (cellB.type == CellType.FatBody && cellA.energy < 0.3f)
        {
            float transfer = math.min(cellB.energy * 0.1f, 0.3f - cellA.energy) * deltaTime;
            cellB.energy -= transfer;
            cellA.energy += transfer;
        }
    }
}