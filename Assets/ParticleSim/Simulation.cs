using Random = UnityEngine.Random;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

public class OptimizedParticleSimulation : MonoBehaviour
{
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

    [Header("Simulation Settings")]
    [Range(0f, 5f)] public float simulationSpeed = 1.0f;
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
    public bool useJobSystem = true; // Toggle for Jobs System

    [Header("Particle Types")]
    public List<ParticleType> particleTypes = new List<ParticleType>();

    [Header("Interaction Rules")]
    public List<InteractionRule> interactionRules = new List<InteractionRule>();

    [Header("Particle Generation")]
    public GameObject particlePrefab;

    // Runtime variables
    private List<SimulationParticle> particles = new List<SimulationParticle>();
    private Dictionary<(int, int), float> interactionLookup = new Dictionary<(int, int), float>();
    private SpatialGrid spatialGrid;

    // Native arrays for Jobs System
    private NativeArray<ParticleData> particleDataArray;
    private NativeArray<float> interactionMatrix;
    private NativeArray<float3> forceArray;
    private NativeArray<ParticleData> tempParticleDataArray;

    // Cached transform references for performance
    private Transform[] particleTransforms;

    // Helper method for editor
    public int GetParticleCount()
    {
        return particles.Count;
    }

    void Start()
    {
        // Build interaction lookup table for quick access
        foreach (var rule in interactionRules)
        {
            interactionLookup[(rule.typeIndexA, rule.typeIndexB)] = rule.attractionValue;
            // REMOVE THIS LINE:
            // interactionLookup[(rule.typeIndexB, rule.typeIndexA)] = rule.attractionValue;
        }

        // Initialize spatial grid if enabled
        if (useGridPartitioning)
        {
            spatialGrid = new SpatialGrid(simulationBounds, cellSize);
        }

        // Spawn initial particles
        SpawnParticles();

        // Initialize the Jobs System arrays if enabled
        if (useJobSystem)
        {
            InitializeJobsSystem();
        }
    }

    void OnDestroy()
    {
        // Clean up native arrays
        if (particleDataArray.IsCreated) particleDataArray.Dispose();
        if (tempParticleDataArray.IsCreated) tempParticleDataArray.Dispose(); // Clean up temp array
        if (interactionMatrix.IsCreated) interactionMatrix.Dispose();
        if (forceArray.IsCreated) forceArray.Dispose();
    }

    void Update()
    {
        Time.timeScale = simulationSpeed;

        if (useJobSystem)
        {
            // Update data arrays from Unity objects
            UpdateParticleDataArray();

            // Run Jobs
            RunSimulationJobs(Time.deltaTime);

            // Apply results back to Unity objects
            ApplyJobResults();
        }
        else
        {
            // Use traditional update method
            UpdateParticles(Time.deltaTime);
        }
    }

    private void SpawnParticles()
    {
        for (int typeIndex = 0; typeIndex < particleTypes.Count; typeIndex++)
        {
            var type = particleTypes[typeIndex];

            for (int i = 0; i < type.spawnAmount; i++)
            {
                GameObject particleObj = Instantiate(particlePrefab, transform);

                // Set random position within bounds
                Vector3 randomPos = new Vector3(
                    Random.Range(-simulationBounds.x / 2, simulationBounds.x / 2),
                    Random.Range(-simulationBounds.y / 2, simulationBounds.y / 2),
                    Random.Range(-simulationBounds.z / 2, simulationBounds.z / 2)
                );

                particleObj.transform.position = randomPos;

                // Add and configure particle component
                SimulationParticle particle = particleObj.AddComponent<SimulationParticle>();
                particle.typeIndex = typeIndex;
                particle.mass = type.mass;
                particle.radius = type.radius;

                // Set visual properties
                Renderer renderer = particleObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = type.color;
                    particleObj.transform.localScale = Vector3.one * type.radius * 2;  // Diameter
                }

                // Give particle a name for debugging
                particleObj.name = $"Particle_{type.name}_{i}";

                // Add to our list
                particles.Add(particle);
            }
        }

