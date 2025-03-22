// Main Tardigrade Simulation Controller
// Handles cellular behaviors, organization, and morphology

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

public class TardigradeSimulator : MonoBehaviour
{
    // Simulation parameters
    [Header("Simulation Settings")]
    public int cellCount = 1200;                // Adult tardigrade has ~1000-1200 cells
    public float simulationSpeed = 1.0f;
    public float worldScale = 50.0f;            // Scale of the simulation space

    [Header("Body Proportions")]
    public float bodyLength = 30.0f;            // Length of the tardigrade body
    public float bodyWidth = 10.0f;             // Width of the tardigrade body
    public float bodyHeight = 8.0f;             // Height of the tardigrade body
    public float headProportion = 0.25f;        // Proportion of body that is head

    [Header("Cell Type Distribution")]
    [Range(0, 1)] public float epidermalRatio = 0.35f;
    [Range(0, 1)] public float muscleRatio = 0.25f;
    [Range(0, 1)] public float nerveRatio = 0.15f;
    [Range(0, 1)] public float digestiveRatio = 0.1f;
    [Range(0, 1)] public float storageRatio = 0.1f;
    [Range(0, 1)] public float reproductiveRatio = 0.05f;

    [Header("Leg Settings")]
    public int legPairCount = 4;                // Tardigrades have 4 pairs of legs
    public float legLength = 5.0f;
    public int cellsPerLeg = 20;

    [Header("Movement Settings")]
    public float movementSpeed = 1.0f;
    public float legMovementAmplitude = 1.0f;
    public float legMovementFrequency = 0.5f;

    [Header("Visualization")]
    public Material cellMaterial;
    public bool showCellTypes = true;
    public bool showBodySegments = true;

    // References
    public TardigradeEnvironment environment;

    // Native arrays for cell data
    [HideInInspector] public NativeArray<TardigradeCellData> cellData;
    private NativeArray<float3> initialPositions;
    private NativeArray<float3> targetPositions;

    // Rendering
    private ComputeBuffer cellBuffer;
    private struct CellRenderData
    {
        public float3 position;
        public float4 color;
        public float size;
    }

    // Simulation state
    private float simulationTime = 0.0f;
    private bool isSimulationPaused = false;
    private bool isCryptobiosisActive = false;

    void Start()
    {
        InitializeSimulation();
    }

    void Update()
    {
        if (!isSimulationPaused)
        {
            float deltaTime = Time.deltaTime * simulationSpeed;
            simulationTime += deltaTime;

            // Run simulation step
            SimulateStep(deltaTime);

            // Check for cryptobiosis trigger
            CheckEnvironmentalTriggers();
        }

        // Update visualization
        UpdateCellVisualization();
    }

    void InitializeSimulation()
    {
        // Calculate how many cells of each type
        int epidermalCount = Mathf.FloorToInt(cellCount * epidermalRatio);
        int muscleCount = Mathf.FloorToInt(cellCount * muscleRatio);
        int nerveCount = Mathf.FloorToInt(cellCount * nerveRatio);
        int digestiveCount = Mathf.FloorToInt(cellCount * digestiveRatio);
        int storageCount = Mathf.FloorToInt(cellCount * storageRatio);
        int reproductiveCount = Mathf.FloorToInt(cellCount * reproductiveRatio);
        int legCellCount = legPairCount * 2 * cellsPerLeg; // Both sides

        // Allocate cell data arrays
        cellData = new NativeArray<TardigradeCellData>(cellCount, Allocator.Persistent);
        initialPositions = new NativeArray<float3>(cellCount, Allocator.Persistent);
        targetPositions = new NativeArray<float3>(cellCount, Allocator.Persistent);

        // Initialize cells by type and position
        int currentCell = 0;

        // Create epidermal cells (outer layer)
        currentCell = InitializeEpidermalCells(currentCell, epidermalCount);

        // Create muscle cells
        currentCell = InitializeMuscleCells(currentCell, muscleCount);

        // Create nerve cells
        currentCell = InitializeNerveCells(currentCell, nerveCount);

        // Create digestive cells
        currentCell = InitializeDigestiveCells(currentCell, digestiveCount);

        // Create storage cells
        currentCell = InitializeStorageCells(currentCell, storageCount);

        // Create reproductive cells
        currentCell = InitializeReproductiveCells(currentCell, reproductiveCount);

        // Create leg cells
        currentCell = InitializeLegCells(currentCell, legCellCount);

        // Initialize rendering
        cellBuffer = new ComputeBuffer(cellCount, 32); // position, color, size
    }

