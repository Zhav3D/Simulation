using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class TardigradeSimulation2 : MonoBehaviour
{
    [Header("Simulation Settings")]
    public bool runSimulation = true;
    public float simulationSpeed = 1.0f;
    [Range(100, 50000)]
    public int cellCount = 5000;
    [Range(1, 16)]
    public int jobBatchSize = 4;

    [Header("Tardigrade Parameters")]
    public float tardigradeLength = 500f; // micrometers
    public float tardigradeWidth = 100f; // micrometers
    [Range(0.1f, 1.0f)]
    public float bodySegmentDistribution = 0.6f;
    public int legPairs = 4;

    [Header("Environmental Factors")]
    [Range(0f, 100f)]
    public float environmentalStress = 0f;
    [Range(-273f, 100f)]
    public float temperature = 20f;
    [Range(0f, 100f)]
    public float radiationLevel = 0f;
    [Range(0f, 100f)]
    public float hydrationLevel = 100f;
    [Range(1f, 1000f)]
    public float environmentalPressure = 1f;

    [Header("Visualization")]
    public bool showCells = true;
    public bool showOrganelles = true;
    public float cellScale = 1.0f;
    public Color cellColor = new Color(0.7f, 0.9f, 0.8f, 0.7f);
    public Color nucleusColor = new Color(0.4f, 0.5f, 0.9f, 0.9f);
    public Color stressedCellColor = new Color(0.9f, 0.3f, 0.3f, 0.7f);

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Simulation data structures
    private NativeArray<Cell> cells;
    private NativeArray<Organelle> organelles;
    private NativeArray<float3> cellPositions;
    private NativeArray<float3> cellVelocities;
    private NativeArray<float> cellHealth;
    private NativeArray<int> cellType;
    private NativeArray<float> cellEnergy;
    private NativeArray<quaternion> cellRotations;

    // Runtime data
    private List<GameObject> cellVisuals = new List<GameObject>();
    private bool isInitialized = false;
    private float timeSinceLastUpdate = 0f;
    private const float updateInterval = 0.05f;
    private float cryptobiosisLevel = 0f;
    private int activeGenomeSegments = 0;
    private int stressResponseProteins = 0;
    private float metabolicRate = 1.0f;

    // Constants
    private const float CELL_SIZE = 10f; // micrometers
    private const float MITOCHONDRIA_SIZE = 2f; // micrometers
    private const float NUCLEUS_SIZE = 5f; // micrometers
    private const float ENDOPLASMIC_RETICULUM_LENGTH = 8f; // micrometers
    private const int AVERAGE_CELL_ORGANELLES = 15;

    // Cell types
    private enum CellType
    {
        Epithelial = 0,
        Muscle = 1,
        Nerve = 2,
        Digestive = 3,
        Reproductive = 4
    }

    // Cell structure
    public struct Cell
    {
        public float3 position;
        public float3 velocity;
        public float size;
        public float health;
        public int type;
        public int organelleStartIndex;
        public int organelleCount;
        public float energy;
        public float stress;
        public quaternion rotation;
        public bool isUndergoing_Cryptobiosis;
    }

    // Organelle structure
    public struct Organelle
    {
        public float3 relativePosition;
        public float size;
        public int type;
        public float health;
        public float activity;
    }

    // Organelle types
    private enum OrganelleType
    {
        Nucleus = 0,
        Mitochondria = 1,
        Lysosome = 2,
        Ribosome = 3,
        Vacuole = 4,
        Golgi = 5,
        EndoplasmicReticulum = 6
    }

    // Tardigrade segmentation
    private struct BodySegment
    {
        public float3 position;
        public float3 size;
        public quaternion rotation;
    }

    private BodySegment[] bodySegments;
    private BodySegment[] legs;

    void OnEnable()
    {
        InitializeSimulation();
    }

    void OnDisable()
    {
        CleanupSimulation();
    }

    void Update()
    {
        if (!isInitialized)
        {
            InitializeSimulation();
            return;
        }

        if (!runSimulation)
            return;

        timeSinceLastUpdate += Time.deltaTime * simulationSpeed;

        if (timeSinceLastUpdate >= updateInterval)
        {
            timeSinceLastUpdate = 0f;
            RunSimulationStep();

            // Check for environmental stress responses
            if (hydrationLevel < 20f || temperature < 0f || temperature > 80f || radiationLevel > 50f || environmentalPressure > 500f)
            {
                InduceCryptobiosis();
            }
            else if (cryptobiosisLevel > 0f)
            {
                RevertCryptobiosis();
            }

            UpdateMetabolicRate();
            UpdateVisuals();
        }
    }

    private void InitializeSimulation()
    {
        if (isInitialized)
            CleanupSimulation();

        // Initialize native arrays
        cells = new NativeArray<Cell>(cellCount, Allocator.Persistent);
        organelles = new NativeArray<Organelle>(cellCount * AVERAGE_CELL_ORGANELLES, Allocator.Persistent);
        cellPositions = new NativeArray<float3>(cellCount, Allocator.Persistent);
        cellVelocities = new NativeArray<float3>(cellCount, Allocator.Persistent);
        cellHealth = new NativeArray<float>(cellCount, Allocator.Persistent);
        cellType = new NativeArray<int>(cellCount, Allocator.Persistent);
        cellEnergy = new NativeArray<float>(cellCount, Allocator.Persistent);
        cellRotations = new NativeArray<quaternion>(cellCount, Allocator.Persistent);

        // Create tardigrade body segments
        CreateTardigradeStructure();

        // Initialize cells within the tardigrade structure
        InitializeCells();

        isInitialized = true;

        // Initialize visualization
        if (showCells)
            CreateCellVisuals();
    }

    private void CreateTardigradeStructure()
    {
        // Create body segments
        int segmentCount = 5;
        bodySegments = new BodySegment[segmentCount];

        float segmentLength = tardigradeLength / segmentCount;
        float segmentWidth = tardigradeWidth;
        float segmentHeight = tardigradeWidth * 0.8f;

        // Create head and body segments
        for (int i = 0; i < segmentCount; i++)
        {
            bodySegments[i] = new BodySegment
            {
                position = new float3(
                    (i - segmentCount / 2) * segmentLength * 0.8f,  // Slight overlap
                    0f,
                    0f
                ),
                size = new float3(
                    segmentLength * (i == 0 ? 0.7f : 1f),  // Head is smaller
                    segmentWidth * (i == 0 ? 0.8f : (1f - (i * 0.05f))),  // Tapering width
                    segmentHeight * (i == 0 ? 0.8f : (1f - (i * 0.05f)))  // Tapering height
                ),
                rotation = quaternion.identity
            };
        }

        // Create legs
        legs = new BodySegment[legPairs * 2];
        float legLength = tardigradeWidth * 0.5f;
        float legWidth = tardigradeWidth * 0.15f;

        for (int i = 0; i < legPairs; i++)
        {
            float segmentIndex = 1 + i * ((segmentCount - 1) / (float)legPairs);
            int mainSegment = Mathf.FloorToInt(segmentIndex);
            float segmentOffset = segmentIndex - mainSegment;

            float3 segmentPos = bodySegments[mainSegment].position;
            if (segmentOffset > 0 && mainSegment < segmentCount - 1)
            {
                segmentPos = math.lerp(
                    bodySegments[mainSegment].position,
                    bodySegments[mainSegment + 1].position,
                    segmentOffset
                );
            }

            // Left leg
            legs[i * 2] = new BodySegment
            {
                position = segmentPos + new float3(0f, 0f, -tardigradeWidth * 0.4f),
                size = new float3(legWidth, legWidth, legLength),
                rotation = quaternion.EulerXYZ(0f, 0f, math.radians(-30f))
            };

            // Right leg
            legs[i * 2 + 1] = new BodySegment
            {
                position = segmentPos + new float3(0f, 0f, tardigradeWidth * 0.4f),
                size = new float3(legWidth, legWidth, legLength),
                rotation = quaternion.EulerXYZ(0f, 0f, math.radians(30f))
            };
        }
    }

    private void InitializeCells()
    {
        System.Random rand = new System.Random(42);
        int organelleIndex = 0;

        // Associate cells with body segments and legs
        for (int i = 0; i < cellCount; i++)
        {
            // Decide if this cell is in a body segment or leg
            bool isInBodySegment = rand.NextDouble() < bodySegmentDistribution;

            BodySegment segment;
            if (isInBodySegment)
            {
                // Choose a random body segment
                int segmentIndex = rand.Next(bodySegments.Length);
                segment = bodySegments[segmentIndex];
            }
            else
            {
                // Choose a random leg
                int legIndex = rand.Next(legs.Length);
                segment = legs[legIndex];
            }

            // Random position within the segment
            float3 localPos = new float3(
                (float)(rand.NextDouble() - 0.5) * segment.size.x,
                (float)(rand.NextDouble() - 0.5) * segment.size.y,
                (float)(rand.NextDouble() - 0.5) * segment.size.z
            );

            // Transform position by segment rotation
            float3 rotatedPos = math.rotate(segment.rotation, localPos);
            float3 worldPos = segment.position + rotatedPos;

            // Determine cell type based on position
            int cellTypeValue = DetermineCellType(worldPos, isInBodySegment, rand);

            // Create cell
            Cell cell = new Cell
            {
                position = worldPos,
                velocity = float3.zero,
                size = CELL_SIZE * (0.8f + 0.4f * (float)rand.NextDouble()),
                health = 0.7f + 0.3f * (float)rand.NextDouble(),
                type = cellTypeValue,
                organelleStartIndex = organelleIndex,
                organelleCount = 0,
                energy = 0.7f + 0.3f * (float)rand.NextDouble(),
                stress = 0f,
                rotation = quaternion.EulerXYZ(
                    (float)rand.NextDouble() * math.PI * 2f,
                    (float)rand.NextDouble() * math.PI * 2f,
                    (float)rand.NextDouble() * math.PI * 2f
                ),
                isUndergoing_Cryptobiosis = false
            };

            // Create organelles for this cell
            int numOrganelles = AVERAGE_CELL_ORGANELLES - 5 + rand.Next(10);
            cell.organelleCount = numOrganelles;

            // Always include a nucleus
            organelles[organelleIndex++] = new Organelle
            {
                relativePosition = float3.zero,  // Nucleus at center
                size = NUCLEUS_SIZE,
                type = (int)OrganelleType.Nucleus,
                health = 0.8f + 0.2f * (float)rand.NextDouble(),
                activity = 1.0f
            };

            // Add other organelles
            for (int j = 1; j < numOrganelles; j++)
            {
                // Random position within the cell
                float distance = cell.size * 0.3f * (float)rand.NextDouble();
                float3 direction = new float3(
                    (float)(rand.NextDouble() - 0.5),
                    (float)(rand.NextDouble() - 0.5),
                    (float)(rand.NextDouble() - 0.5)
                );

                if (math.length(direction) > 0.001f)
                    direction = math.normalize(direction);

                float3 orgPos = direction * distance;

                // Determine organelle type with mitochondria being common
                int organelleType = rand.Next(100) < 40
                    ? (int)OrganelleType.Mitochondria
                    : rand.Next(1, Enum.GetNames(typeof(OrganelleType)).Length);

                // Set organelle size based on type
                float organelleSize = MITOCHONDRIA_SIZE;
                switch (organelleType)
                {
                    case (int)OrganelleType.Mitochondria:
                        organelleSize = MITOCHONDRIA_SIZE;
                        break;
                    case (int)OrganelleType.Lysosome:
                        organelleSize = MITOCHONDRIA_SIZE * 0.8f;
                        break;
                    case (int)OrganelleType.Ribosome:
                        organelleSize = MITOCHONDRIA_SIZE * 0.5f;
                        break;
                    case (int)OrganelleType.Vacuole:
                        organelleSize = MITOCHONDRIA_SIZE * 1.2f;
                        break;
                    case (int)OrganelleType.Golgi:
                        organelleSize = MITOCHONDRIA_SIZE * 1.5f;
                        break;
                    case (int)OrganelleType.EndoplasmicReticulum:
                        organelleSize = ENDOPLASMIC_RETICULUM_LENGTH;
                        break;
                }

                organelles[organelleIndex++] = new Organelle
                {
                    relativePosition = orgPos,
                    size = organelleSize,
                    type = organelleType,
                    health = 0.7f + 0.3f * (float)rand.NextDouble(),
                    activity = 0.7f + 0.3f * (float)rand.NextDouble()
                };
            }

            // Update parallel arrays for job system
            cells[i] = cell;
            cellPositions[i] = cell.position;
            cellVelocities[i] = cell.velocity;
            cellHealth[i] = cell.health;
            cellType[i] = cell.type;
            cellEnergy[i] = cell.energy;
            cellRotations[i] = cell.rotation;
        }
    }

    private int DetermineCellType(float3 position, bool isInBodySegment, System.Random rand)
    {
        if (!isInBodySegment)
            return (int)CellType.Muscle; // Legs are mostly muscle

        // Head region
        if (position.x < -tardigradeLength * 0.3f)
        {
            return rand.Next(100) < 70
                ? (int)CellType.Nerve
                : (int)CellType.Epithelial;
        }

        // Middle region
        if (position.x > -tardigradeLength * 0.1f && position.x < tardigradeLength * 0.1f)
        {
            return rand.Next(100) < 60
                ? (int)CellType.Digestive
                : (int)CellType.Epithelial;
        }

        // Posterior region
        if (position.x > tardigradeLength * 0.2f)
        {
            return rand.Next(100) < 40
                ? (int)CellType.Reproductive
                : (int)CellType.Epithelial;
        }

        // Default to epithelial cells
        return (int)CellType.Epithelial;
    }

    private void RunSimulationStep()
    {
        // Update environmental factors' impact on cells
        var environmentJob = new EnvironmentalImpactJob
        {
            cells = cells,
            temperature = temperature,
            radiationLevel = radiationLevel / 100f,
            hydrationLevel = hydrationLevel / 100f,
            environmentalPressure = environmentalPressure,
            deltaTime = updateInterval,
            cryptobiosisLevel = cryptobiosisLevel,
            cellHealth = cellHealth,
            cellEnergy = cellEnergy,
            metabolicRate = metabolicRate
        };

        // Cell interaction job
        var interactionJob = new CellInteractionJob
        {
            cells = cells,
            cellPositions = cellPositions,
            cellVelocities = cellVelocities,
            cellType = cellType,
            cellEnergy = cellEnergy,
            cellHealth = cellHealth,
            deltaTime = updateInterval,
            cellCount = cellCount
        };

        // Cell movement job
        var movementJob = new CellMovementJob
        {
            cells = cells,
            cellPositions = cellPositions,
            cellVelocities = cellVelocities,
            cellRotations = cellRotations,
            deltaTime = updateInterval,
            cellCount = cellCount,
            tardigradeLength = tardigradeLength,
            tardigradeWidth = tardigradeWidth
        };

        // Organelle activity job
        var organelleJob = new OrganelleActivityJob
        {
            cells = cells,
            organelles = organelles,
            cellEnergy = cellEnergy,
            cellHealth = cellHealth,
            deltaTime = updateInterval,
            metabolicRate = metabolicRate
        };

        // Schedule jobs
        var environmentJobHandle = environmentJob.Schedule(cellCount, jobBatchSize);
        var interactionJobHandle = interactionJob.Schedule(cellCount, jobBatchSize, environmentJobHandle);
        var organelleJobHandle = organelleJob.Schedule(cellCount, jobBatchSize, interactionJobHandle);
        var movementJobHandle = movementJob.Schedule(cellCount, jobBatchSize, organelleJobHandle);

        // Wait for jobs to complete
        movementJobHandle.Complete();

        // Update parallel arrays from job results
        for (int i = 0; i < cellCount; i++)
        {
            cellPositions[i] = cells[i].position;
            cellVelocities[i] = cells[i].velocity;
            cellHealth[i] = cells[i].health;
            cellEnergy[i] = cells[i].energy;
            cellRotations[i] = cells[i].rotation;
        }
    }

    private void InduceCryptobiosis()
    {
        // Gradually enter cryptobiosis
        cryptobiosisLevel = math.min(cryptobiosisLevel + 0.1f * (1f - hydrationLevel / 100f) * updateInterval, 1f);
        metabolicRate = math.max(0.01f, 1f - cryptobiosisLevel * 0.99f);

        // Update stress response proteins
        stressResponseProteins = Mathf.FloorToInt(cellCount * 0.1f * cryptobiosisLevel);
        activeGenomeSegments = Mathf.FloorToInt(cellCount * (1f - cryptobiosisLevel * 0.7f));
    }

    private void RevertCryptobiosis()
    {
        // Gradually exit cryptobiosis
        cryptobiosisLevel = math.max(0f, cryptobiosisLevel - 0.05f * (hydrationLevel / 100f) * updateInterval);
        metabolicRate = math.max(0.01f, 1f - cryptobiosisLevel * 0.99f);

        // Update stress response proteins
        stressResponseProteins = Mathf.FloorToInt(cellCount * 0.1f * cryptobiosisLevel);
        activeGenomeSegments = Mathf.FloorToInt(cellCount * (1f - cryptobiosisLevel * 0.7f));
    }

    private void UpdateMetabolicRate()
    {
        if (cryptobiosisLevel < 0.01f)
        {
            // Normal metabolic rate varies with temperature (within normal range)
            if (temperature > 0f && temperature < 40f)
            {
                float optimalTemp = 25f;
                float tempFactor = 1f - math.abs(temperature - optimalTemp) / optimalTemp;
                metabolicRate = 0.7f + 0.3f * tempFactor;
            }
            else
            {
                // Extreme temperatures reduce metabolism
                metabolicRate = 0.3f;
            }
        }
    }

    private void UpdateVisuals()
    {
        if (!showCells)
            return;

        // Update or create cell visualizations
        if (cellVisuals.Count != cellCount)
            CreateCellVisuals();

        for (int i = 0; i < cellCount; i++)
        {
            if (i >= cellVisuals.Count)
                break;

            var visual = cellVisuals[i];
            if (visual == null)
                continue;

            // Position, scale, and rotation
            visual.transform.position = cells[i].position / 1000f; // Convert micrometers to millimeters
            visual.transform.localScale = Vector3.one * cells[i].size * cellScale / 1000f;
            visual.transform.rotation = cells[i].rotation;

            // Color based on health and stress
            Color cellColorToUse = Color.Lerp(stressedCellColor, cellColor, cells[i].health);

            if (cells[i].isUndergoing_Cryptobiosis)
                cellColorToUse = Color.Lerp(cellColorToUse, Color.blue, cryptobiosisLevel);

            // Set material color
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = renderer.material;
                mat.color = cellColorToUse;
            }
        }
    }

    private void CreateCellVisuals()
    {
        // Clean up existing visuals
        foreach (var visual in cellVisuals)
        {
            if (visual != null)
                DestroyImmediate(visual);
        }

        cellVisuals.Clear();

        if (!showCells)
            return;

        // Create new visuals for each cell
        for (int i = 0; i < cellCount; i++)
        {
            GameObject cellObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cellObj.name = $"Cell_{i}";
            cellObj.transform.parent = transform;

            // Position, scale, and rotation
            cellObj.transform.position = cells[i].position / 1000f; // Convert to millimeters
            cellObj.transform.localScale = Vector3.one * cells[i].size * cellScale / 1000f;
            cellObj.transform.rotation = cells[i].rotation;

            // Set material
            Renderer renderer = cellObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = cellColor;
                renderer.material = mat;
            }

            cellVisuals.Add(cellObj);

            // Add nucleus visualization if enabled
            if (showOrganelles)
            {
                CreateOrganelleVisuals(i, cellObj.transform);
            }
        }
    }

    private void CreateOrganelleVisuals(int cellIndex, Transform parentTransform)
    {
        Cell cell = cells[cellIndex];
        int startIdx = cell.organelleStartIndex;
        int endIdx = startIdx + cell.organelleCount;

        for (int i = startIdx; i < endIdx; i++)
        {
            Organelle org = organelles[i];

            // Only visualize nucleus to avoid too many GameObjects
            if (org.type == (int)OrganelleType.Nucleus)
            {
                GameObject nucleusObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                nucleusObj.name = $"Nucleus_{cellIndex}";
                nucleusObj.transform.parent = parentTransform;

                // Position and scale relative to parent cell
                nucleusObj.transform.localPosition = org.relativePosition / 1000f;
                nucleusObj.transform.localScale = Vector3.one * org.size / cell.size;

                // Set material
                Renderer renderer = nucleusObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = nucleusColor;
                    renderer.material = mat;
                }
            }
        }
    }

    private void CleanupSimulation()
    {
        if (cells.IsCreated) cells.Dispose();
        if (organelles.IsCreated) organelles.Dispose();
        if (cellPositions.IsCreated) cellPositions.Dispose();
        if (cellVelocities.IsCreated) cellVelocities.Dispose();
        if (cellHealth.IsCreated) cellHealth.Dispose();
        if (cellType.IsCreated) cellType.Dispose();
        if (cellEnergy.IsCreated) cellEnergy.Dispose();
        if (cellRotations.IsCreated) cellRotations.Dispose();

        // Clean up visualization
        foreach (var visual in cellVisuals)
        {
            if (visual != null)
                DestroyImmediate(visual);
        }

        cellVisuals.Clear();
        isInitialized = false;
    }

    void OnGUI()
    {
        if (!showDebugInfo)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Tardigrade Simulation");
        GUILayout.Label($"Cell Count: {cellCount}");
        GUILayout.Label($"Cryptobiosis: {cryptobiosisLevel:F2}");
        GUILayout.Label($"Metabolic Rate: {metabolicRate:F2}");
        GUILayout.Label($"Active Genome: {activeGenomeSegments}/{cellCount}");
        GUILayout.Label($"Stress Proteins: {stressResponseProteins}");
        GUILayout.EndArea();
    }

    // Jobs definitions

    [BurstCompile]
    public struct EnvironmentalImpactJob : IJobParallelFor
    {
        public NativeArray<Cell> cells;
        [ReadOnly] public float temperature;
        [ReadOnly] public float radiationLevel;
        [ReadOnly] public float hydrationLevel;
        [ReadOnly] public float environmentalPressure;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float cryptobiosisLevel;
        public NativeArray<float> cellHealth;
        public NativeArray<float> cellEnergy;
        [ReadOnly] public float metabolicRate;

        public void Execute(int index)
        {
            Cell cell = cells[index];

            // Determine if cell is in cryptobiosis
            cell.isUndergoing_Cryptobiosis = cryptobiosisLevel > 0.5f;

            if (cell.isUndergoing_Cryptobiosis)
            {
                // Cells in cryptobiosis maintain health but consume almost no energy
                cell.energy -= 0.001f * deltaTime;
            }
            else
            {
                // Temperature impact
                float tempStress = 0f;
                if (temperature < 0f)
                    tempStress = math.abs(temperature) * 0.01f;
                else if (temperature > 40f)
                    tempStress = (temperature - 40f) * 0.01f;

                // Radiation impact
                float radStress = radiationLevel * 0.05f;

                // Hydration impact
                float hydroStress = (1f - hydrationLevel) * 0.1f;

                // Pressure impact
                float pressureStress = 0f;
                if (environmentalPressure > 100f)
                    pressureStress = (environmentalPressure - 100f) * 0.0001f;

                // Combined stress
                float totalStress = math.min(1f, (tempStress + radStress + hydroStress + pressureStress));
                cell.stress = totalStress;

                // Apply stress to health if not in cryptobiosis
                if (totalStress > 0.1f)
                {
                    cell.health -= totalStress * 0.05f * deltaTime;
                    cell.health = math.max(0.01f, cell.health);
                }
                else if (cell.health < 0.99f)
                {
                    // Recover health slowly
                    cell.health += 0.01f * deltaTime * cell.energy;
                    cell.health = math.min(1f, cell.health);
                }

                // Energy consumption based on metabolic rate
                cell.energy -= 0.05f * metabolicRate * deltaTime;

                // Energy production from mitochondria happens in the organelle job
            }

            // Ensure values are within range
            cell.energy = math.max(0.01f, cell.energy);
            cell.energy = math.min(1f, cell.energy);

            // Update parallel arrays
            cellHealth[index] = cell.health;
            cellEnergy[index] = cell.energy;

            // Update the cell in the array
            cells[index] = cell;
        }
    }

    [BurstCompile]
    public struct CellInteractionJob : IJobParallelFor
    {
        public NativeArray<Cell> cells;
        [ReadOnly] public NativeArray<float3> cellPositions;
        public NativeArray<float3> cellVelocities;
        [ReadOnly] public NativeArray<int> cellType;
        public NativeArray<float> cellEnergy;
        public NativeArray<float> cellHealth;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public int cellCount;

        public void Execute(int index)
        {
            Cell cell = cells[index];
            float3 totalForce = float3.zero;

            // Only process a limited number of nearest cells for performance
            int maxNeighbors = 10;
            float maxDistance = 50f; // micrometers

            // Simple interaction with nearby cells
            for (int i = 0; i < maxNeighbors; i++)
            {
                // Use a simple hashing function to sample different cells
                int otherIndex = (index + 37 * i) % cellCount;

                if (otherIndex == index)
                    continue;

                float3 otherPos = cellPositions[otherIndex];
                float3 diff = cell.position - otherPos;
                float distance = math.length(diff);

                if (distance < maxDistance && distance > 0.001f)
                {
                    float3 direction = diff / distance;

                    // Cell type interaction
                    int otherType = cellType[otherIndex];
                    bool sameCellType = (cell.type == otherType);

                    // Adhesion for same cell type, mild repulsion for different types
                    float adhesionFactor = sameCellType ? 2f : 0.5f;

                    // Compute force
                    float repulsionDistance = (cell.size + cells[otherIndex].size) * 0.6f;
                    float force = 0f;

                    if (distance < repulsionDistance)
                    {
                        // Repulsion when too close
                        force = -10f * (1f - distance / repulsionDistance);
                    }
                    else if (sameCellType && distance < repulsionDistance * 2f)
                    {
                        // Adhesion for same type cells at appropriate distance
                        force = 0.5f * adhesionFactor * (1f - (distance - repulsionDistance) / repulsionDistance);
                    }

                    totalForce += direction * force;
                }
            }

            // Apply force to velocity
            float3 velocity = cellVelocities[index];
            velocity += totalForce * deltaTime;

            // Apply drag based on energy
            float drag = 2f * (1f - cell.energy * 0.5f);
            velocity *= math.exp(-drag * deltaTime);

            // Update position
            float3 position = cell.position + velocity * deltaTime;

            // Update cell
            cell.position = position;
            cell.velocity = velocity;

            // Update arrays
            cells[index] = cell;
            cellVelocities[index] = velocity;
        }
    }

    [BurstCompile]
    public struct CellMovementJob : IJobParallelFor
    {
        public NativeArray<Cell> cells;
        public NativeArray<float3> cellPositions;
        public NativeArray<float3> cellVelocities;
        public NativeArray<quaternion> cellRotations;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public int cellCount;
        [ReadOnly] public float tardigradeLength;
        [ReadOnly] public float tardigradeWidth;

        public void Execute(int index)
        {
            Cell cell = cells[index];

            // Get current position and velocity
            float3 position = cell.position;
            float3 velocity = cell.velocity;

            // Check boundaries of the tardigrade
            float halfLength = tardigradeLength * 0.5f;
            float halfWidth = tardigradeWidth * 0.5f;

            // Confinement force to keep cells within the tardigrade
            float3 boundaryForce = float3.zero;

            if (math.abs(position.x) > halfLength)
            {
                boundaryForce.x = -math.sign(position.x) * 5f * (math.abs(position.x) - halfLength);
            }

            if (math.abs(position.y) > halfWidth)
            {
                boundaryForce.y = -math.sign(position.y) * 5f * (math.abs(position.y) - halfWidth);
            }

            if (math.abs(position.z) > halfWidth)
            {
                boundaryForce.z = -math.sign(position.z) * 5f * (math.abs(position.z) - halfWidth);
            }

            // Update velocity with boundary force
            velocity += boundaryForce * deltaTime;

            // Update position
            position += velocity * deltaTime;

            // Add slight random rotation
            quaternion rotation = cell.rotation;

            // Use index for deterministic but cell-specific randomization
            float randomX = math.sin(index * 0.1f + deltaTime * 0.5f) * 0.01f;
            float randomY = math.cos(index * 0.2f + deltaTime * 0.3f) * 0.01f;
            float randomZ = math.sin(index * 0.3f + deltaTime * 0.7f) * 0.01f;

            quaternion randomRotation = quaternion.EulerXYZ(randomX, randomY, randomZ);
            rotation = math.mul(rotation, randomRotation);

            // Update cell data
            cell.position = position;
            cell.velocity = velocity;
            cell.rotation = rotation;

            // Update arrays
            cells[index] = cell;
            cellPositions[index] = position;
            cellVelocities[index] = velocity;
            cellRotations[index] = rotation;
        }
    }

    [BurstCompile]
    public struct OrganelleActivityJob : IJobParallelFor
    {
        public NativeArray<Cell> cells;
        public NativeArray<Organelle> organelles;
        public NativeArray<float> cellEnergy;
        public NativeArray<float> cellHealth;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float metabolicRate;

        public void Execute(int index)
        {
            Cell cell = cells[index];

            // Skip if cell is in full cryptobiosis (minimal organelle activity)
            if (cell.isUndergoing_Cryptobiosis)
                return;

            int startIdx = cell.organelleStartIndex;
            int endIdx = startIdx + cell.organelleCount;

            float energyProduction = 0f;
            float cellRepair = 0f;

            // Process organelles
            for (int i = startIdx; i < endIdx; i++)
            {
                Organelle org = organelles[i];

                // Organelle activity depends on cell energy and health
                float activityLevel = org.activity * cell.energy * cell.health * metabolicRate;

                switch (org.type)
                {
                    case (int)OrganelleType.Mitochondria:
                        // Energy production
                        energyProduction += 0.02f * activityLevel * deltaTime;
                        break;

                    case (int)OrganelleType.Nucleus:
                        // Cell repair capability
                        cellRepair += 0.01f * activityLevel * deltaTime;
                        break;

                    case (int)OrganelleType.Lysosome:
                        // Waste processing
                        if (cell.health < 0.7f)
                            cellRepair += 0.005f * activityLevel * deltaTime;
                        break;

                        // Add more organelle-specific behaviors as needed
                }

                // Update organelle
                organelles[i] = org;
            }

            // Apply energy production and cell repair
            cell.energy += energyProduction;
            cell.energy = math.min(1f, cell.energy);

            if (cell.health < 0.99f)
            {
                cell.health += cellRepair;
                cell.health = math.min(1f, cell.health);
            }

            // Update cell and arrays
            cells[index] = cell;
            cellEnergy[index] = cell.energy;
            cellHealth[index] = cell.health;
        }
    }

    void OnDrawGizmos()
    {
        if (!isInitialized || !showDebugInfo)
            return;

        // Draw tardigrade boundary
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(tardigradeLength, tardigradeWidth, tardigradeWidth) / 1000f
        );

        // Draw body segments
        if (bodySegments != null)
        {
            Gizmos.color = Color.blue;
            foreach (var segment in bodySegments)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;

                Gizmos.matrix = Matrix4x4.TRS(
                    segment.position / 1000f,
                    segment.rotation,
                    segment.size / 1000f
                );

                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

                Gizmos.matrix = oldMatrix;
            }
        }

        // Draw legs
        if (legs != null)
        {
            Gizmos.color = Color.green;
            foreach (var leg in legs)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;

                Gizmos.matrix = Matrix4x4.TRS(
                    leg.position / 1000f,
                    leg.rotation,
                    leg.size / 1000f
                );

                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

                Gizmos.matrix = oldMatrix;
            }
        }
    }
}