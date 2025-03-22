using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

[Serializable]
public class Cell
{
    public int id;
    public int cellType; // 0: Epidermal, 1: Muscle, 2: Digestive, 3: Nervous
    public float energy = 1.0f;
    public float waterContent = 1.0f;
    public float3 stress;
    public bool isInCryptobiosis = false;
    public int cryptobiosisType = 0;
    public float timeInCryptobiosis = 0f;
    public float trehaloseLevel = 0.1f;

    // Reference to the visual GameObject
    [NonSerialized] public GameObject visualObject;
}

[Serializable]
public class CellConnection
{
    public int cell1Id;
    public int cell2Id;
    public float strength = 1.0f;

    // Reference to the visual GameObject
    [NonSerialized] public GameObject visualObject;
}

/// <summary>
/// Main controller script for the tardigrade cellular simulation.
/// Handles cell generation, physics, cryptobiosis, and visualization.
/// </summary>
public class TardigradeSimulation : MonoBehaviour
{
    // ================ INSPECTOR SETTINGS ================

    [Range(0f, 100f)]
    public float SimulationSpeed = 1.0f;

    [Header("Simulation Settings")]
    [Tooltip("Number of cells to simulate")]
    [SerializeField] private int totalCellCount = 1000;

    [Tooltip("Distribution of cell types")]
    [SerializeField, Range(0f, 1f)] private float epidermalCellPercentage = 0.4f;
    [SerializeField, Range(0f, 1f)] private float muscleCellPercentage = 0.3f;
    [SerializeField, Range(0f, 1f)] private float digestiveCellPercentage = 0.2f;
    [SerializeField, Range(0f, 1f)] private float nervousCellPercentage = 0.1f;

    [Header("Environment")]
    [SerializeField, Range(-50f, 100f)] private float temperature = 20f;
    [SerializeField, Range(0f, 1f)] private float humidity = 0.8f;
    [SerializeField, Range(0f, 5000f)] private float radiation = 0f;
    [SerializeField, Range(0f, 1f)] private float oxygen = 0.95f;
    [SerializeField, Range(0f, 1f)] private float toxicity = 0f;

    [Header("Prefabs")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject connectionPrefab;

    [Header("Materials")]
    [SerializeField] private Material epidermalCellMaterial;
    [SerializeField] private Material muscleCellMaterial;
    [SerializeField] private Material digestiveCellMaterial;
    [SerializeField] private Material nervousCellMaterial;

    [Header("Visualization")]
    [SerializeField] private bool showConnections = true;
    [Tooltip("Visualization mode: 0=Normal, 1=Energy, 2=Water, 3=Trehalose, 4=Stress")]
    [SerializeField, Range(0, 4)] private int visualizationMode = 0;

    [Header("Cell Filter")]
    [SerializeField] private bool showEpidermalCells = true;
    [SerializeField] private bool showMuscleCells = true;
    [SerializeField] private bool showDigestiveCells = true;
    [SerializeField] private bool showNervousCells = true;

    [Header("Camera")]
    [SerializeField] private Camera simulationCamera;
    [SerializeField] private float movementSpeed = 0.5f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float minZoom = 0.05f;
    [SerializeField] private float maxZoom = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = false;
    [SerializeField] private bool logDebugInfo = false;
    [SerializeField] private bool visualizeCellForces = false;

    // ================ PRIVATE VARIABLES ================

    // Containers
    private Transform cellContainer;
    private Transform connectionContainer;

    // Data structures
    private List<Cell> cells = new List<Cell>();
    private List<CellConnection> cellConnections = new List<CellConnection>();

    // Native arrays for jobs
    private NativeArray<float3> cellPositions;
    private NativeArray<float3> cellVelocities;
    private NativeArray<int> cellTypes;
    private NativeArray<float> cellEnergies;
    private NativeArray<float> cellWaterContents;
    private NativeArray<float3> cellStresses;
    private NativeArray<bool> cellCryptobiosisStates;
    private NativeArray<float> cellTrehaloseLevels;

    // Transform access arrays for jobs
    private TransformAccessArray cellTransforms;

    // Camera control variables
    private Vector3 targetCameraPosition;
    private Quaternion targetCameraRotation;
    private float targetZoom;
    private float currentZoom;
    private Vector3 lastMousePosition;
    private bool isRotating = false;
    private bool isPanning = false;

    // Simulation stats
    private float simulationTime = 0f;
    private int cryptobiosisCellCount = 0;

    // ================ UNITY LIFECYCLE ================

    private void Start()
    {
        // Create containers
        CreateContainers();

        // Initialize the simulation
        InitializeSimulation();

        // Initialize camera
        InitializeCamera();
    }

    private void Update()
    {
        // Update environment (if using Inspector)
        UpdateEnvironmentFromInspector();

        // Schedule jobs
        ScheduleJobs();

        // Update visual representation
        UpdateVisuals();

        // Update cell visibility based on filter settings
        UpdateCellVisibility();

        // Handle camera control
        HandleCameraControls();

        // Update simulation statistics
        UpdateSimulationStats();

        Time.timeScale = SimulationSpeed;
    }

    private void OnDestroy()
    {
        // Clean up native arrays
        DisposeNativeArrays();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw outline of tardigrade shape
        Gizmos.color = Color.cyan;
        float tardigradeLength = 0.5f;
        float tardigradeWidth = 0.1f;
        float tardigradeHeight = 0.1f;

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        // Draw ellipsoid body
        DrawEllipsoid(Vector3.zero, new Vector3(tardigradeLength / 2, tardigradeWidth / 2, tardigradeHeight / 2));

        // Draw legs if cells are initialized
        if (cells.Count > 0)
        {
            Gizmos.color = Color.yellow;

            // Find all epidermal cells in leg positions
            foreach (var cell in cells)
            {
                if (cell.cellType == 0 && cell.visualObject != null)
                {
                    Vector3 pos = cell.visualObject.transform.position;

                    // Check if this is likely a leg position
                    if (Mathf.Abs(pos.y) > tardigradeWidth * 0.4f && Mathf.Abs(pos.z) < tardigradeHeight * 0.3f)
                    {
                        Gizmos.DrawSphere(pos, 0.005f);
                    }
                }
            }
        }

        Gizmos.matrix = originalMatrix;
    }

    // ================ INITIALIZATION ================

    private void CreateContainers()
    {
        // Create cell container
        GameObject cellContainerObj = new GameObject("Cells");
        cellContainerObj.transform.SetParent(transform);
        cellContainer = cellContainerObj.transform;

        // Create connection container
        GameObject connContainerObj = new GameObject("Connections");
        connContainerObj.transform.SetParent(transform);
        connectionContainer = connContainerObj.transform;
    }

    private void InitializeSimulation()
    {
        // Generate cells
        GenerateCells();

        // Establish connections between cells
        EstablishCellConnections();

        // Initialize native arrays for jobs
        InitializeNativeArrays();

        // Log info
        if (logDebugInfo)
        {
            Debug.Log($"Initialized tardigrade simulation with {cells.Count} cells and {cellConnections.Count} connections");
        }
    }

    private void InitializeCamera()
    {
        // If no camera assigned, use the main camera
        if (simulationCamera == null)
        {
            simulationCamera = Camera.main;
        }

        if (simulationCamera != null)
        {
            // Initialize target values
            targetCameraPosition = simulationCamera.transform.position;
            targetCameraRotation = simulationCamera.transform.rotation;

            if (simulationCamera.orthographic)
            {
                targetZoom = currentZoom = simulationCamera.orthographicSize;
            }
            else
            {
                // For perspective camera, use distance as zoom
                targetZoom = currentZoom = 0.5f;
            }
        }
    }

    // ================ CELL GENERATION ================

    private void GenerateCells()
    {
        // Clear existing cells
        cells.Clear();

        // Destroy existing cell objects
        foreach (Transform child in cellContainer)
        {
            Destroy(child.gameObject);
        }

        // Generate cells
        for (int i = 0; i < totalCellCount; i++)
        {
            // Determine cell type
            float cellTypeRandom = UnityEngine.Random.value;
            int cellType;

            // Distribute cell types according to their percentages
            if (cellTypeRandom < epidermalCellPercentage)
                cellType = 0; // Epidermal
            else if (cellTypeRandom < epidermalCellPercentage + muscleCellPercentage)
                cellType = 1; // Muscle
            else if (cellTypeRandom < epidermalCellPercentage + muscleCellPercentage + digestiveCellPercentage)
                cellType = 2; // Digestive
            else
                cellType = 3; // Nervous

            // Create cell data
            Cell cell = new Cell
            {
                id = i,
                cellType = cellType,
                energy = UnityEngine.Random.Range(0.7f, 1f),
                waterContent = UnityEngine.Random.Range(0.8f, 1f),
                stress = float3.zero,
                trehaloseLevel = UnityEngine.Random.Range(0.05f, 0.15f)
            };

            cells.Add(cell);

            // Position the cell based on type and tardigrade anatomy
            Vector3 position = CalculateCellPosition(i, cellType, totalCellCount);

            // Create visual representation
            GameObject cellObject = Instantiate(cellPrefab, position, Quaternion.identity, cellContainer);
            cellObject.name = $"Cell_{i}_{GetCellTypeName(cellType)}";

            // Store reference to visual object
            cell.visualObject = cellObject;

            // Set material based on cell type
            Renderer renderer = cellObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = null;

                switch (cellType)
                {
                    case 0: // Epidermal
                        material = epidermalCellMaterial;
                        break;
                    case 1: // Muscle
                        material = muscleCellMaterial;
                        break;
                    case 2: // Digestive
                        material = digestiveCellMaterial;
                        break;
                    case 3: // Nervous
                        material = nervousCellMaterial;
                        break;
                }

                if (material != null)
                {
                    renderer.material = new Material(material); // Create instance for individual modifications
                }
            }
        }
    }