    int InitializeEpidermalCells(int startIndex, int count)
    {
        // Create epidermal cells that form the outer layer of the tardigrade
        for (int i = 0; i < count; i++)
        {
            // Calculate position on the "skin" of the tardigrade
            float progress = (float)i / count;
            float segmentProgress = progress * 5.0f; // 5 segments (head + 4 body)
            BodySegment segment = (BodySegment)Mathf.FloorToInt(segmentProgress);
            float segmentPosition = segmentProgress - Mathf.Floor(segmentProgress);

            // Calculate position based on segment
            float3 position = CalculatePositionOnBody(segment, segmentPosition, true);

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Epidermal, position);
            cell.segment = segment;
            cell.segmentPosition = segmentPosition;
            cell.adhesionStrength = 0.8f; // Strong adhesion for epidermal cells

            // Store cell data
            int index = startIndex + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        return startIndex + count;
    }

    int InitializeMuscleCells(int startIndex, int count)
    {
        // Create muscle cells - both longitudinal and circular
        int longitudinalCount = count / 2;
        int circularCount = count - longitudinalCount;

        // Longitudinal muscles (run along body axis)
        for (int i = 0; i < longitudinalCount; i++)
        {
            float progress = (float)i / longitudinalCount;
            float segmentProgress = progress * 5.0f;
            BodySegment segment = (BodySegment)Mathf.FloorToInt(segmentProgress);
            float segmentPosition = segmentProgress - Mathf.Floor(segmentProgress);

            // Position muscles just under the epidermis
            float3 position = CalculatePositionOnBody(segment, segmentPosition, false);
            position = position * 0.9f; // Move slightly inward

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Muscle, position);
            cell.segment = segment;
            cell.segmentPosition = segmentPosition;
            cell.contractionState = 0.5f;

            // Store cell data
            int index = startIndex + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        // Circular muscles (wrap around body axis)
        for (int i = 0; i < circularCount; i++)
        {
            float progress = (float)i / circularCount;
            float segmentProgress = progress * 5.0f;
            BodySegment segment = (BodySegment)Mathf.FloorToInt(segmentProgress);
            float segmentPosition = segmentProgress - Mathf.Floor(segmentProgress);

            // Calculate angle around the body axis
            float angle = progress * 2.0f * Mathf.PI;

            // Position circular muscles just under the longitudinal muscles
            float3 basePosition = CalculatePositionOnBody(segment, segmentPosition, false);
            basePosition = basePosition * 0.85f; // Move inward more than longitudinal

            // Rotate around body axis
            float3 position = RotateAroundBodyAxis(basePosition, angle, segment, segmentPosition);

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Muscle, position);
            cell.segment = segment;
            cell.segmentPosition = segmentPosition;
            cell.contractionState = 0.5f;

            // Store cell data
            int index = startIndex + longitudinalCount + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        return startIndex + count;
    }

