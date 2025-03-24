using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class WirelessSimulation : MonoBehaviour
{
    [Header("Simulation Parameters")]
    [SerializeField] private int gridSizeX = 100;
    [SerializeField] private int gridSizeY = 50;
    [SerializeField] private int gridSizeZ = 100;
    [SerializeField] private float cellSize = 0.1f; // Size of each cell in meters
    [SerializeField] private float propagationSpeed = 299792458f; // Speed of light in m/s
    [SerializeField] private float frequencyHz = 2.4e9f; // 2.4 GHz
    [SerializeField] private float transmitterPower = 100f; // In mW
    [SerializeField] private Transform transmitterTransform;
    [SerializeField] private Transform receiverTransform;

    [Header("Visualization")]
    [SerializeField] private Material waveMaterial;
    [SerializeField] private float visualizationIntensityScale = 5f;
    [SerializeField] private bool showVisualization = true;

    // Simulation state
    private NativeArray<float> fieldStrength;
    private NativeArray<float> previousFieldStrength;
    private NativeArray<int> mediumType; // 0 = air, 1 = solid object, etc.

    // Cached indices for transmitter and receiver
    private int3 transmitterIndex;
    private int3 receiverIndex;

    // Visualization
    private ComputeBuffer waveBuffer;
    private Mesh gridMesh;

    // Physics parameters
    private float wavelength;
    private float angularFrequency;
    private float timeStep;

    private void Start()
    {
        InitializeSimulation();
        CreateVisualizationMesh();
    }

    private void InitializeSimulation()
    {
        // Calculate physics parameters
        wavelength = propagationSpeed / frequencyHz;
        angularFrequency = 2f * Mathf.PI * frequencyHz;
        timeStep = cellSize / (2f * propagationSpeed); // Ensure stability

        // Initialize native arrays
        int totalCells = gridSizeX * gridSizeY * gridSizeZ;
        fieldStrength = new NativeArray<float>(totalCells, Allocator.Persistent);
        previousFieldStrength = new NativeArray<float>(totalCells, Allocator.Persistent);
        mediumType = new NativeArray<int>(totalCells, Allocator.Persistent);

        // Initialize medium (all air by default)
        for (int i = 0; i < totalCells; i++)
        {
            mediumType[i] = 0; // Air
        }

        // Map objects in the scene to the grid
        MapSceneObjectsToGrid();

        // Cache transmitter and receiver indices
        transmitterIndex = WorldToGridPosition(transmitterTransform.position);
        receiverIndex = WorldToGridPosition(receiverTransform.position);

        Debug.Log($"Simulation initialized with {totalCells} cells. Wavelength: {wavelength}m");
        Debug.Log($"Time step: {timeStep * 1e9f}ns. Cell size: {cellSize}m");
    }

    private void MapSceneObjectsToGrid()
    {
        // Find all colliders in the scene
        Collider[] colliders = FindObjectsOfType<Collider>();

        foreach (Collider collider in colliders)
        {
            // Skip the transmitter and receiver objects
            if (collider.transform == transmitterTransform || collider.transform == receiverTransform)
                continue;

            // Calculate bounds in grid space
            Bounds bounds = collider.bounds;
            int3 minBound = WorldToGridPosition(bounds.min);
            int3 maxBound = WorldToGridPosition(bounds.max);

            // Clamp to grid boundaries
            minBound = math.clamp(minBound, new int3(0, 0, 0), new int3(gridSizeX - 1, gridSizeY - 1, gridSizeZ - 1));
            maxBound = math.clamp(maxBound, new int3(0, 0, 0), new int3(gridSizeX - 1, gridSizeY - 1, gridSizeZ - 1));

            // Assign material properties based on object tag
            int materialType = 1; // Default solid

            if (collider.CompareTag("Metal"))
                materialType = 2;
            else if (collider.CompareTag("Glass"))
                materialType = 3;
            else if (collider.CompareTag("Wood"))
                materialType = 4;

            // Mark cells inside the object
            for (int x = minBound.x; x <= maxBound.x; x++)
            {
                for (int y = minBound.y; y <= maxBound.y; y++)
                {
                    for (int z = minBound.z; z <= maxBound.z; z++)
                    {
                        Vector3 cellWorldPos = GridToWorldPosition(new int3(x, y, z));
                        if (collider.bounds.Contains(cellWorldPos))
                        {
                            int index = GridPositionToIndex(new int3(x, y, z));
                            mediumType[index] = materialType;
                        }
                    }
                }
            }
        }

        Debug.Log("Scene objects mapped to simulation grid");
    }

    private void CreateVisualizationMesh()
    {
        if (!showVisualization)
            return;

        // Create a 3D grid of points for visualization
        gridMesh = new Mesh();

        // Create visualization buffer
        waveBuffer = new ComputeBuffer(fieldStrength.Length, sizeof(float));

        // Set the buffer to the material
        waveMaterial.SetBuffer("_WaveBuffer", waveBuffer);
        waveMaterial.SetFloat("_GridSizeX", gridSizeX);
        waveMaterial.SetFloat("_GridSizeY", gridSizeY);
        waveMaterial.SetFloat("_GridSizeZ", gridSizeZ);
        waveMaterial.SetFloat("_IntensityScale", visualizationIntensityScale);

        Debug.Log("Visualization initialized");
    }

    private void Update()
    {
        // Run the simulation step
        RunSimulationStep(Time.deltaTime);

        // Update visualization if enabled
        if (showVisualization)
            UpdateVisualization();

        // Update transmitter and receiver positions if they've moved
        if (transmitterTransform.hasChanged)
        {
            transmitterIndex = WorldToGridPosition(transmitterTransform.position);
            transmitterTransform.hasChanged = false;
        }

        if (receiverTransform.hasChanged)
        {
            receiverIndex = WorldToGridPosition(receiverTransform.position);
            receiverTransform.hasChanged = false;
        }

        // Calculate received power
        float receivedPower = CalculateReceivedPower();
        Debug.Log($"Received power: {receivedPower} mW");
    }

    private void RunSimulationStep(float deltaTime)
    {
        // Calculate how many physics steps to run this frame
        int steps = Mathf.CeilToInt(deltaTime / timeStep);
        float actualTimeStep = deltaTime / steps;

        for (int step = 0; step < steps; step++)
        {
            // Apply transmitter energy
            int transmitterIdx = GridPositionToIndex(transmitterIndex);
            float transmitterAmplitude = Mathf.Sqrt(transmitterPower) * Mathf.Sin(angularFrequency * Time.time);
            fieldStrength[transmitterIdx] = transmitterAmplitude;

            // Schedule wave propagation job
            var propagationJob = new WavePropagationJob
            {
                GridSizeX = gridSizeX,
                GridSizeY = gridSizeY,
                GridSizeZ = gridSizeZ,
                FieldStrength = fieldStrength,
                PreviousFieldStrength = previousFieldStrength,
                MediumType = mediumType,
                TimeStep = actualTimeStep,
                CellSize = cellSize,
                PropagationSpeed = propagationSpeed
            };

            // Execute job
            JobHandle handle = propagationJob.Schedule(fieldStrength.Length, 64);
            handle.Complete();

            // Swap buffers
            (fieldStrength, previousFieldStrength) = (previousFieldStrength, fieldStrength);
        }
    }

    private void UpdateVisualization()
    {
        // Update the wave buffer with current field values
        waveBuffer.SetData(fieldStrength);

        // Draw the grid using the material
        Graphics.DrawMesh(gridMesh, Matrix4x4.identity, waveMaterial, 0);
    }

    private float CalculateReceivedPower()
    {
        int receiverIdx = GridPositionToIndex(receiverIndex);
        float fieldAmplitude = fieldStrength[receiverIdx];

        // Power is proportional to amplitude squared
        return fieldAmplitude * fieldAmplitude;
    }

    private int3 WorldToGridPosition(Vector3 worldPosition)
    {
        // Convert world position to grid indices
        Vector3 localPos = worldPosition - transform.position + new Vector3(
            gridSizeX * cellSize * 0.5f,
            gridSizeY * cellSize * 0.5f,
            gridSizeZ * cellSize * 0.5f
        );

        int x = Mathf.FloorToInt(localPos.x / cellSize);
        int y = Mathf.FloorToInt(localPos.y / cellSize);
        int z = Mathf.FloorToInt(localPos.z / cellSize);

        // Clamp to grid boundaries
        x = Mathf.Clamp(x, 0, gridSizeX - 1);
        y = Mathf.Clamp(y, 0, gridSizeY - 1);
        z = Mathf.Clamp(z, 0, gridSizeZ - 1);

        return new int3(x, y, z);
    }

    private Vector3 GridToWorldPosition(int3 gridPosition)
    {
        // Convert grid indices to world position
        return transform.position - new Vector3(
            gridSizeX * cellSize * 0.5f,
            gridSizeY * cellSize * 0.5f,
            gridSizeZ * cellSize * 0.5f
        ) + new Vector3(
            (gridPosition.x + 0.5f) * cellSize,
            (gridPosition.y + 0.5f) * cellSize,
            (gridPosition.z + 0.5f) * cellSize
        );
    }

    private int GridPositionToIndex(int3 gridPosition)
    {
        // Convert 3D grid position to 1D array index
        return gridPosition.x + gridPosition.y * gridSizeX + gridPosition.z * gridSizeX * gridSizeY;
    }

    private void OnDestroy()
    {
        // Clean up native arrays
        if (fieldStrength.IsCreated) fieldStrength.Dispose();
        if (previousFieldStrength.IsCreated) previousFieldStrength.Dispose();
        if (mediumType.IsCreated) mediumType.Dispose();

        // Clean up visualization resources
        if (waveBuffer != null) waveBuffer.Dispose();
    }
}