    private Vector3 CalculateCellPosition(int index, int cellType, int totalCount)
    {
        // Body length of a tardigrade is about 0.5mm, scale to Unity units
        float tardigradeLength = 0.5f;
        float tardigradeWidth = 0.1f;
        float tardigradeHeight = 0.1f;

        Vector3 position = Vector3.zero;

        switch (cellType)
        {
            case 0: // Epidermal cells - form the outer layer
                // Create a roughly ellipsoid shape with 8 leg protrusions
                float t = (float)index / totalCount * 2f * Mathf.PI;
                float bodySection = (float)index / totalCount * tardigradeLength;

                // Basic ellipsoid body
                position.x = bodySection - tardigradeLength / 2f;
                position.y = Mathf.Sin(t) * tardigradeWidth / 2f * (0.7f + 0.3f * Mathf.Sin(5f * bodySection));
                position.z = Mathf.Cos(t) * tardigradeHeight / 2f * (0.7f + 0.3f * Mathf.Sin(5f * bodySection));

                // Add some noise for natural appearance
                position += new Vector3(
                    UnityEngine.Random.Range(-0.01f, 0.01f),
                    UnityEngine.Random.Range(-0.01f, 0.01f),
                    UnityEngine.Random.Range(-0.01f, 0.01f)
                );
                break;

            case 1: // Muscle cells
                // Muscle cells connect to the epidermis but are slightly inward
                t = (float)index / totalCount * 2f * Mathf.PI;
                bodySection = (float)index / totalCount * tardigradeLength;

                position.x = bodySection - tardigradeLength / 2f;
                position.y = Mathf.Sin(t) * tardigradeWidth / 2f * 0.8f;
                position.z = Mathf.Cos(t) * tardigradeHeight / 2f * 0.8f;

                // Add noise for natural muscle attachment points
                position += new Vector3(
                    UnityEngine.Random.Range(-0.02f, 0.02f),
                    UnityEngine.Random.Range(-0.02f, 0.02f),
                    UnityEngine.Random.Range(-0.02f, 0.02f)
                );
                break;

            case 2: // Digestive cells
                // Form a central tube structure
                bodySection = (float)index / totalCount * tardigradeLength;
                float radius = 0.02f;
                t = (float)index / totalCount * 2f * Mathf.PI;

                position.x = bodySection - tardigradeLength / 2f;
                position.y = Mathf.Sin(t) * radius;
                position.z = Mathf.Cos(t) * radius;
                break;

            case 3: // Nervous cells
                // Concentrate at the head with a central nerve cord
                float headBias = Mathf.Pow(UnityEngine.Random.value, 2f); // Concentrate more cells at head

                position.x = (headBias * tardigradeLength * 0.7f) - tardigradeLength / 2f;
                position.y = UnityEngine.Random.Range(-0.03f, 0.03f);
                position.z = UnityEngine.Random.Range(-0.03f, 0.03f);
                break;
        }

        return position;
    }