    int InitializeNerveCells(int startIndex, int count)
    {
        // Create nerve cells - concentrate in head and then along body axis
        int headNeuronsCount = Mathf.FloorToInt(count * 0.6f); // 60% in head
        int bodyNeuronsCount = count - headNeuronsCount;

        // Head neurons (brain)
        for (int i = 0; i < headNeuronsCount; i++)
        {
            float progress = (float)i / headNeuronsCount;

            // Position within head segment
            float3 position = new float3(
                UnityEngine.Random.Range(-bodyWidth * 0.3f, bodyWidth * 0.3f),
                UnityEngine.Random.Range(-bodyHeight * 0.3f, bodyHeight * 0.3f),
                -bodyLength * (0.5f - headProportion * progress * 0.8f)
            );

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Nerve, position);
            cell.segment = BodySegment.Head;
            cell.segmentPosition = progress;

            // Store cell data
            int index = startIndex + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        // Body neurons (ganglia along ventral nerve cord)
        for (int i = 0; i < bodyNeuronsCount; i++)
        {
            float progress = (float)i / bodyNeuronsCount;
            BodySegment segment = (BodySegment)(1 + Mathf.FloorToInt(progress * 4.0f)); // Skip head
            float segmentPosition = progress * 4.0f - Mathf.Floor(progress * 4.0f);

            // Positioned along ventral side
            float3 basePosition = CalculatePositionOnBody(segment, segmentPosition, false);
            float3 position = new float3(
                basePosition.x * 0.2f,
                -bodyHeight * 0.4f, // Ventral positioning
                basePosition.z
            );

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Nerve, position);
            cell.segment = segment;
            cell.segmentPosition = segmentPosition;

            // Store cell data
            int index = startIndex + headNeuronsCount + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        return startIndex + count;
    }

    int InitializeDigestiveCells(int startIndex, int count)
    {
        // Create digestive cells - simple tube from mouth to anus
        for (int i = 0; i < count; i++)
        {
            float progress = (float)i / count;
            float segmentProgress = progress * 5.0f;
            BodySegment segment = (BodySegment)Mathf.FloorToInt(segmentProgress);
            float segmentPosition = segmentProgress - Mathf.Floor(segmentProgress);

            // Calculate position along central body axis with some variation
            float3 basePosition = CalculatePositionOnBody(segment, segmentPosition, false);
            float radius = bodyWidth * 0.2f * UnityEngine.Random.value;
            float angle = UnityEngine.Random.value * 2.0f * Mathf.PI;

            float3 position = new float3(
                basePosition.x + radius * Mathf.Cos(angle),
                basePosition.y + radius * Mathf.Sin(angle),
                basePosition.z
            );

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Digestive, position);
            cell.segment = segment;
            cell.segmentPosition = segmentPosition;

            // Store cell data
            int index = startIndex + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        return startIndex + count;
    }

    int InitializeStorageCells(int startIndex, int count)
    {
        // Create storage cells - clustered in body segments
        for (int i = 0; i < count; i++)
        {
            float progress = (float)i / count;
            BodySegment segment = (BodySegment)(1 + Mathf.FloorToInt(progress * 4.0f)); // Skip head
            float segmentPosition = progress * 4.0f - Mathf.Floor(progress * 4.0f);

            // Position storage cells in the middle segments, mostly dorsal
            float3 basePosition = CalculatePositionOnBody(segment, segmentPosition, false);
            float3 position = new float3(
                basePosition.x * UnityEngine.Random.Range(0.2f, 0.6f),
                basePosition.y * UnityEngine.Random.Range(0.2f, 0.6f), // More dorsal
                basePosition.z
            );

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Storage, position);
            cell.segment = segment;
            cell.segmentPosition = segmentPosition;
            cell.energy = UnityEngine.Random.Range(0.7f, 1.0f); // Storage cells have high energy

            // Store cell data
            int index = startIndex + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        return startIndex + count;
    }

    int InitializeReproductiveCells(int startIndex, int count)
    {
        // Create reproductive cells - positioned in posterior segments
        for (int i = 0; i < count; i++)
        {
            float progress = (float)i / count;
            BodySegment segment = (BodySegment)(3 + Mathf.FloorToInt(progress * 2.0f)); // Last two segments
            float segmentPosition = progress * 2.0f - Mathf.Floor(progress * 2.0f);

            // Position deep in the body
            float3 basePosition = CalculatePositionOnBody(segment, segmentPosition, false);
            float3 position = basePosition * UnityEngine.Random.Range(0.3f, 0.5f);

            // Create cell
            TardigradeCellData cell = CreateCellOfType(TardigradeCell.Reproductive, position);
            cell.segment = segment;
            cell.segmentPosition = segmentPosition;

            // Store cell data
            int index = startIndex + i;
            cellData[index] = cell;
            initialPositions[index] = position;
            targetPositions[index] = position;
        }

        return startIndex + count;
    }

