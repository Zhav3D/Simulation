using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class GPUParticleSimulation : MonoBehaviour
{
    // Similar serializable classes from the original implementation
    [System.Serializable]
    public class ParticleType
    {
        public string name;
        public Color color = Color.white;
        public float mass = 1f;
        public float radius = 0.5f;
        public float spawnAmount = 50;
    }

    [System.Serializable]
    public class InteractionRule
    {
        public int typeIndexA;
        public int typeIndexB;
        public float attractionValue; // Positive for attraction, negative for repulsion
    }

    // Simulation settings (similar to the original)
    [Header("Simulation Settings")]
    [SerializeField, Range(0f, 5f)] private float simulationSpeed = 1.0f;
    [Range(0f, 1f)] public float collisionElasticity = 0.5f;
    public Vector3 simulationBounds = new Vector3(10f, 10f, 10f);
    public float dampening = 0.95f; // Air resistance / friction
    public float interactionStrength = 1f; // Global multiplier for interaction forces
    public float minDistance = 0.5f; // Minimum distance to prevent extreme forces
    public float bounceForce = 0.8f; // How much velocity is preserved on collision with boundaries
    public float maxForce = 100f; // Maximum force magnitude to prevent instability
    public float maxVelocity = 20f; // Maximum velocity magnitude to prevent instability
    public float interactionRadius = 10f; // Maximum distance for particle interactions

    [Header("Spatial Partitioning")]
    public float cellSize = 2.5f; // Size of each grid cell, should be >= interactionRadius/2
    public bool useGridPartitioning = true; // Toggle for spatial grid

    [Header("Particle Types")]
    public List<ParticleType> particleTypes = new List<ParticleType>();

    [Header("Interaction Rules")]
    public List<InteractionRule> interactionRules = new List<InteractionRule>();

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public Material particleMaterial;
    public ComputeShader clearGridShader;
    public ComputeShader updateGridShader;
    public ComputeShader forceShader;
    public ComputeShader integrationShader;
    public ComputeShader collisionShader;
    public ComputeShader countingShader;

    // GPU Buffer related fields
    private ComputeBuffer particleBuffer;
    private ComputeBuffer typesBuffer;
    private ComputeBuffer interactionMatrixBuffer;
    private ComputeBuffer gridCellBuffer;
    private ComputeBuffer gridOccupancyBuffer;
    private ComputeBuffer gridCounterBuffer;
    private ComputeBuffer indirectArgsBuffer;

    // Rendering fields
    private MaterialPropertyBlock propertyBlock;
    private int particleCount;
    private int collisionIterations = 3;
    private int[] gridSize;
    private int gridCellCount;

    // Shader kernel IDs
    private int kernelClearGrid;
    private int kernelUpdateGrid;
    private int kernelCalculateForces;
    private int kernelIntegration;
    private int kernelCollision;
    private int kernelCountType;

    // Helper method for editor
    public int GetParticleCount()
    {
        return particleCount;
    }

    void Start()
    {
        InitializeKernels();
        InitializeParticles();
        InitializeGrid();
        InitializeRenderingResources();
    }

    void InitializeKernels()
    {
        kernelClearGrid = clearGridShader.FindKernel("ClearGrid");
        kernelUpdateGrid = updateGridShader.FindKernel("UpdateGrid");
        kernelCalculateForces = forceShader.FindKernel("CalculateForces");
        kernelIntegration = integrationShader.FindKernel("Integration");
        kernelCollision = collisionShader.FindKernel("Collision");
        kernelCountType = countingShader.FindKernel("CountParticleType");
    }

    void InitializeParticles()
    {
        // Calculate total particle count
        particleCount = 0;
        foreach (var type in particleTypes)
        {
            particleCount += Mathf.FloorToInt(type.spawnAmount);
        }

        // Create and initialize particle buffer
        particleBuffer = new ComputeBuffer(particleCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUParticleData)));

        // Create array for initial particle data
        GPUParticleData[] particleData = new GPUParticleData[particleCount];
        int particleIndex = 0;

        // Initialize particle data
        for (int typeIndex = 0; typeIndex < particleTypes.Count; typeIndex++)
        {
            var type = particleTypes[typeIndex];
            int c = Mathf.FloorToInt(type.spawnAmount);

            for (int i = 0; i < c; i++)
            {
                // Set random position within bounds
                Vector3 randomPos = new Vector3(
                    Random.Range(-simulationBounds.x / 2, simulationBounds.x / 2),
                    Random.Range(-simulationBounds.y / 2, simulationBounds.y / 2),
                    Random.Range(-simulationBounds.z / 2, simulationBounds.z / 2)
                );

                particleData[particleIndex] = new GPUParticleData
                {
                    position = randomPos,
                    velocity = Vector3.zero,
                    force = Vector3.zero,
                    typeIndex = typeIndex,
                    mass = type.mass,
                    radius = type.radius
                };

                particleIndex++;
            }
        }

        // Upload initial data to GPU
        particleBuffer.SetData(particleData);

        // Initialize types buffer
        typesBuffer = new ComputeBuffer(particleTypes.Count, sizeof(float) * 6); // 4 floats for color, 1 for mass, 1 for radius
        GPUParticleType[] types = new GPUParticleType[particleTypes.Count];

        for (int i = 0; i < particleTypes.Count; i++)
        {
            types[i] = new GPUParticleType
            {
                color = particleTypes[i].color,
                mass = particleTypes[i].mass,
                radius = particleTypes[i].radius
            };
        }

        typesBuffer.SetData(types);

        // Create and initialize interaction matrix buffer
        int typeCount = particleTypes.Count;
        interactionMatrixBuffer = new ComputeBuffer(typeCount * typeCount, sizeof(float));
        float[] interactionMatrix = new float[typeCount * typeCount];

        // Fill interaction matrix (no longer assuming symmetry, as in original code)
        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                interactionMatrix[i + j * typeCount] = 0f; // Default: no interaction
            }
        }

        // Apply interaction rules from the inspector
        foreach (var rule in interactionRules)
        {
            interactionMatrix[rule.typeIndexA + rule.typeIndexB * typeCount] = rule.attractionValue;
        }

        interactionMatrixBuffer.SetData(interactionMatrix);
    }

    void InitializeGrid()
    {
        if (!useGridPartitioning) return;

        // Calculate grid dimensions based on simulation bounds and cell size
        gridSize = new int[3]
        {
            Mathf.CeilToInt(simulationBounds.x / cellSize),
            Mathf.CeilToInt(simulationBounds.y / cellSize),
            Mathf.CeilToInt(simulationBounds.z / cellSize)
        };
        gridCellCount = gridSize[0] * gridSize[1] * gridSize[2];

        // Create buffers for grid data
        gridCellBuffer = new ComputeBuffer(gridCellCount, sizeof(int) * 2); // startIndex and count per cell
        gridOccupancyBuffer = new ComputeBuffer(particleCount, sizeof(int) * 2); // cellIndex and particleIndex
        gridCounterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Counter);

        // Initialize grid cell buffer
        GridCellData[] cellData = new GridCellData[gridCellCount];
        for (int i = 0; i < gridCellCount; i++)
        {
            cellData[i] = new GridCellData { startIndex = 0, count = 0 };
        }
        gridCellBuffer.SetData(cellData);
    }

    void InitializeRenderingResources()
    {
        // Create property block for instanced rendering
        propertyBlock = new MaterialPropertyBlock();

        // Create indirect draw args buffer for instanced rendering
        indirectArgsBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);

        // Set initial values for indirect arguments
        // Format: indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation
        if (particleMesh != null)
        {
            // Get the actual index count from the mesh
            int[] args = new int[5] { 0, 0, 0, 0, 0 };
            args[0] = (int)particleMesh.GetIndexCount(0);
            indirectArgsBuffer.SetData(args);
        }
        else
        {
            Debug.LogError("Particle mesh is not assigned!");
        }

        // Make sure the particle material has the shader assigned
        if (particleMaterial != null && particleMaterial.shader.name != "Custom/GPUParticleShader")
        {
            Debug.LogWarning("Particle material doesn't use the correct shader. Assigning GPUParticleShader.");
        }
    }

    void Update()
    {
        Time.timeScale = simulationSpeed;
        UpdateSimulation(Time.deltaTime);
    }

    void UpdateSimulation(float deltaTime)
    {
        if (useGridPartitioning)
        {
            UpdateGrid();
        }

        // Calculate forces
        CalculateForces();

        // Integrate positions and handle boundaries
        Integration(deltaTime);

        // Resolve collisions
        for (int i = 0; i < collisionIterations; i++)
        {
            ResolveCollisions();
        }
    }

    void UpdateGrid()
    {
        // Clear grid cell counts
        clearGridShader.SetBuffer(kernelClearGrid, "GridCellBuffer", gridCellBuffer);
        clearGridShader.SetInt("GridCellCount", gridCellCount);
        clearGridShader.Dispatch(kernelClearGrid, Mathf.CeilToInt(gridCellCount / 64f), 1, 1);

        // Reset counter for the grid occupancy buffer
        int[] resetCounterData = new int[] { 0 };
        gridCounterBuffer.SetData(resetCounterData);

        // Update grid
        updateGridShader.SetBuffer(kernelUpdateGrid, "ParticleBuffer", particleBuffer);
        updateGridShader.SetBuffer(kernelUpdateGrid, "GridCellBuffer", gridCellBuffer);
        updateGridShader.SetBuffer(kernelUpdateGrid, "GridOccupancyBuffer", gridOccupancyBuffer);
        updateGridShader.SetBuffer(kernelUpdateGrid, "GridCounterBuffer", gridCounterBuffer);
        updateGridShader.SetInt("ParticleCount", particleCount);
        updateGridShader.SetVector("GridDimensions", new Vector3(gridSize[0], gridSize[1], gridSize[2]));
        updateGridShader.SetVector("CellSize", new Vector3(cellSize, cellSize, cellSize));
        updateGridShader.SetVector("SimulationBounds", simulationBounds);
        updateGridShader.Dispatch(kernelUpdateGrid, Mathf.CeilToInt(particleCount / 64f), 1, 1);
    }

    void CalculateForces()
    {
        forceShader.SetBuffer(kernelCalculateForces, "ParticleBuffer", particleBuffer);
        forceShader.SetBuffer(kernelCalculateForces, "InteractionMatrix", interactionMatrixBuffer);

        if (useGridPartitioning)
        {
            forceShader.SetBuffer(kernelCalculateForces, "GridCellBuffer", gridCellBuffer);
            forceShader.SetBuffer(kernelCalculateForces, "GridOccupancyBuffer", gridOccupancyBuffer);
            forceShader.SetVector("GridDimensions", new Vector3(gridSize[0], gridSize[1], gridSize[2]));
            forceShader.SetVector("CellSize", new Vector3(cellSize, cellSize, cellSize));
            forceShader.SetVector("SimulationBounds", simulationBounds);
        }

        forceShader.SetInt("ParticleCount", particleCount);
        forceShader.SetInt("TypeCount", particleTypes.Count);
        forceShader.SetFloat("InteractionStrength", interactionStrength);
        forceShader.SetFloat("MinDistance", minDistance);
        forceShader.SetFloat("MaxForce", maxForce);
        forceShader.SetFloat("InteractionRadius", interactionRadius);
        forceShader.SetBool("UseGridPartitioning", useGridPartitioning);

        forceShader.Dispatch(kernelCalculateForces, Mathf.CeilToInt(particleCount / 64f), 1, 1);
    }

    void Integration(float deltaTime)
    {
        integrationShader.SetBuffer(kernelIntegration, "ParticleBuffer", particleBuffer);
        integrationShader.SetInt("ParticleCount", particleCount);
        integrationShader.SetFloat("DeltaTime", deltaTime);
        integrationShader.SetFloat("Dampening", dampening);
        integrationShader.SetFloat("BounceForce", bounceForce);
        integrationShader.SetFloat("MaxVelocity", maxVelocity);
        integrationShader.SetVector("HalfBounds", simulationBounds * 0.5f);

        integrationShader.Dispatch(kernelIntegration, Mathf.CeilToInt(particleCount / 64f), 1, 1);
    }

    void ResolveCollisions()
    {
        collisionShader.SetBuffer(kernelCollision, "ParticleBuffer", particleBuffer);

        if (useGridPartitioning)
        {
            collisionShader.SetBuffer(kernelCollision, "GridCellBuffer", gridCellBuffer);
            collisionShader.SetBuffer(kernelCollision, "GridOccupancyBuffer", gridOccupancyBuffer);
            collisionShader.SetVector("GridDimensions", new Vector3(gridSize[0], gridSize[1], gridSize[2]));
            collisionShader.SetVector("CellSize", new Vector3(cellSize, cellSize, cellSize));
            collisionShader.SetVector("SimulationBounds", simulationBounds);
        }

        collisionShader.SetInt("ParticleCount", particleCount);
        collisionShader.SetFloat("Elasticity", collisionElasticity);
        collisionShader.SetBool("UseGridPartitioning", useGridPartitioning);

        collisionShader.Dispatch(kernelCollision, Mathf.CeilToInt(particleCount / 64f), 1, 1);
    }

    void OnRenderObject()
    {
        RenderParticles();
    }

    void RenderParticles()
    {
        if (particleMesh == null || particleMaterial == null)
        {
            Debug.LogError("Cannot render particles: Mesh or Material is missing");
            return;
        }

        // Create property block if needed
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        // Set up material property block with particle data
        propertyBlock.SetBuffer("_ParticleBuffer", particleBuffer);
        propertyBlock.SetBuffer("_TypesBuffer", typesBuffer);

        // For each particle type, render all particles of that type
        for (int typeIndex = 0; typeIndex < particleTypes.Count; typeIndex++)
        {
            // Set up counting shader to count particles of this type
            countingShader.SetBuffer(kernelCountType, "ParticleBuffer", particleBuffer);
            countingShader.SetInt("TargetType", typeIndex);
            countingShader.SetBuffer(kernelCountType, "IndirectArgsBuffer", indirectArgsBuffer);
            countingShader.SetInt("ParticleCount", particleCount);
            countingShader.Dispatch(kernelCountType, 1, 1, 1);

            // Set type index for the shader
            propertyBlock.SetInt("_TypeIndex", typeIndex);

            // Draw all particles of this type in one batch
            Graphics.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                particleMaterial,
                new Bounds(Vector3.zero, simulationBounds * 2), // Double bounds size to ensure visibility
                indirectArgsBuffer,
                0,
                propertyBlock
            );
        }
    }

    void OnDestroy()
    {
        // Clean up buffers
        if (particleBuffer != null) particleBuffer.Release();
        if (typesBuffer != null) typesBuffer.Release();
        if (interactionMatrixBuffer != null) interactionMatrixBuffer.Release();
        if (gridCellBuffer != null) gridCellBuffer.Release();
        if (gridOccupancyBuffer != null) gridOccupancyBuffer.Release();
        if (gridCounterBuffer != null) gridCounterBuffer.Release();
        if (indirectArgsBuffer != null) indirectArgsBuffer.Release();
    }

    void OnDrawGizmos()
    {
        // Draw the simulation bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, simulationBounds);
    }

    // Structure definitions for GPU buffers
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct GPUParticleData
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 force;
        public int typeIndex;
        public float mass;
        public float radius;
        // Padding to ensure proper alignment with HLSL struct
        public float padding; // Make the struct size a multiple of 16 bytes
    }

    struct GPUParticleType
    {
        public Vector4 color;
        public float mass;
        public float radius;
    }

    struct GridCellData
    {
        public int startIndex;
        public int count;
    }
}