    private void EstablishCellConnections()
    {
        // Clear existing connections
        cellConnections.Clear();

        // Destroy existing connection objects
        foreach (Transform child in connectionContainer)
        {
            Destroy(child.gameObject);
        }

        // Only show connections if enabled
        if (!showConnections) return;

        // Calculate connections based on proximity and cell type
        float connectionRadius = 0.02f; // Adjust based on cell density

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell1 = cells[i];
            Vector3 pos1 = cell1.visualObject.transform.position;

            for (int j = i + 1; j < cells.Count; j++) // Start from i+1 to avoid duplicates
            {
                Cell cell2 = cells[j];
                Vector3 pos2 = cell2.visualObject.transform.position;

                // Check distance
                float distanceSq = Vector3.SqrMagnitude(pos1 - pos2);
                if (distanceSq > connectionRadius * connectionRadius) continue;

                // Determine if these cells should connect based on type
                bool shouldConnect = false;

                switch (cell1.cellType)
                {
                    case 0: // Epidermal connects to other epidermal and muscle
                        shouldConnect = cell2.cellType == 0 || cell2.cellType == 1;
                        break;
                    case 1: // Muscle connects to epidermal, other muscle, and nervous
                        shouldConnect = cell2.cellType == 0 || cell2.cellType == 1 || cell2.cellType == 3;
                        break;
                    case 2: // Digestive connects to other digestive and nervous
                        shouldConnect = cell2.cellType == 2 || cell2.cellType == 3;
                        break;
                    case 3: // Nervous connects to all types
                        shouldConnect = true;
                        break;
                }

                if (shouldConnect)
                {
                    // Create connection data
                    CellConnection connection = new CellConnection
                    {
                        cell1Id = cell1.id,
                        cell2Id = cell2.id,
                        strength = 1f - Mathf.Sqrt(distanceSq) / connectionRadius // Strength based on distance
                    };

                    cellConnections.Add(connection);

                    // Visualize connection
                    Vector3 midPoint = (pos1 + pos2) / 2f;
                    GameObject connObject = Instantiate(connectionPrefab, midPoint, Quaternion.identity, connectionContainer);
                    connObject.name = $"Connection_{cell1.id}_{cell2.id}";

                    // Position and scale the connection to match the cells
                    Vector3 direction = pos2 - pos1;
                    float distance = direction.magnitude;

                    // Look at target
                    connObject.transform.LookAt(pos2);

                    // Scale to match the distance
                    connObject.transform.localScale = new Vector3(0.001f, 0.001f, distance);

                    // Store reference to visual object
                    connection.visualObject = connObject;

                    // Set line renderer properties if available
                    LineRenderer lineRenderer = connObject.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        Color lineColor = Color.white;
                        lineColor.a = connection.strength;
                        lineRenderer.startColor = lineColor;
                        lineRenderer.endColor = lineColor;
                        lineRenderer.widthMultiplier = connection.strength * 0.002f;

                        // Set positions
                        lineRenderer.SetPosition(0, pos1);
                        lineRenderer.SetPosition(1, pos2);
                    }
                }
            }
        }
    }

    private void InitializeNativeArrays()
    {
        // Allocate native arrays for jobs
        cellPositions = new NativeArray<float3>(cells.Count, Allocator.Persistent);
        cellVelocities = new NativeArray<float3>(cells.Count, Allocator.Persistent);
        cellTypes = new NativeArray<int>(cells.Count, Allocator.Persistent);
        cellEnergies = new NativeArray<float>(cells.Count, Allocator.Persistent);
        cellWaterContents = new NativeArray<float>(cells.Count, Allocator.Persistent);
        cellStresses = new NativeArray<float3>(cells.Count, Allocator.Persistent);
        cellCryptobiosisStates = new NativeArray<bool>(cells.Count, Allocator.Persistent);
        cellTrehaloseLevels = new NativeArray<float>(cells.Count, Allocator.Persistent);

        // Initialize transform access array
        Transform[] transforms = new Transform[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            transforms[i] = cells[i].visualObject.transform;
        }
        cellTransforms = new TransformAccessArray(transforms);

        // Initialize data
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            cellPositions[i] = cells[i].visualObject.transform.position;
            cellVelocities[i] = float3.zero;
            cellTypes[i] = cell.cellType;
            cellEnergies[i] = cell.energy;
            cellWaterContents[i] = cell.waterContent;
            cellStresses[i] = cell.stress;
            cellCryptobiosisStates[i] = cell.isInCryptobiosis;
            cellTrehaloseLevels[i] = cell.trehaloseLevel;
        }
    }

    // ================ JOB SCHEDULING ================

    // Update this method in your TardigradeSimulation class to fix the job dependency issue
    private void ScheduleJobs()
    {
        float deltaTime = Time.deltaTime;

        // Step 1: Create temporary arrays for forces
        NativeArray<float3> cellForces = new NativeArray<float3>(cells.Count, Allocator.TempJob);

        // Step 2: Schedule environment update job first
        JobHandle environmentJobHandle = new EnvironmentUpdateJob
        {
            DeltaTime = deltaTime,
            Temperature = temperature,
            Humidity = humidity,
            Radiation = radiation,
            Oxygen = oxygen,
            Toxicity = toxicity,
            CellEnergies = cellEnergies,
            CellWaterContents = cellWaterContents,
            CellCryptobiosisStates = cellCryptobiosisStates
        }.Schedule(cells.Count, 64);

        // Step 3: Schedule force calculation job (depends on environment update)
        JobHandle forceJobHandle = new CalculateCellForcesJob
        {
            Positions = cellPositions,
            CellTypes = cellTypes,
            Forces = cellForces
        }.Schedule(cells.Count, 32, environmentJobHandle);

        // Step 4: Schedule cryptobiosis update job (depends on environment update)
        // IMPORTANT: Cryptobiosis needs to complete before movement because it modifies CellCryptobiosisStates
        JobHandle cryptobiosisJobHandle = new CryptobiosisUpdateJob
        {
            DeltaTime = deltaTime,
            Temperature = temperature,
            Humidity = humidity,
            Radiation = radiation,
            Oxygen = oxygen,
            Toxicity = toxicity,
            CellEnergies = cellEnergies,
            CellWaterContents = cellWaterContents,
            CellCryptobiosisStates = cellCryptobiosisStates,
            CellTypes = cellTypes
        }.Schedule(cells.Count, 64, environmentJobHandle);

        // Step 5: Schedule trehalose production job (depends on cryptobiosis)
        JobHandle trehaloseJobHandle = new TrehaloseProductionJob
        {
            DeltaTime = deltaTime,
            CellCryptobiosisStates = cellCryptobiosisStates,
            TrehaloseLevels = cellTrehaloseLevels
        }.Schedule(cells.Count, 64, cryptobiosisJobHandle);

        // Step 6: Schedule cell movement job - depends on BOTH force calculation AND cryptobiosis update
        // Create a combined dependency using JobHandle.CombineDependencies
        JobHandle combinedDependency = JobHandle.CombineDependencies(forceJobHandle, cryptobiosisJobHandle);

        JobHandle movementJobHandle = new MoveCellsJob
        {
            DeltaTime = deltaTime,
            Positions = cellPositions,
            Velocities = cellVelocities,
            Forces = cellForces,
            CellCryptobiosisStates = cellCryptobiosisStates
        }.Schedule(cellTransforms, combinedDependency);

        // Step 7: Wait for all jobs to complete
        // We need to make sure both the movement job and trehalose job finish 
        JobHandle finalDependency = JobHandle.CombineDependencies(movementJobHandle, trehaloseJobHandle);
        finalDependency.Complete();

        // Clean up temporary arrays
        cellForces.Dispose();

        // Update cell data from native arrays
        UpdateCellDataFromNativeArrays();

        // Update connection positions
        UpdateConnectionPositions();
    }

    private void UpdateCellDataFromNativeArrays()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            cell.energy = cellEnergies[i];
            cell.waterContent = cellWaterContents[i];
            cell.stress = cellStresses[i];
            cell.isInCryptobiosis = cellCryptobiosisStates[i];
            cell.trehaloseLevel = cellTrehaloseLevels[i];
        }
    }

    private void UpdateConnectionPositions()
    {
        if (!showConnections) return;

        foreach (var connection in cellConnections)
        {
            if (connection.visualObject == null) continue;

            // Get positions of connected cells
            if (connection.cell1Id >= cells.Count || connection.cell2Id >= cells.Count) continue;

            Cell cell1 = cells[connection.cell1Id];
            Cell cell2 = cells[connection.cell2Id];

            if (cell1.visualObject == null || cell2.visualObject == null) continue;

            Vector3 pos1 = cell1.visualObject.transform.position;
            Vector3 pos2 = cell2.visualObject.transform.position;

            // Update line renderer
            LineRenderer lineRenderer = connection.visualObject.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, pos1);
                lineRenderer.SetPosition(1, pos2);

                // Check if either cell is in cryptobiosis and update connection appearance
                bool cell1Crypto = cell1.isInCryptobiosis;
                bool cell2Crypto = cell2.isInCryptobiosis;

                if (cell1Crypto || cell2Crypto)
                {
                    // When in cryptobiosis, connections become more rigid
                    Color lineColor = new Color(0.7f, 0.7f, 0.7f, connection.strength * 0.7f);
                    lineRenderer.startColor = lineColor;
                    lineRenderer.endColor = lineColor;
                    lineRenderer.widthMultiplier = connection.strength * 0.001f;
                }
                else
                {
                    // Normal connections
                    Color lineColor = new Color(1f, 1f, 1f, connection.strength);
                    lineRenderer.startColor = lineColor;
                    lineRenderer.endColor = lineColor;
                    lineRenderer.widthMultiplier = connection.strength * 0.002f;
                }
            }

            // Update connection position and rotation
            Vector3 midPoint = (pos1 + pos2) / 2f;
            connection.visualObject.transform.position = midPoint;

            Vector3 direction = pos2 - pos1;
            float distance = direction.magnitude;

            // Look at target
            connection.visualObject.transform.LookAt(pos2);

            // Scale to match the distance
            connection.visualObject.transform.localScale = new Vector3(0.001f, 0.001f, distance);
        }
    }

    // ================ VISUALIZATION ================

    private void UpdateVisuals()
    {
        // Update cell visualizations based on current mode
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];

            if (cell.visualObject == null) continue;

            // Update appearance based on cell state
            Renderer renderer = cell.visualObject.GetComponent<Renderer>();
            if (renderer == null) continue;

            Color baseColor = GetCellTypeColor(cell.cellType);
            Color finalColor = baseColor;

            switch (visualizationMode)
            {
                case 0: // Normal
                    // Base color with energy and water adjustments
                    float energyFactor = Mathf.Lerp(0.5f, 1f, cell.energy);
                    float waterFactor = Mathf.Lerp(0.3f, 1f, cell.waterContent);

                    finalColor = baseColor * energyFactor;
                    finalColor.a = waterFactor;
                    break;

                case 1: // Energy
                    // Heat map for energy (red = high, blue = low)
                    finalColor = Color.Lerp(Color.blue, Color.red, cell.energy);
                    break;

                case 2: // Water
                    // Blue gradient for water content
                    finalColor = Color.Lerp(Color.white, Color.blue, cell.waterContent);
                    break;

                case 3: // Trehalose
                    // Yellow gradient for trehalose level
                    finalColor = Color.Lerp(Color.white, Color.yellow, cell.trehaloseLevel);
                    break;

                case 4: // Stress
                    // Green to red gradient for stress
                    float stressLevel = math.length(cell.stress) / 0.05f; // Normalize stress
                    stressLevel = Mathf.Clamp01(stressLevel);
                    finalColor = Color.Lerp(Color.green, Color.red, stressLevel);
                    break;
            }

            // Cryptobiosis state affects appearance
            if (cell.isInCryptobiosis)
            {
                switch (cell.cryptobiosisType)
                {
                    case 1: // Anhydrobiosis - dry and compact
                        finalColor = Color.Lerp(finalColor, new Color(0.7f, 0.6f, 0.5f, 0.7f), 0.7f);
                        cell.visualObject.transform.localScale = Vector3.one * 0.7f;
                        break;

                    case 2: // Cryobiosis - frozen appearance
                        finalColor = Color.Lerp(finalColor, new Color(0.8f, 0.9f, 1f, 0.9f), 0.6f);
                        cell.visualObject.transform.localScale = Vector3.one * 0.9f;
                        break;

                    case 3: // Chemobiosis - toxic resistance
                        finalColor = Color.Lerp(finalColor, new Color(0.7f, 0.5f, 0.7f, 0.8f), 0.5f);
                        cell.visualObject.transform.localScale = Vector3.one * 0.8f;
                        break;

                    case 4: // Anoxybiosis - oxygen deprivation
                        finalColor = Color.Lerp(finalColor, new Color(0.5f, 0.3f, 0.3f, 0.85f), 0.4f);
                        cell.visualObject.transform.localScale = Vector3.one * 0.85f;
                        break;
                }
            }
            else
            {
                // Normal cell size
                cell.visualObject.transform.localScale = Vector3.one;
            }

            // Apply trehalose visualization for anhydrobiosis resistance
            if (cell.trehaloseLevel > 0.3f && cell.waterContent < 0.5f)
            {
                // Trehalose gives a slight golden shine
                finalColor = Color.Lerp(finalColor, new Color(1f, 0.9f, 0.6f, finalColor.a), cell.trehaloseLevel * 0.3f);
            }

            // Apply the color to the material
            renderer.material.color = finalColor;

            // Debug visualization of forces if enabled
            if (visualizeCellForces && cell.stress.y > 0.01f)
            {
                Debug.DrawRay(cell.visualObject.transform.position, new Vector3(cell.stress.x, cell.stress.y, cell.stress.z) * 10f, Color.red);
            }
        }
    }

    private void UpdateCellVisibility()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];

            if (cell.visualObject == null) continue;

            bool shouldBeVisible = false;

            switch (cell.cellType)
            {
                case 0: // Epidermal
                    shouldBeVisible = showEpidermalCells;
                    break;
                case 1: // Muscle
                    shouldBeVisible = showMuscleCells;
                    break;
                case 2: // Digestive
                    shouldBeVisible = showDigestiveCells;
                    break;
                case 3: // Nervous
                    shouldBeVisible = showNervousCells;
                    break;
            }

            cell.visualObject.SetActive(shouldBeVisible);
        }
    }

    // ================ CAMERA CONTROLS ================

    private void HandleCameraControls()
    {
        if (simulationCamera == null) return;

        // Mouse wheel for zoom
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (scrollDelta != 0)
        {
            targetZoom = Mathf.Clamp(targetZoom - scrollDelta * zoomSpeed, minZoom, maxZoom);
        }

        // Right mouse button for rotation
        if (Input.GetMouseButtonDown(1))
        {
            isRotating = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isRotating = false;
        }

        // Middle mouse button for panning
        if (Input.GetMouseButtonDown(2))
        {
            isPanning = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        // Handle rotation
        if (isRotating)
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;

            // Rotate around Y axis (left/right)
            targetCameraRotation *= Quaternion.Euler(0, mouseDelta.x * rotationSpeed * Time.deltaTime, 0);

            // Rotate around X axis (up/down)
            targetCameraRotation *= Quaternion.Euler(-mouseDelta.y * rotationSpeed * Time.deltaTime, 0, 0);

            lastMousePosition = Input.mousePosition;
        }

        // Handle panning
        if (isPanning)
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            Vector3 move = new Vector3(-mouseDelta.x, -mouseDelta.y, 0) * movementSpeed * currentZoom * Time.deltaTime;

            // Transform the movement direction from screen space to world space
            move = simulationCamera.transform.TransformDirection(move);
            move.y = 0; // Optional: restrict to horizontal plane

            targetCameraPosition += move;

            lastMousePosition = Input.mousePosition;
        }

        // WASD keys for movement
        Vector3 moveDir = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            moveDir += simulationCamera.transform.forward;
        if (Input.GetKey(KeyCode.S))
            moveDir -= simulationCamera.transform.forward;
        if (Input.GetKey(KeyCode.A))
            moveDir -= simulationCamera.transform.right;
        if (Input.GetKey(KeyCode.D))
            moveDir += simulationCamera.transform.right;

        // Normalize and apply movement
        if (moveDir.magnitude > 0.01f)
        {
            moveDir.Normalize();
            moveDir.y = 0; // Optional: restrict to horizontal plane
            targetCameraPosition += moveDir * movementSpeed * currentZoom * Time.deltaTime;
        }

        // Q and E for rotation around Y axis
        if (Input.GetKey(KeyCode.Q))
            targetCameraRotation *= Quaternion.Euler(0, -rotationSpeed * Time.deltaTime * 10f, 0);
        if (Input.GetKey(KeyCode.E))
            targetCameraRotation *= Quaternion.Euler(0, rotationSpeed * Time.deltaTime * 10f, 0);

        // Update camera
        UpdateCameraTransform();
    }

    private void UpdateCameraTransform()
    {
        // Smoothly update zoom
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * 5f);

        if (simulationCamera.orthographic)
        {
            simulationCamera.orthographicSize = currentZoom;
        }
        else
        {
            // For perspective camera, adjust distance instead
            float distanceToTarget = Vector3.Distance(simulationCamera.transform.position, targetCameraPosition);
            Vector3 direction = (simulationCamera.transform.position - targetCameraPosition).normalized;

            // Adjust distance based on zoom
            float targetDistance = Mathf.Lerp(0.1f, 2.0f, currentZoom / maxZoom);
            Vector3 newPosition = targetCameraPosition + direction * targetDistance;

            simulationCamera.transform.position = Vector3.Lerp(simulationCamera.transform.position, newPosition, Time.deltaTime * 5f);
        }

        // Smoothly update position and rotation
        simulationCamera.transform.position = Vector3.Lerp(simulationCamera.transform.position, targetCameraPosition, Time.deltaTime * 5f);
        simulationCamera.transform.rotation = Quaternion.Slerp(simulationCamera.transform.rotation, targetCameraRotation, Time.deltaTime * 5f);
    }

    // ================ SIMULATION STATS ================

    private void UpdateSimulationStats()
    {
        simulationTime += Time.deltaTime;

        // Count cells in cryptobiosis
        cryptobiosisCellCount = 0;
        foreach (var cell in cells)
        {
            if (cell.isInCryptobiosis)
                cryptobiosisCellCount++;
        }

        // Log debug info if enabled
        if (logDebugInfo && Time.frameCount % 60 == 0) // Once per second at 60 FPS
        {
            float avgEnergy = GetAverageCellEnergy();
            float avgWater = GetAverageCellWaterContent();
            float avgTrehalose = GetAverageTrehaloseLevel();
            float cryptoPercentage = cells.Count > 0 ? (float)cryptobiosisCellCount / cells.Count * 100f : 0f;

            Debug.Log($"Simulation Time: {simulationTime:F1}s | Cells: {cells.Count} | Cryptobiosis: {cryptobiosisCellCount} ({cryptoPercentage:F1}%) | Avg Energy: {avgEnergy:F2} | Avg Water: {avgWater:F2} | Avg Trehalose: {avgTrehalose:F2}");
        }
    }

    private float GetAverageCellEnergy()
    {
        if (cells.Count == 0) return 0;

        float total = 0;
        foreach (var cell in cells)
        {
            total += cell.energy;
        }
        return total / cells.Count;
    }

    private float GetAverageCellWaterContent()
    {
        if (cells.Count == 0) return 0;

        float total = 0;
        foreach (var cell in cells)
        {
            total += cell.waterContent;
        }
        return total / cells.Count;
    }

    private float GetAverageTrehaloseLevel()
    {
        if (cells.Count == 0) return 0;

        float total = 0;
        foreach (var cell in cells)
        {
            total += cell.trehaloseLevel;
        }
        return total / cells.Count;
    }

    // ================ ENVIRONMENT ================

    private void UpdateEnvironmentFromInspector()
    {
        // Nothing to do here since we're using the Inspector values directly
        // But this could be extended to handle dynamic environment changes
    }

    // ================ HELPER METHODS ================

    private string GetCellTypeName(int cellType)
    {
        switch (cellType)
        {
            case 0: return "Epidermal";
            case 1: return "Muscle";
            case 2: return "Digestive";
            case 3: return "Nervous";
            default: return "Unknown";
        }
    }

    private Color GetCellTypeColor(int cellType)
    {
        switch (cellType)
        {
            case 0: return new Color(0.8f, 0.8f, 0.9f); // Epidermal - light blue-gray
            case 1: return new Color(0.8f, 0.3f, 0.3f); // Muscle - red
            case 2: return new Color(0.3f, 0.8f, 0.4f); // Digestive - green
            case 3: return new Color(0.9f, 0.9f, 0.2f); // Nervous - yellow
            default: return Color.gray;
        }
    }

    private void DrawEllipsoid(Vector3 position, Vector3 radius)
    {
        // Draw three orthogonal circles
        DrawCircle(position, radius.y, radius.z, Vector3.right, 32);
        DrawCircle(position, radius.x, radius.z, Vector3.up, 32);
        DrawCircle(position, radius.x, radius.y, Vector3.forward, 32);
    }

    private void DrawCircle(Vector3 position, float radiusX, float radiusY, Vector3 axis, int segments)
    {
        // Create basis vectors
        Vector3 forward = axis;
        Vector3 up = (forward.z != 0 || forward.x != 0) ? Vector3.up : Vector3.forward;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        up = Vector3.Cross(forward, right).normalized;

        // Draw segments
        float angle = 0f;
        float angleStep = 2f * Mathf.PI / segments;
        Vector3 prevPoint = position + right * radiusX * Mathf.Cos(angle) + up * radiusY * Mathf.Sin(angle);

        for (int i = 0; i < segments + 1; i++)
        {
            angle += angleStep;
            Vector3 nextPoint = position + right * radiusX * Mathf.Cos(angle) + up * radiusY * Mathf.Sin(angle);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    // ================ PUBLIC METHODS ================

    /// <summary>
    /// Resets the simulation with current parameters
    /// </summary>
    public void ResetSimulation()
    {
        // Dispose native arrays
        DisposeNativeArrays();

        // Reinitialize the simulation
        InitializeSimulation();

        // Reset simulation time
        simulationTime = 0f;
    }

    /// <summary>
    /// Changes the environment to simulate extreme drought
    /// </summary>
    public void SimulateExtremeDrought()
    {
        humidity = 0.01f;
    }

    /// <summary>
    /// Changes the environment to simulate extreme cold
    /// </summary>
    public void SimulateExtremeCold()
    {
        temperature = -20f;
    }

    /// <summary>
    /// Changes the environment to simulate extreme radiation
    /// </summary>
    public void SimulateExtremeRadiation()
    {
        radiation = 2000f;
    }

    /// <summary>
    /// Changes the environment to simulate oxygen deprivation
    /// </summary>
    public void SimulateOxygenDeprivation()
    {
        oxygen = 0.05f;
    }

    /// <summary>
    /// Changes the environment to simulate toxic exposure
    /// </summary>
    public void SimulateToxicExposure()
    {
        toxicity = 0.8f;
    }

    /// <summary>
    /// Restores normal environmental conditions
    /// </summary>
    public void RestoreNormalConditions()
    {
        temperature = 20f;
        humidity = 0.8f;
        radiation = 0f;
        oxygen = 0.95f;
        toxicity = 0f;
    }

    /// <summary>
    /// Exports simulation data to CSV file
    /// </summary>
    public void ExportSimulationData()
    {
        string path = Application.dataPath + "/TardigradeSimulationData.csv";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Write header
        sb.AppendLine("CellID,CellType,Energy,WaterContent,TrehaloseLevel,InCryptobiosis,X,Y,Z");

        // Write cell data
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            Vector3 position = cell.visualObject.transform.position;

            sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                i,
                cell.cellType,
                cell.energy,
                cell.waterContent,
                cell.trehaloseLevel,
                cell.isInCryptobiosis ? 1 : 0,
                position.x,
                position.y,
                position.z));
        }

        // Write to file
        System.IO.File.WriteAllText(path, sb.ToString());

        Debug.Log("Exported simulation data to: " + path);
    }

    /// <summary>
    /// Focuses the camera on the tardigrade center
    /// </summary>
    public void FocusCamera()
    {
        targetCameraPosition = transform.position;
        targetZoom = (minZoom + maxZoom) * 0.5f;
    }

    private void DisposeNativeArrays()
    {
        if (cellPositions.IsCreated) cellPositions.Dispose();
        if (cellVelocities.IsCreated) cellVelocities.Dispose();
        if (cellTypes.IsCreated) cellTypes.Dispose();
        if (cellEnergies.IsCreated) cellEnergies.Dispose();
        if (cellWaterContents.IsCreated) cellWaterContents.Dispose();
        if (cellStresses.IsCreated) cellStresses.Dispose();
        if (cellCryptobiosisStates.IsCreated) cellCryptobiosisStates.Dispose();
        if (cellTrehaloseLevels.IsCreated) cellTrehaloseLevels.Dispose();
        if (cellTransforms.isCreated) cellTransforms.Dispose();
    }
}