    int InitializeLegCells(int startIndex, int maxCount)
    {
        // Create cells for all leg pairs
        int cellsCreated = 0;
        int cellsPerPair = cellsPerLeg * 2; // Left and right

        for (int pair = 0; pair < legPairCount; pair++)
        {
            BodySegment segment = (BodySegment)(pair + 1); // Legs attach to body segments 1-4

            // For each side (left/right)
            for (int side = 0; side < 2; side++)
            {
                float sideMultiplier = (side == 0) ? -1.0f : 1.0f; // -1 for left, 1 for right

                // For each cell in leg
                for (int i = 0; i < cellsPerLeg; i++)
                {
                    // Check if we've reached the maximum allowed cells
                    if (startIndex + cellsCreated >= cellData.Length)
                    {
                        Debug.LogWarning("Reached maximum cell count. Not all leg cells were created.");
                        return startIndex + cellsCreated;
                    }

                    float legProgress = (float)i / cellsPerLeg;

                    // Calculate leg cell position
                    float3 basePosition = CalculatePositionOnBody(segment, 0.5f, true); // Middle of segment
                    float legX = basePosition.x + (sideMultiplier * bodyWidth * 0.5f); // Side attachment point
                    float legY = basePosition.y - (bodyHeight * 0.3f); // Slightly below middle

                    // Position along the leg length
                    float3 position = new float3(
                        legX + (sideMultiplier * legProgress * legLength),
                        legY - (legProgress * legLength * 0.8f), // Leg curves downward
                        basePosition.z + (legProgress * legLength * 0.2f - legLength * 0.1f) // Slight fore/aft curve
                    );

                    // Create leg cell
                    TardigradeCellData cell = CreateCellOfType(TardigradeCell.Leg, position);
                    cell.segment = segment;
                    cell.segmentPosition = 0.5f;
                    cell.isLegCell = true;
                    cell.legPair = pair + 1;

                    // Store cell data
                    int index = startIndex + cellsCreated;
                    cellData[index] = cell;
                    initialPositions[index] = position;
                    targetPositions[index] = position;

                    cellsCreated++;
                }
            }
        }

        return startIndex + cellsCreated;
    }

    TardigradeCellData CreateCellOfType(TardigradeCell type, float3 position)
    {
        // Create a new cell with type-specific properties
        TardigradeCellData cell = new TardigradeCellData
        {
            position = position,
            velocity = new float3(0, 0, 0),
            energy = UnityEngine.Random.Range(0.5f, 0.9f),
            age = UnityEngine.Random.Range(0.0f, 0.5f),
            cellType = type,
            cellState = CellState.Alive,
            metabolicState = MetabolicState.Active,
            segment = BodySegment.Head, // Default, will be set later
            segmentPosition = 0.0f,
            adhesionStrength = 0.5f, // Default adhesion
            structuralIntegrity = 1.0f,
            waterContent = 0.9f, // Fully hydrated by default
            trehaloseLevel = 0.2f, // Base level of protective sugars
            heatShockProteins = 0.1f, // Base level of protective proteins
            isLegCell = false,
            legPair = 0,
            contractionState = 0.0f
        };

        // Adjust properties based on cell type
        switch (type)
        {
            case TardigradeCell.Epidermal:
                cell.adhesionStrength = 0.8f;
                break;

            case TardigradeCell.Muscle:
                cell.contractionState = UnityEngine.Random.Range(0.3f, 0.7f);
                break;

            case TardigradeCell.Nerve:
                cell.energy = UnityEngine.Random.Range(0.6f, 1.0f); // High energy consumption
                break;

            case TardigradeCell.Digestive:
                cell.energy = UnityEngine.Random.Range(0.7f, 0.9f);
                break;

            case TardigradeCell.Storage:
                cell.energy = UnityEngine.Random.Range(0.8f, 1.0f); // High energy storage
                break;

            case TardigradeCell.Reproductive:
                cell.energy = UnityEngine.Random.Range(0.7f, 0.9f);
                break;

            case TardigradeCell.Leg:
                cell.adhesionStrength = 0.7f;
                cell.isLegCell = true;
                break;
        }

        return cell;
    }