        // Cache transform references
        particleTransforms = new Transform[particles.Count];
        for (int i = 0; i < particles.Count; i++)
        {
            particleTransforms[i] = particles[i].transform;
        }
    }

    private void InitializeJobsSystem()
    {
        int particleCount = particles.Count;
        int typeCount = particleTypes.Count;

        // Create native arrays
        particleDataArray = new NativeArray<ParticleData>(particleCount, Allocator.Persistent);
        tempParticleDataArray = new NativeArray<ParticleData>(particleCount, Allocator.Persistent); // Add temp array
        interactionMatrix = new NativeArray<float>(typeCount * typeCount, Allocator.Persistent);
        forceArray = new NativeArray<float3>(particleCount, Allocator.Persistent);

        // Fill interaction matrix (no longer assuming symmetry)
        for (int i = 0; i < typeCount; i++)
        {
            for (int j = 0; j < typeCount; j++)
            {
                float attraction = 0f;
                if (interactionLookup.TryGetValue((i, j), out float value))
                {
                    attraction = value;
                }
                interactionMatrix[i + j * typeCount] = attraction;
            }
        }

        // Initial population of particle data
        UpdateParticleDataArray();
    }

    private void UpdateParticleDataArray()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var particle = particles[i];
            float3 position = particle.transform.position;

            particleDataArray[i] = new ParticleData
            {
                position = position,
                velocity = particle.velocity,
                typeIndex = particle.typeIndex,
                mass = particle.mass,
                radius = particle.radius
            };
        }
    }

    private void ApplyJobResults()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var data = particleDataArray[i];
            particles[i].velocity = data.velocity;
            particleTransforms[i].position = data.position;
        }
    }

    private void RunSimulationJobs(float deltaTime)
    {
        // Calculate forces
        var forceJob = new ParticleForceJob
        {
            particles = particleDataArray,
            interactionMatrix = interactionMatrix,
            typeCount = particleTypes.Count,
            interactionStrength = interactionStrength,
            minDistance = minDistance,
            maxForce = maxForce,
            interactionRadius = interactionRadius,
            forces = forceArray
        };

        // Update positions
        var updateJob = new ParticleUpdateJob
        {
            forces = forceArray,
            deltaTime = deltaTime,
            dampening = dampening,
            halfBounds = simulationBounds * 0.5f,
            bounceForce = bounceForce,
            maxVelocity = maxVelocity,
            particles = particleDataArray
        };

        // Schedule and complete force and update jobs
        var forceHandle = forceJob.Schedule(particles.Count, 64);
        var updateHandle = updateJob.Schedule(particles.Count, 64, forceHandle);
        updateHandle.Complete();

        // Copy initial data to temp array
        tempParticleDataArray.CopyFrom(particleDataArray);

        // Track which array has the most recent data
        bool finalDataInMainArray = true;

        // Run the collision job for a few iterations to resolve penetrations
        int collisionIterations = 3; // More iterations = more stable but slower

        // Run collision iterations with double buffering
        for (int i = 0; i < collisionIterations; i++)
        {
            var collisionJob = new ParticleCollisionJob
            {
                inputParticles = finalDataInMainArray ? particleDataArray : tempParticleDataArray,
                outputParticles = finalDataInMainArray ? tempParticleDataArray : particleDataArray,
                minDistance = minDistance,
                elasticity = collisionElasticity
            };

            // Complete the job
            collisionJob.Schedule(particles.Count, 64).Complete();

            // Toggle which array has the latest data
            finalDataInMainArray = !finalDataInMainArray;
        }

        // If final data is in temp array, copy back to main array
        if (!finalDataInMainArray)
        {
            particleDataArray.CopyFrom(tempParticleDataArray);
        }
    }

    private void UpdateParticles(float deltaTime)
    {
        // Update spatial grid if enabled
        if (useGridPartitioning)
        {
            spatialGrid.UpdateGrid(particles);
        }

        // Calculate all forces
        foreach (var particleA in particles)
        {
            Vector3 totalForce = Vector3.zero;

            // Get particles to check against (all or just nearby)
            List<SimulationParticle> particlesToCheck;
            if (useGridPartitioning)
            {
                particlesToCheck = spatialGrid.GetNearbyParticles(particleA.transform.position, interactionRadius);
            }
            else
            {
                particlesToCheck = particles;
            }

            // Check interaction with relevant particles
            foreach (var particleB in particlesToCheck)
            {
                if (particleA == particleB) continue;

                // Calculate direction and distance
                Vector3 direction = particleB.transform.position - particleA.transform.position;
                float distance = direction.magnitude;

                // Skip if too far away
                if (distance > interactionRadius) continue;

                // Prevent division by zero or extreme forces
                if (distance < minDistance) distance = minDistance;

                // Get attraction value from lookup
                float attraction = 0f;
                if (interactionLookup.TryGetValue((particleA.typeIndex, particleB.typeIndex), out float value))
                {
                    attraction = value;
                }

                // Calculate force magnitude (inverse square law with safety cap)
                float forceMagnitude = (attraction * interactionStrength) / (distance * distance);

                // Cap the force magnitude to prevent extreme values
                forceMagnitude = Mathf.Clamp(forceMagnitude, -maxForce, maxForce);

                // Apply force in the right direction
                totalForce += direction.normalized * forceMagnitude;
            }

            // Apply force as acceleration (F = ma, so a = F/m)
            Vector3 acceleration = totalForce / particleA.mass;

            // Cap acceleration to prevent numerical instability
            float maxAccel = 50f;
            if (acceleration.sqrMagnitude > maxAccel * maxAccel)
            {
                acceleration = acceleration.normalized * maxAccel;
            }

            particleA.velocity += acceleration * deltaTime;

            // Cap velocity to prevent numerical instability
            if (particleA.velocity.sqrMagnitude > maxVelocity * maxVelocity)
            {
                particleA.velocity = particleA.velocity.normalized * maxVelocity;
            }
        }

        // First pass: Update positions
        foreach (var particle in particles)
        {
            // Apply dampening
            particle.velocity *= dampening;

            // Update position
            particle.transform.position += particle.velocity * deltaTime;
        }

        // Second pass: Resolve collisions between particles
        for (int i = 0; i < particles.Count; i++)
        {
            var particleA = particles[i];
            Vector3 posA = particleA.transform.position;
            float radiusA = particleA.radius;

            // Get particles to check against (all or just nearby)
            List<SimulationParticle> particlesToCheck;
            if (useGridPartitioning)
            {
                particlesToCheck = spatialGrid.GetNearbyParticles(posA, radiusA * 2f);
            }
            else
            {
                particlesToCheck = particles;
            }

            foreach (var particleB in particlesToCheck)
            {
                if (particleA == particleB) continue;

                Vector3 posB = particleB.transform.position;
                float radiusB = particleB.radius;

                // Calculate overlap
                Vector3 direction = posB - posA;
                float distance = direction.magnitude;
                float minDistance = radiusA + radiusB;

                // If particles are overlapping
                if (distance < minDistance && distance > 0.001f)
                {
                    // Calculate penetration depth
                    float penetrationDepth = minDistance - distance;
                    Vector3 normal = direction.normalized;

                    // Calculate separation based on inverse mass ratio
                    float totalMass = particleA.mass + particleB.mass;
                    float ratioA = particleB.mass / totalMass;
                    float ratioB = particleA.mass / totalMass;

                    // Move particles apart
                    particleA.transform.position -= normal * penetrationDepth * ratioA;
                    particleB.transform.position += normal * penetrationDepth * ratioB;

                    // Optional: Apply collision response (elasticity)
                    float elasticity = 0.5f; // 0 = inelastic, 1 = perfectly elastic

                    // Calculate relative velocity
                    Vector3 relativeVelocity = particleB.velocity - particleA.velocity;

                    // Calculate impulse
                    float impulse = (-(1 + elasticity) * Vector3.Dot(relativeVelocity, normal)) /
                                   (1 / particleA.mass + 1 / particleB.mass);

                    // Apply impulse
                    particleA.velocity -= normal * (impulse / particleA.mass);
                    particleB.velocity += normal * (impulse / particleB.mass);
                }
            }
        }

        // Update positions and handle collisions
        foreach (var particle in particles)
        {
            // Check boundaries and bounce if needed
            Vector3 position = particle.transform.position;
            Vector3 halfBounds = simulationBounds / 2;

            // X boundaries
            if (position.x < -halfBounds.x + particle.radius)
            {
                position.x = -halfBounds.x + particle.radius;
                particle.velocity.x = -particle.velocity.x * bounceForce;
            }
            else if (position.x > halfBounds.x - particle.radius)
            {
                position.x = halfBounds.x - particle.radius;
                particle.velocity.x = -particle.velocity.x * bounceForce;
            }

            // Y boundaries
            if (position.y < -halfBounds.y + particle.radius)
            {
                position.y = -halfBounds.y + particle.radius;
                particle.velocity.y = -particle.velocity.y * bounceForce;
            }
            else if (position.y > halfBounds.y - particle.radius)
            {
                position.y = halfBounds.y - particle.radius;
                particle.velocity.y = -particle.velocity.y * bounceForce;
            }

            // Z boundaries
            if (position.z < -halfBounds.z + particle.radius)
            {
                position.z = -halfBounds.z + particle.radius;
                particle.velocity.z = -particle.velocity.z * bounceForce;
            }
            else if (position.z > halfBounds.z - particle.radius)
            {
                position.z = halfBounds.z - particle.radius;
                particle.velocity.z = -particle.velocity.z * bounceForce;
            }

            // Apply corrected position
            particle.transform.position = position;
        }
    }

    void OnDrawGizmos()
    {
        // Draw the simulation bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, simulationBounds);
    }
}