// ================ JOB IMPLEMENTATIONS ================

/// <summary>
/// Job to update cells based on environment conditions
/// </summary>
public struct EnvironmentUpdateJob : IJobParallelFor
{
    // Environment data
    [ReadOnly] public float DeltaTime;
    [ReadOnly] public float Temperature;
    [ReadOnly] public float Humidity;
    [ReadOnly] public float Radiation;
    [ReadOnly] public float Oxygen;
    [ReadOnly] public float Toxicity;

    // Cell data
    public NativeArray<float> CellEnergies;
    public NativeArray<float> CellWaterContents;
    public NativeArray<bool> CellCryptobiosisStates;

    public void Execute(int index)
    {
        // Skip cells in cryptobiosis
        if (CellCryptobiosisStates[index]) return;

        // Environment affects cell properties
        float energy = CellEnergies[index];
        float waterContent = CellWaterContents[index];

        // Temperature effect on metabolism
        if (Temperature > 30f)
        {
            // High temperature increases metabolism (energy usage)
            energy -= DeltaTime * 0.02f * ((Temperature - 30f) / 10f);
            // Also increases water loss
            waterContent -= DeltaTime * 0.01f * ((Temperature - 30f) / 10f);
        }
        else if (Temperature < 5f)
        {
            // Low temperature slows metabolism
            energy -= DeltaTime * 0.005f * ((5f - Temperature) / 5f);
        }
        else
        {
            // Normal temperature range - standard metabolism
            energy -= DeltaTime * 0.01f;
        }

        // Humidity effect on water content
        if (Humidity < 0.4f)
        {
            // Low humidity causes water loss
            waterContent -= DeltaTime * 0.02f * ((0.4f - Humidity) / 0.4f);
        }
        else if (Humidity > 0.9f)
        {
            // High humidity allows water absorption
            waterContent = math.min(1.0f, waterContent + DeltaTime * 0.01f);
        }

        // Radiation effect on energy
        if (Radiation > 10f)
        {
            // Radiation damages cells, requiring energy for repair
            energy -= DeltaTime * 0.01f * (Radiation / 100f);
        }

        // Oxygen effect on energy production
        if (Oxygen < 0.5f)
        {
            // Low oxygen reduces energy production
            energy -= DeltaTime * 0.015f * ((0.5f - Oxygen) / 0.5f);
        }

        // Toxicity effect
        if (Toxicity > 0.2f)
        {
            // Toxins require energy to process
            energy -= DeltaTime * 0.02f * (Toxicity / 0.8f);
        }

        // Energy restoration (food absorption) - simplified
        energy = math.min(1.0f, energy + DeltaTime * 0.005f);

        // Clamp values
        energy = math.clamp(energy, 0f, 1f);
        waterContent = math.clamp(waterContent, 0f, 1f);

        // Update cell data
        CellEnergies[index] = energy;
        CellWaterContents[index] = waterContent;
    }
}