    float3 CalculatePositionOnBody(BodySegment segment, float segmentPosition, bool surface)
    {
        // Calculate a position based on the segment and relative position within the segment
        float segmentLength = bodyLength / 5.0f; // 5 segments total

        // Calculate base Z position
        float zPos = bodyLength * 0.5f - (int)segment * segmentLength - segmentPosition * segmentLength;

        // Calculate segment width and height (tapered at ends)
        float widthRatio = 1.0f;
        float heightRatio = 1.0f;

        if (segment == BodySegment.Head)
        {
            // Head tapers toward front
            widthRatio = 0.7f + 0.3f * segmentPosition;
            heightRatio = 0.7f + 0.3f * segmentPosition;
        }
        else if (segment == BodySegment.Segment4)
        {
            // Last segment tapers toward rear
            widthRatio = 0.7f + 0.3f * (1.0f - segmentPosition);
            heightRatio = 0.7f + 0.3f * (1.0f - segmentPosition);
        }

        // For surface positions, use a random point on the elliptical cross-section
        if (surface)
        {
            float angle = UnityEngine.Random.value * 2.0f * Mathf.PI;
            float x = Mathf.Cos(angle) * bodyWidth * 0.5f * widthRatio;
            float y = Mathf.Sin(angle) * bodyHeight * 0.5f * heightRatio;

            return new float3(x, y, zPos);
        }
        else
        {
            // For interior positions, use a point inside the elliptical cross-section
            float radius = UnityEngine.Random.value;
            float angle = UnityEngine.Random.value * 2.0f * Mathf.PI;
            float x = Mathf.Cos(angle) * bodyWidth * 0.5f * widthRatio * radius;
            float y = Mathf.Sin(angle) * bodyHeight * 0.5f * heightRatio * radius;

            return new float3(x, y, zPos);
        }
    }

    float3 RotateAroundBodyAxis(float3 position, float angle, BodySegment segment, float segmentPosition)
    {
        // Rotate a point around the body's main axis (Z)
        float segmentLength = bodyLength / 5.0f;
        float zPos = bodyLength * 0.5f - (int)segment * segmentLength - segmentPosition * segmentLength;

        // Calculate center axis position
        float3 center = new float3(0, 0, zPos);

        // Calculate vector from center
        float3 fromCenter = position - center;

        // Rotate in XY plane
        float x = fromCenter.x * Mathf.Cos(angle) - fromCenter.y * Mathf.Sin(angle);
        float y = fromCenter.x * Mathf.Sin(angle) + fromCenter.y * Mathf.Cos(angle);

        // Return rotated position
        return new float3(x, y, position.z);
    }

    void SimulateStep(float deltaTime)
    {
        // Only run simulation if not in cryptobiosis
        if (isCryptobiosisActive && environment.humidity < environment.cryptobiosisThreshold)
        {
            // In cryptobiosis, very little happens
            CryptobiosisUpdate(deltaTime);
            return;
        }

        // Run jobs for simulation
        CellMovementJob moveJob = new CellMovementJob
        {
            cellData = cellData,
            targetPositions = targetPositions,
            deltaTime = deltaTime,
            movementSpeed = movementSpeed,
            simulationTime = simulationTime
        };

        JobHandle moveHandle = moveJob.Schedule(cellData.Length, 64);
        moveHandle.Complete();

        // Update cell behavior
        UpdateCellBehavior(deltaTime);

        // Apply environmental effects
        if (environment != null)
        {
            environment.ApplyEnvironmentToCells(cellData, deltaTime);
        }

        // Update limb movement
        UpdateLegMovement(deltaTime);

        // Check for state transitions
        UpdateCellStates(deltaTime);
    }