public class SpatialGrid
{
    private List<SimulationParticle>[] cells;
    private Vector3 gridWorldSize;
    private Vector3 cellSize;
    private int gridSizeX, gridSizeY, gridSizeZ;

    public SpatialGrid(Vector3 worldSize, float cellSize)
    {
        this.gridWorldSize = worldSize;
        this.cellSize = new Vector3(cellSize, cellSize, cellSize);

        gridSizeX = Mathf.CeilToInt(worldSize.x / cellSize);
        gridSizeY = Mathf.CeilToInt(worldSize.y / cellSize);
        gridSizeZ = Mathf.CeilToInt(worldSize.z / cellSize);

        int totalCells = gridSizeX * gridSizeY * gridSizeZ;
        cells = new List<SimulationParticle>[totalCells];

        for (int i = 0; i < totalCells; i++)
        {
            cells[i] = new List<SimulationParticle>();
        }
    }

    public void UpdateGrid(List<SimulationParticle> particles)
    {
        // Clear all cells
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].Clear();
        }

        // Add particles to appropriate cells
        foreach (var particle in particles)
        {
            int cellIndex = GetCellIndex(particle.transform.position);
            if (cellIndex >= 0 && cellIndex < cells.Length)
            {
                cells[cellIndex].Add(particle);
            }
        }
    }

    public List<SimulationParticle> GetNearbyParticles(Vector3 position, float radius)
    {
        List<SimulationParticle> nearbyParticles = new List<SimulationParticle>();
        Vector3 halfWorldSize = gridWorldSize * 0.5f;

        // Calculate the cell indices for the specified radius
        Vector3Int minCellPos = WorldToCell(position - new Vector3(radius, radius, radius) + halfWorldSize);
        Vector3Int maxCellPos = WorldToCell(position + new Vector3(radius, radius, radius) + halfWorldSize);

        // Clamp to grid boundaries
        minCellPos.x = Mathf.Max(0, minCellPos.x);
        minCellPos.y = Mathf.Max(0, minCellPos.y);
        minCellPos.z = Mathf.Max(0, minCellPos.z);

        maxCellPos.x = Mathf.Min(gridSizeX - 1, maxCellPos.x);
        maxCellPos.y = Mathf.Min(gridSizeY - 1, maxCellPos.y);
        maxCellPos.z = Mathf.Min(gridSizeZ - 1, maxCellPos.z);

        // Iterate through all cells in the specified range
        for (int x = minCellPos.x; x <= maxCellPos.x; x++)
        {
            for (int y = minCellPos.y; y <= maxCellPos.y; y++)
            {
                for (int z = minCellPos.z; z <= maxCellPos.z; z++)
                {
                    int cellIndex = GetCellIndex(x, y, z);
                    nearbyParticles.AddRange(cells[cellIndex]);
                }
            }
        }

        return nearbyParticles;
    }

    private Vector3Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 normalizedPos = (worldPosition + gridWorldSize * 0.5f);
        normalizedPos.x /= cellSize.x;
        normalizedPos.y /= cellSize.y;
        normalizedPos.z /= cellSize.z;
        return new Vector3Int(
            Mathf.FloorToInt(normalizedPos.x),
            Mathf.FloorToInt(normalizedPos.y),
            Mathf.FloorToInt(normalizedPos.z)
        );
    }

    private int GetCellIndex(Vector3 worldPosition)
    {
        Vector3 halfWorldSize = gridWorldSize * 0.5f;
        Vector3 adjustedPos = worldPosition + halfWorldSize;

        // Check if position is within grid bounds
        if (adjustedPos.x < 0 || adjustedPos.x >= gridWorldSize.x ||
            adjustedPos.y < 0 || adjustedPos.y >= gridWorldSize.y ||
            adjustedPos.z < 0 || adjustedPos.z >= gridWorldSize.z)
        {
            return -1;
        }

        Vector3Int cellPos = WorldToCell(worldPosition);
        return GetCellIndex(cellPos.x, cellPos.y, cellPos.z);
    }

    private int GetCellIndex(int x, int y, int z)
    {
        return x + y * gridSizeX + z * gridSizeX * gridSizeY;
    }
}