/// <summary>
/// Job to calculate forces between cells
/// </summary>
public struct CalculateCellForcesJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> Positions;
    [ReadOnly] public NativeArray<int> CellTypes;
    public NativeArray<float3> Forces;

    public void Execute(int index)
    {
        float3 totalForce = float3.zero;
        float3 pos = Positions[index];
        int type = CellTypes[index];

        // Calculate forces from all other cells
        for (int i = 0; i < Positions.Length; i++)
        {
            if (i == index) continue;

            float3 otherPos = Positions[i];
            int otherType = CellTypes[i];

            float3 direction = pos - otherPos;
            float distanceSq = math.lengthsq(direction);

            // Skip if too far
            if (distanceSq > 0.1f) continue;

            float distance = math.sqrt(distanceSq);
            float3 normalizedDir = direction / distance;

            // Simplified forces based on cell types
            // In reality, this would be much more complex
            float forceMagnitude = 0;

            // Repulsive force to prevent overlap
            float repulsion = 0.01f / (distanceSq + 0.001f);

            // Attractive force based on cell type relationships
            float attraction = 0;

            // Examples of cell type attractions:
            // Epidermal cells stick together
            if (type == 0 && otherType == 0)
                attraction = 0.005f;

            // Muscle cells connect to nerves
            if ((type == 1 && otherType == 3) || (type == 3 && otherType == 1))
                attraction = 0.008f;

            // Digestive cells form a tube
            if (type == 2 && otherType == 2)
                attraction = 0.007f;

            // Nervous cells connect to each other
            if (type == 3 && otherType == 3)
                attraction = 0.006f;

            // Epidermal cells connect to muscle
            if ((type == 0 && otherType == 1) || (type == 1 && otherType == 0))
                attraction = 0.004f;

            // Calculate net force
            forceMagnitude = repulsion - attraction;
            totalForce += normalizedDir * forceMagnitude;
        }

        Forces[index] = totalForce;
    }
}