    void UpdateCellBehavior(float deltaTime)
    {
        // Handle cell type specific behaviors
        for (int i = 0; i < cellData.Length; i++)
        {
            TardigradeCellData cell = cellData[i];

            // Skip cells that aren't active
            if (cell.metabolicState != MetabolicState.Active)
            {
                continue;
            }

            // Type-specific behaviors
            switch (cell.cellType)
            {
                case TardigradeCell.Muscle:
                    // Muscle contraction cycles
                    float contractionRate = 0.2f * deltaTime;
                    bool isLongitudinal = !cell.isLegCell && UnityEngine.Random.value > 0.5f;

                    if (isLongitudinal)
                    {
                        // Longitudinal muscles contract in sequence
                        float targetContraction = Mathf.Sin(simulationTime + cell.segmentPosition * 5.0f) * 0.5f + 0.5f;
                        cell.contractionState = Mathf.MoveTowards(cell.contractionState, targetContraction, contractionRate);
                    }
                    else
                    {
                        // Circular and leg muscles maintain tone
                        cell.contractionState = Mathf.MoveTowards(cell.contractionState, 0.5f, contractionRate * 0.5f);
                    }
                    break;

                case TardigradeCell.Nerve:
                    // Nerve cells consume more energy
                    cell.energy -= 0.01f * deltaTime;
                    break;

                case TardigradeCell.Digestive:
                    // Digestive cells process nutrients when available
                    if (environment != null && environment.humidity > 50f)
                    {
                        cell.energy = Mathf.Min(1.0f, cell.energy + 0.005f * deltaTime);
                    }
                    break;

                case TardigradeCell.Storage:
                    // Storage cells maintain energy reserves
                    cell.energy = Mathf.Max(cell.energy, 0.7f);
                    break;
            }

            // Update the cell
            cellData[i] = cell;
        }
    }

    void UpdateLegMovement(float deltaTime)
    {
        // Handle leg movement for locomotion
        for (int i = 0; i < cellData.Length; i++)
        {
            TardigradeCellData cell = cellData[i];

            // Only update leg cells
            if (!cell.isLegCell || cell.metabolicState != MetabolicState.Active)
            {
                continue;
            }

            // Calculate leg movement patterns
            int legPair = cell.legPair;
            float phaseOffset = legPair * (Mathf.PI / 2.0f); // Offset each leg pair
            float cyclePosition = simulationTime * legMovementFrequency + phaseOffset;

            // Calculate movement
            float3 legMovement = new float3(
                Mathf.Sin(cyclePosition) * legMovementAmplitude,
                Mathf.Cos(cyclePosition) * legMovementAmplitude * 0.5f,
                0
            );

            // Update target position
            targetPositions[i] = initialPositions[i] + legMovement;

            // Update leg contraction state for visualization
            cell.contractionState = (Mathf.Sin(cyclePosition) + 1.0f) * 0.5f;
            cellData[i] = cell;
        }
    }

    void UpdateCellStates(float deltaTime)
    {
        // Update cell states based on energy and age
        for (int i = 0; i < cellData.Length; i++)
        {
            TardigradeCellData cell = cellData[i];

            // Skip cells in cryptobiosis
            if (cell.metabolicState == MetabolicState.Tun ||
                cell.metabolicState == MetabolicState.Cyst ||
                cell.metabolicState == MetabolicState.Anhydrobiotic)
            {
                continue;
            }

            // Age cells slightly
            cell.age += 0.001f * deltaTime;

            // Basic metabolism
            float baseMetabolism = 0.005f * deltaTime;

            // Adjust metabolism based on cell type
            switch (cell.cellType)
            {
                case TardigradeCell.Nerve:
                    baseMetabolism *= 1.5f;
                    break;
                case TardigradeCell.Muscle:
                    baseMetabolism *= 1.2f;
                    break;
                case TardigradeCell.Storage:
                    baseMetabolism *= 0.5f;
                    break;
            }

            // Consume energy
            cell.energy = Mathf.Max(0, cell.energy - baseMetabolism);

            // Check for stress conditions
            if (cell.energy < 0.2f)
            {
                cell.metabolicState = MetabolicState.Stressed;

                // Trigger protection mechanisms
                cell.trehaloseLevel = Mathf.Min(1.0f, cell.trehaloseLevel + 0.01f * deltaTime);
                cell.heatShockProteins = Mathf.Min(1.0f, cell.heatShockProteins + 0.01f * deltaTime);
            }
            else if (cell.energy > 0.5f && cell.metabolicState == MetabolicState.Stressed)
            {
                // Recover from stress
                cell.metabolicState = MetabolicState.Active;
            }

            cellData[i] = cell;
        }
    }