[System.Serializable]
public struct ParticleData
{
    public float3 position;
    public float3 velocity;
    public int typeIndex;
    public float mass;
    public float radius;
}

[BurstCompile]
public struct ParticleForceJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<ParticleData> particles;
    [ReadOnly] public NativeArray<float> interactionMatrix;
    [ReadOnly] public int typeCount;
    [ReadOnly] public float interactionStrength;
    [ReadOnly] public float minDistance;
    [ReadOnly] public float maxForce;
    [ReadOnly] public float interactionRadius;

    public NativeArray<float3> forces;

    public void Execute(int index)
    {
        ParticleData particleA = particles[index];
        float3 totalForce = float3.zero;

        for (int i = 0; i < particles.Length; i++)
        {
            if (i == index) continue;

            ParticleData particleB = particles[i];

            // Calculate direction and distance
            float3 direction = particleB.position - particleA.position;
            float distance = math.length(direction);

            // Skip if too far away (optimization)
            if (distance > interactionRadius) continue;

            // Prevent division by zero or extreme forces
            if (distance < minDistance) distance = minDistance;

            // Get attraction value from flattened 2D matrix
            int lookupIndex = particleA.typeIndex + particleB.typeIndex * typeCount;
            float attraction = interactionMatrix[lookupIndex];

            // Calculate force magnitude (inverse square law with safety cap)
            float forceMagnitude = (attraction * interactionStrength) / (distance * distance);

            // Cap the force magnitude to prevent extreme values
            forceMagnitude = math.clamp(forceMagnitude, -maxForce, maxForce);

            // Apply force in the right direction
            float3 normalizedDir = math.normalizesafe(direction);
            totalForce += normalizedDir * forceMagnitude;
        }

        forces[index] = totalForce;
    }
}