/// <summary>
/// Job to move cells based on calculated forces
/// </summary>
public struct MoveCellsJob : IJobParallelForTransform
{
    [ReadOnly] public float DeltaTime;
    public NativeArray<float3> Positions;
    public NativeArray<float3> Velocities;
    [ReadOnly] public NativeArray<float3> Forces;
    [ReadOnly] public NativeArray<bool> CellCryptobiosisStates;

    public void Execute(int index, TransformAccess transform)
    {
        // Get current position and velocity
        float3 position = Positions[index];
        float3 velocity = Velocities[index];

        // Apply force to velocity (F = ma, simplified with m=1)
        float3 acceleration = Forces[index];

        // Reduce movement if in cryptobiosis
        float movementFactor = CellCryptobiosisStates[index] ? 0.05f : 1.0f;

        // Update velocity with damping
        velocity = velocity * 0.9f + acceleration * DeltaTime * movementFactor;

        // Update position
        position = position + velocity * DeltaTime;

        // Update transform
        transform.position = position;

        // Store back to native arrays
        Positions[index] = position;
        Velocities[index] = velocity;
    }
}

/// <summary>
/// Job to handle cryptobiosis state transitions
/// </summary>
public struct CryptobiosisUpdateJob : IJobParallelFor
{
    [ReadOnly] public float DeltaTime;
    [ReadOnly] public float Temperature;
    [ReadOnly] public float Humidity;
    [ReadOnly] public float Radiation;
    [ReadOnly] public float Oxygen;
    [ReadOnly] public float Toxicity;
    [ReadOnly] public NativeArray<int> CellTypes;