    void CryptobiosisUpdate(float deltaTime)
    {
        // Minimal updates during cryptobiosis
        for (int i = 0; i < cellData.Length; i++)
        {
            TardigradeCellData cell = cellData[i];

            // In cryptobiosis, metabolism virtually stops
            float minimalMetabolism = 0.0001f * deltaTime;
            cell.energy = Mathf.Max(0, cell.energy - minimalMetabolism);

            // Maintain high levels of protective compounds
            cell.trehaloseLevel = Mathf.Max(0.8f, cell.trehaloseLevel);

            cellData[i] = cell;
        }
    }

    void CheckEnvironmentalTriggers()
    {
        if (environment == null) return;

        // Check for cryptobiosis triggers
        bool shouldBeCryptobiotic = environment.humidity < environment.cryptobiosisThreshold ||
                                   environment.temperature < 0f ||
                                   environment.temperature > 40f;

        // Transition to/from cryptobiosis
        if (shouldBeCryptobiotic && !isCryptobiosisActive)
        {
            EnterCryptobiosis();
        }
        else if (!shouldBeCryptobiotic && isCryptobiosisActive)
        {
            ExitCryptobiosis();
        }
    }

    void EnterCryptobiosis()
    {
        isCryptobiosisActive = true;
        Debug.Log("Tardigrade entering cryptobiosis state");

        // Update all cells
        for (int i = 0; i < cellData.Length; i++)
        {
            TardigradeCellData cell = cellData[i];
            cell.metabolicState = MetabolicState.Tun;
            cell.velocity = new float3(0, 0, 0);
            cell.waterContent = 0.2f;
            cell.trehaloseLevel = 0.9f;
            cellData[i] = cell;
        }
    }

    void ExitCryptobiosis()
    {
        isCryptobiosisActive = false;
        Debug.Log("Tardigrade exiting cryptobiosis state");

        // Gradually rehydrate cells
        for (int i = 0; i < cellData.Length; i++)
        {
            TardigradeCellData cell = cellData[i];
            cell.metabolicState = MetabolicState.Stressed; // First stressed, then active
            cell.waterContent = 0.5f; // Partially rehydrated
            cellData[i] = cell;
        }
    }

    void UpdateCellVisualization()
    {
        // Create render data for visualization
        var renderData = new NativeArray<CellRenderData>(cellData.Length, Allocator.Temp);

        for (int i = 0; i < cellData.Length; i++)
        {
            TardigradeCellData cell = cellData[i];

            // Get color based on cell type
            Color color = GetCellTypeColor(cell.cellType);

            // Modify color based on cell state
            switch (cell.metabolicState)
            {
                case MetabolicState.Stressed:
                    color = Color.Lerp(color, Color.yellow, 0.5f);
                    break;
                case MetabolicState.Tun:
                    color = Color.Lerp(color, Color.blue, 0.7f);
                    break;
                case MetabolicState.Anhydrobiotic:
                    color = Color.Lerp(color, Color.grey, 0.8f);
                    break;
            }

            // Create render data
            renderData[i] = new CellRenderData
            {
                position = cell.position,
                color = new float4(color.r, color.g, color.b, color.a),
                size = GetCellSize(cell)
            };
        }

        // Update buffer
        cellBuffer.SetData(renderData);

        // Set buffer to material
        cellMaterial.SetBuffer("_CellBuffer", cellBuffer);
        cellMaterial.SetInt("_CellCount", cellData.Length);

        renderData.Dispose();
    }

    private Color GetCellTypeColor(TardigradeCell type)
    {
        switch (type)
        {
            case TardigradeCell.Epidermal:
                return new Color(0.8f, 0.7f, 0.6f); // Tan
            case TardigradeCell.Muscle:
                return new Color(0.8f, 0.2f, 0.2f); // Red
            case TardigradeCell.Nerve:
                return new Color(0.2f, 0.8f, 0.2f); // Green
            case TardigradeCell.Digestive:
                return new Color(0.8f, 0.5f, 0.2f); // Orange
            case TardigradeCell.Storage:
                return new Color(0.8f, 0.8f, 0.2f); // Yellow
            case TardigradeCell.Reproductive:
                return new Color(0.8f, 0.4f, 0.8f); // Purple
            case TardigradeCell.Leg:
                return new Color(0.6f, 0.6f, 0.8f); // Light blue
            default:
                return Color.gray;
        }
    }