[BurstCompile]
public struct ParticleUpdateJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> forces;
    [ReadOnly] public float deltaTime;
    [ReadOnly] public float dampening;
    [ReadOnly] public float3 halfBounds;
    [ReadOnly] public float bounceForce;
    [ReadOnly] public float maxVelocity;

    public NativeArray<ParticleData> particles;

    public void Execute(int index)
    {
        ParticleData particle = particles[index];

        // Apply force as acceleration (F = ma, so a = F/m)
        float3 acceleration = forces[index] / particle.mass;

        // Cap acceleration to prevent numerical instability
        float maxAccel = 50f;
        if (math.lengthsq(acceleration) > maxAccel * maxAccel)
        {
            acceleration = math.normalizesafe(acceleration) * maxAccel;
        }

        // Update velocity
        particle.velocity += acceleration * deltaTime;

        // Apply dampening
        particle.velocity *= dampening;

        // Cap velocity to prevent numerical instability
        if (math.lengthsq(particle.velocity) > maxVelocity * maxVelocity)
        {
            particle.velocity = math.normalizesafe(particle.velocity) * maxVelocity;
        }

        // Update position
        particle.position += particle.velocity * deltaTime;

        // Check boundaries and bounce if needed
        float3 position = particle.position;

        // X boundaries
        if (position.x < -halfBounds.x + particle.radius)
        {
            position.x = -halfBounds.x + particle.radius;
            particle.velocity.x = -particle.velocity.x * bounceForce;
        }
        else if (position.x > halfBounds.x - particle.radius)
        {
            position.x = halfBounds.x - particle.radius;
            particle.velocity.x = -particle.velocity.x * bounceForce;
        }

        // Y boundaries
        if (position.y < -halfBounds.y + particle.radius)
        {
            position.y = -halfBounds.y + particle.radius;
            particle.velocity.y = -particle.velocity.y * bounceForce;
        }
        else if (position.y > halfBounds.y - particle.radius)
        {
            position.y = halfBounds.y - particle.radius;
            particle.velocity.y = -particle.velocity.y * bounceForce;
        }

        // Z boundaries
        if (position.z < -halfBounds.z + particle.radius)
        {
            position.z = -halfBounds.z + particle.radius;
            particle.velocity.z = -particle.velocity.z * bounceForce;
        }
        else if (position.z > halfBounds.z - particle.radius)
        {
            position.z = halfBounds.z - particle.radius;
            particle.velocity.z = -particle.velocity.z * bounceForce;
        }

        // Update particle with new values
        particle.position = position;
        particles[index] = particle;
    }
}