    public NativeArray<float> CellEnergies;
    public NativeArray<float> CellWaterContents;
    public NativeArray<bool> CellCryptobiosisStates;

    public void Execute(int index)
    {
        // Determine if the cell should enter cryptobiosis
        bool shouldEnterCryptobiosis = false;
        int cryptobiosisType = 0;

        // Check extreme environmental conditions
        bool extremeDrought = Humidity < 0.05f;
        bool extremeCold = Temperature < -5f;
        bool highRadiation = Radiation > 1000f;
        bool lowOxygen = Oxygen < 0.1f;
        bool highToxicity = Toxicity > 0.8f;

        // Different cell types have different thresholds
        int cellType = CellTypes[index];

        // Determine cryptobiosis type based on environmental conditions
        if (extremeDrought)
        {
            shouldEnterCryptobiosis = true;
            cryptobiosisType = 1; // Anhydrobiosis
        }
        else if (extremeCold)
        {
            shouldEnterCryptobiosis = true;
            cryptobiosisType = 2; // Cryobiosis
        }
        else if (highToxicity)
        {
            shouldEnterCryptobiosis = true;
            cryptobiosisType = 3; // Chemobiosis
        }
        else if (lowOxygen)
        {
            shouldEnterCryptobiosis = true;
            cryptobiosisType = 4; // Anoxybiosis
        }

        // Get current state
        bool isInCryptobiosis = CellCryptobiosisStates[index];
        float energy = CellEnergies[index];
        float waterContent = CellWaterContents[index];

        // Handle cryptobiosis entry
        if (shouldEnterCryptobiosis && !isInCryptobiosis)
        {
            // Enter cryptobiosis state
            CellCryptobiosisStates[index] = true;

            // Apply appropriate changes based on cryptobiosis type
            switch (cryptobiosisType)
            {
                case 1: // Anhydrobiosis (desiccation)
                    waterContent = 0.05f;
                    energy = math.max(0.1f, energy * 0.2f);
                    break;

                case 2: // Cryobiosis (freezing)
                    waterContent = 0.8f;
                    energy = math.max(0.1f, energy * 0.1f);
                    break;

                case 3: // Chemobiosis (toxic chemicals)
                    waterContent = math.max(0.3f, waterContent * 0.5f);
                    energy = math.max(0.2f, energy * 0.3f);
                    break;

                case 4: // Anoxybiosis (oxygen deprivation)
                    waterContent = math.max(0.5f, waterContent * 0.7f);
                    energy = math.max(0.15f, energy * 0.25f);
                    break;
            }
        }
        // Handle cryptobiosis exit
        else if (!shouldEnterCryptobiosis && isInCryptobiosis)
        {
            // Exit cryptobiosis state
            CellCryptobiosisStates[index] = false;

            // Restore cell properties
            waterContent = math.min(1.0f, waterContent + 0.5f);
            energy = math.min(0.7f, energy + 0.2f);
        }
        // Maintain cryptobiosis state
        else if (isInCryptobiosis)
        {
            // Minimal energy consumption during cryptobiosis
            energy = math.max(0.05f, energy - DeltaTime * 0.001f);
        }

        // Update cell data
        CellEnergies[index] = energy;
        CellWaterContents[index] = waterContent;
    }
}

/// <summary>
/// Job to simulate trehalose production (sugar that helps tardigrades survive desiccation)
/// </summary>
public struct TrehaloseProductionJob : IJobParallelFor
{
    [ReadOnly] public float DeltaTime;
    [ReadOnly] public NativeArray<bool> CellCryptobiosisStates;
    public NativeArray<float> TrehaloseLevels;

    public void Execute(int index)
    {
        float currentLevel = TrehaloseLevels[index];
        bool isInCryptobiosis = CellCryptobiosisStates[index];

        // Trehalose production increases in cryptobiosis
        if (isInCryptobiosis)
        {
            // Ramp up production
            currentLevel = math.min(1f, currentLevel + DeltaTime * 0.1f);
        }
        else
        {
            // Gradually reduce levels when not in cryptobiosis
            currentLevel = math.max(0.1f, currentLevel - DeltaTime * 0.05f);
        }

        TrehaloseLevels[index] = currentLevel;
    }
}