// Job for processing wave propagation
[BurstCompile]
public struct WavePropagationJob : IJobParallelFor
{
    public int GridSizeX;
    public int GridSizeY;
    public int GridSizeZ;

    [ReadOnly] public NativeArray<float> PreviousFieldStrength;
    [ReadOnly] public NativeArray<int> MediumType;
    public NativeArray<float> FieldStrength;

    public float TimeStep;
    public float CellSize;
    public float PropagationSpeed;

    public void Execute(int index)
    {
        // Skip solid objects (they don't propagate waves internally)
        if (MediumType[index] != 0)
            return;

        // Convert 1D index to 3D position
        int z = index / (GridSizeX * GridSizeY);
        int remainder = index % (GridSizeX * GridSizeY);
        int y = remainder / GridSizeX;
        int x = remainder % GridSizeX;

        // Skip boundaries
        if (x == 0 || x == GridSizeX - 1 ||
            y == 0 || y == GridSizeY - 1 ||
            z == 0 || z == GridSizeZ - 1)
            return;

        // Get neighbor indices
        int left = index - 1;
        int right = index + 1;
        int down = index - GridSizeX;
        int up = index + GridSizeX;
        int back = index - GridSizeX * GridSizeY;
        int forward = index + GridSizeX * GridSizeY;

        // Calculate wave propagation using the wave equation
        // ?²u/?t² = c² * (?²u/?x² + ?²u/?y² + ?²u/?z²)

        // Laplacian part (spatial second derivative)
        float laplacian = 0;

        // Add contribution from each direction, accounting for material boundaries
        for (int i = 0; i < 6; i++)
        {
            int neighborIndex;
            float materialCoefficient = 1.0f;

            switch (i)
            {
                case 0: neighborIndex = left; break;
                case 1: neighborIndex = right; break;
                case 2: neighborIndex = down; break;
                case 3: neighborIndex = up; break;
                case 4: neighborIndex = back; break;
                case 5: neighborIndex = forward; break;
                default: neighborIndex = index; break;
            }

            // Check material type of neighbor
            int neighborMaterial = MediumType[neighborIndex];

            // Adjust coefficient based on material properties
            // This simulates reflection, transmission, and absorption
            switch (neighborMaterial)
            {
                case 0: // Air
                    materialCoefficient = 1.0f;
                    break;
                case 1: // Generic solid - mostly blocks waves
                    materialCoefficient = 0.1f;
                    break;
                case 2: // Metal - reflects waves
                    materialCoefficient = -0.9f; // Negative for reflection
                    break;
                case 3: // Glass - partial transmission
                    materialCoefficient = 0.7f;
                    break;
                case 4: // Wood - absorption
                    materialCoefficient = 0.3f;
                    break;
                default:
                    materialCoefficient = 0.0f; // Unknown material blocks completely
                    break;
            }

            // Add contribution to laplacian
            laplacian += materialCoefficient * PreviousFieldStrength[neighborIndex];
        }

        // Final laplacian calculation
        laplacian = laplacian - 6 * PreviousFieldStrength[index];
        laplacian /= (CellSize * CellSize);

        // Wave equation time integration
        // Using the discrete form of the wave equation:
        // u(t+dt) = 2*u(t) - u(t-dt) + c²*dt²*laplacian
        float waveSpeed = PropagationSpeed;

        // Adjust wave speed based on medium type
        switch (MediumType[index])
        {
            case 0: // Air
                waveSpeed = PropagationSpeed;
                break;
            case 1: // Generic solid
                waveSpeed = PropagationSpeed * 0.5f;
                break;
            case 2: // Metal
                waveSpeed = PropagationSpeed * 0.1f;
                break;
            case 3: // Glass
                waveSpeed = PropagationSpeed * 0.7f;
                break;
            case 4: // Wood
                waveSpeed = PropagationSpeed * 0.4f;
                break;
        }

        // Apply wave equation
        float dtSquared = TimeStep * TimeStep;
        FieldStrength[index] = 2 * PreviousFieldStrength[index] - FieldStrength[index]
                             + dtSquared * waveSpeed * waveSpeed * laplacian;

        // Add damping/attenuation to prevent unbounded oscillation
        FieldStrength[index] *= 0.9999f;
    }
}