[BurstCompile]
public struct ParticleCollisionJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<ParticleData> inputParticles;
    public NativeArray<ParticleData> outputParticles;
    [ReadOnly] public float minDistance;
    [ReadOnly] public float elasticity;

    public void Execute(int indexA)
    {
        // Start with the input data for our output
        ParticleData particleA = inputParticles[indexA];
        outputParticles[indexA] = particleA;

        float3 posA = particleA.position;
        float radiusA = particleA.radius;

        for (int indexB = 0; indexB < inputParticles.Length; indexB++)
        {
            if (indexA == indexB) continue;

            ParticleData particleB = inputParticles[indexB];
            float3 posB = particleB.position;
            float radiusB = particleB.radius;

            // Calculate overlap
            float3 direction = posB - posA;
            float distance = math.length(direction);
            float collisionDistance = radiusA + radiusB;

            // If particles are overlapping
            if (distance < collisionDistance && distance > 0.001f)
            {
                // Get our current output particle
                ParticleData outputParticle = outputParticles[indexA];

                // Calculate penetration depth
                float penetrationDepth = collisionDistance - distance;
                float3 normal = math.normalize(direction);

                // Calculate separation based on inverse mass ratio
                float totalMass = particleA.mass + particleB.mass;
                float ratioA = particleB.mass / totalMass;

                // Apply position correction
                outputParticle.position -= normal * penetrationDepth * ratioA;

                // Apply collision response (elasticity)
                float3 relativeVelocity = particleB.velocity - particleA.velocity;
                float impulse = (-(1 + elasticity) * math.dot(relativeVelocity, normal)) /
                               (1 / particleA.mass + 1 / particleB.mass);

                // Apply impulse to our particle only
                outputParticle.velocity -= normal * (impulse / particleA.mass);

                // Update the output particle
                outputParticles[indexA] = outputParticle;
            }
        }
    }
}