    private float GetCellSize(TardigradeCellData cell)
    {
        // Base size depends on cell type
        float baseSize = 1.0f;

        switch (cell.cellType)
        {
            case TardigradeCell.Epidermal:
                baseSize = 0.8f;
                break;
            case TardigradeCell.Muscle:
                baseSize = 1.0f;
                break;
            case TardigradeCell.Nerve:
                baseSize = 0.7f;
                break;
            case TardigradeCell.Digestive:
                baseSize = 0.9f;
                break;
            case TardigradeCell.Storage:
                baseSize = 1.1f;
                break;
            case TardigradeCell.Reproductive:
                baseSize = 1.0f;
                break;
            case TardigradeCell.Leg:
                baseSize = 0.7f;
                break;
        }

        // Adjust by energy level
        baseSize *= 0.8f + (cell.energy * 0.4f);

        // Adjust by metabolic state
        if (cell.metabolicState == MetabolicState.Tun)
        {
            baseSize *= 0.7f; // Smaller in cryptobiosis
        }

        return baseSize;
    }

    void OnRenderObject()
    {
        if (cellMaterial != null)
        {
            cellMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, cellData.Length);
        }
    }

    void OnDestroy()
    {
        // Clean up memory
        if (cellData.IsCreated) cellData.Dispose();
        if (initialPositions.IsCreated) initialPositions.Dispose();
        if (targetPositions.IsCreated) targetPositions.Dispose();

        if (cellBuffer != null) cellBuffer.Release();
    }
}

// Job for cell movement
[BurstCompile]
public struct CellMovementJob : IJobParallelFor
{
    public NativeArray<TardigradeCellData> cellData;
    [ReadOnly] public NativeArray<float3> targetPositions;
    public float deltaTime;
    public float movementSpeed;
    public float simulationTime;

    public void Execute(int index)
    {
        TardigradeCellData cell = cellData[index];

        // Skip cells in cryptobiosis
        if (cell.metabolicState == MetabolicState.Tun ||
            cell.metabolicState == MetabolicState.Anhydrobiotic)
        {
            return;
        }

        // Calculate direction to target
        float3 direction = targetPositions[index] - cell.position;
        float distance = math.length(direction);

        // Move toward target if not at destination
        if (distance > 0.01f)
        {
            // Normalize direction
            direction = math.normalize(direction);

            // Calculate speed based on cell type and state
            float speed = movementSpeed;

            if (cell.cellType == TardigradeCell.Muscle)
            {
                speed *= 1.0f + (cell.contractionState * 0.5f);
            }
            else if (cell.isLegCell)
            {
                speed *= 1.5f; // Legs move faster
            }

            // Adjust by metabolic state
            if (cell.metabolicState == MetabolicState.Stressed)
            {
                speed *= 0.5f;
            }

            // Calculate velocity
            float3 targetVelocity = direction * speed;

            // Smooth movement
            cell.velocity = math.lerp(cell.velocity, targetVelocity, deltaTime * 5.0f);

            // Update position
            cell.position += cell.velocity * deltaTime;
        }
        else
        {
            // At destination, slow down
            cell.velocity *= 0.9f;
        }

        // Apply small random movement for liveliness
        if (cell.metabolicState == MetabolicState.Active)
        {
            float jitter = 0.05f;
            cell.position += new float3(
                (Unity.Mathematics.noise.snoise(new float2(index, simulationTime * 10)) - 0.5f) * jitter * deltaTime,
                (Unity.Mathematics.noise.snoise(new float2(index + 100, simulationTime * 10)) - 0.5f) * jitter * deltaTime,
                (Unity.Mathematics.noise.snoise(new float2(index + 200, simulationTime * 10)) - 0.5f) * jitter * deltaTime
            );
        }

        cellData[index] = cell;
    }
}