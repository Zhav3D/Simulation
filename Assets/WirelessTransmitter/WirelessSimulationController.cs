using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// Main controller for the wireless simulation
public class WirelessSimulationController : MonoBehaviour
{
    // Reference to scene objects
    [Header("Scene References")]
    [Tooltip("The GameObject representing the wireless transmitter - this is the source of the signal")]
    public GameObject transmitterObject;

    [Tooltip("List of GameObjects representing wireless receivers that will detect and process signals")]
    public List<GameObject> receiverObjects = new List<GameObject>();

    [Tooltip("Parent transform for all environment objects (walls, furniture, etc.) that affect signal propagation")]
    public Transform environmentParent;

    [Tooltip("Prefab used to visualize the expanding electromagnetic waves emitted by the transmitter")]
    public GameObject waveVisualizerPrefab;

    [Tooltip("Prefab used to create a color-coded floor heatmap showing signal strength throughout the environment")]
    public GameObject signalHeatmapPrefab;

    [Tooltip("Prefab for visualizing signal reflections when waves bounce off surfaces")]
    public GameObject reflectionEffectPrefab;

    [Header("Transmitter Settings")]
    [Tooltip("Power output of the transmitter in milliwatts (mW). WiFi routers typically range from 30-100mW")]
    public float transmitPower = 100.0f; // in milliwatts

    [Tooltip("Center frequency of the wireless signal in MHz. 2400MHz (2.4GHz) is standard for WiFi")]
    public float transmitFrequency = 2400.0f; // in MHz (2.4 GHz default)

    [Tooltip("Width of the frequency channel in MHz. WiFi typically uses 20MHz or 40MHz channels")]
    public float bandwidth = 20.0f; // in MHz

    [Tooltip("Directional radiation pattern of the antenna. X-axis (0-1) represents angle from 0° to 180°, Y-axis represents gain")]
    public AnimationCurve transmissionPattern; // Directional pattern

    [Tooltip("Time in seconds between signal pulse emissions. Lower values increase visual density but impact performance")]
    public float pulseInterval = 0.5f; // Time between signal pulse emissions

    [Tooltip("Number of additional frequency components to simulate. Higher values produce more realistic frequency spectrum visuals")]
    public int signalHarmonics = 3; // Number of frequency harmonics to simulate

    [Header("Receiver Settings")]
    [Tooltip("Minimum signal strength in dBm required for the receiver to detect the signal. Typical WiFi devices range from -70dBm to -90dBm")]
    public float receiverSensitivity = -70.0f; // in dBm

    [Tooltip("Ratio of signal power to noise power in dB. Higher values mean cleaner signals and better data rates")]
    public float signalToNoiseRatio = 10.0f; // in dB

    [Tooltip("Directional sensitivity pattern of the receiver antenna. X-axis (0-1) represents angle from 0° to 180°, Y-axis represents sensitivity")]
    public AnimationCurve receiverPattern; // Directional pattern

    [Tooltip("Visual effect prefab that appears when a receiver successfully detects a signal")]
    public GameObject receiverEffectPrefab; // Effect when signal is received

    [Header("Environment Settings")]
    [Tooltip("Base signal attenuation factor for air. Higher values cause signal to weaken more quickly with distance")]
    public float airAttenuation = 0.1f; // Signal loss in air

    [Tooltip("Layer mask defining which objects in the scene count as obstacles for signal propagation")]
    public LayerMask obstaclesMask;

    [Tooltip("How effectively surfaces reflect signals (0-1). Higher values create stronger reflections and more complex multipath effects")]
    public float reflectionCoefficient = 0.3f; // How much signal is reflected (0-1)

    [Tooltip("How effectively signals bend around corners and obstacles (0-1). Higher values allow more signal to reach shadowed areas")]
    public float diffractionFactor = 0.2f; // How much signal bends around obstacles

    [Tooltip("Whether to simulate multiple signal paths (direct, reflected, diffracted). Enables more realistic propagation but is more computationally expensive")]
    public bool simulateMultipath = true; // Enable multipath propagation

    [Header("Visualization")]
    [Tooltip("Whether to show the expanding spherical waves representing electromagnetic signal propagation")]
    public bool showSignalPropagation = true;

    [Tooltip("Whether to display the color-coded floor heatmap showing signal strength throughout the environment")]
    public bool showSignalHeatmap = true;

    [Tooltip("Resolution of the spherical wave visualizations. Higher values create smoother spheres but impact performance")]
    public int waveResolution = 36;

    [Tooltip("Speed at which the visualization waves expand outward. This is a visual setting and doesn't affect actual propagation calculations")]
    public float waveSpeed = 3.0f;

    [Tooltip("Maximum distance that visualization waves will travel before fading out completely")]
    public float maxWaveDistance = 20.0f;

    [Tooltip("Spatial resolution of the signal heatmap in meters. Lower values create more detailed heatmaps but impact performance")]
    public float heatmapResolution = 0.5f; // Distance between heatmap points

    [Tooltip("Overall dimensions of the heatmap in world units (X,Y,Z). The heatmap will be centered on origin")]
    public Vector3 heatmapSize = new Vector3(20, 0, 20); // XZ plane size of heatmap

    [Tooltip("Base color of signal visualization waves and UI elements")]
    public Color signalColor = new Color(0.0f, 0.8f, 1.0f, 0.5f);

    [Tooltip("Color gradient used to visualize signal strength in the heatmap. Left side (0) is weakest signal, right side (1) is strongest")]
    public Gradient signalStrengthGradient; // Color gradient for signal strength visualization

    [Tooltip("Material used for the expanding spherical wave visualizations. Should support transparency")]
    public Material wavesMaterial;

    [Tooltip("Material used for the signal strength heatmap visualization. Should support transparency and vertex coloring")]
    public Material heatmapMaterial;

    // Internal variables
    private NativeArray<float3> environmentObstacles;
    private NativeArray<float> obstacleMaterials;
    private NativeArray<float3> visualizationPoints;
    private NativeArray<float> signalStrengths;
    private JobHandle signalPropagationJobHandle;
    private List<GameObject> waveVisualizers = new List<GameObject>();
    private GameObject signalHeatmap;
    private Dictionary<GameObject, float> receiverSignalStrengths = new Dictionary<GameObject, float>();
    private float timeSinceLastPulse = 0;
    private System.Random random = new System.Random();

    // MIMO configuration
    [Header("MIMO Configuration")]
    [Tooltip("Whether to simulate Multiple-Input Multiple-Output antenna configurations")]
    public bool enableMIMO = false;

    [Tooltip("Number of transmitting antennas in MIMO configuration")]
    [Range(1, 8)]
    public int mimoTxAntennas = 2;

    [Tooltip("Number of receiving antennas in MIMO configuration")]
    [Range(1, 8)]
    public int mimoRxAntennas = 2;

    [Tooltip("Spatial separation between MIMO antennas (in wavelengths)")]
    [Range(0.1f, 2.0f)]
    public float mimoAntennaSeparation = 0.5f;

    // Interference sources
    [Header("Interference Sources")]
    [Tooltip("Whether to simulate external interference sources")]
    public bool enableInterference = false;

    [Tooltip("List of interference source GameObjects that generate competing signals")]
    public List<GameObject> interferenceSources = new List<GameObject>();

    [Tooltip("Strength of background noise floor in dBm (typically -90 to -100 dBm)")]
    public float noiseFloor = -95.0f;

    [Tooltip("Random variation in the noise floor (in dB)")]
    public float noiseVariation = 5.0f;

    // Channel configuration
    [Header("Channel Configuration")]
    [Tooltip("Predefined wireless channels (e.g., WiFi channels 1-14 for 2.4GHz)")]
    public List<WirelessChannel> availableChannels = new List<WirelessChannel>();

    [Tooltip("Currently selected channel index")]
    public int currentChannelIndex = 0;

    // Simulation control
    private bool simulationPaused = false;
    private float simulationSpeed = 1.0f;

    // Signal recording
    private const int MaxRecordedPoints = 100;
    private List<float> recordedSignalStrengths = new List<float>();
    private List<float> recordedThroughputs = new List<float>();
    private float recordingInterval = 0.5f;
    private float timeSinceLastRecording = 0;

    // Throughput calculation
    private float calculatedThroughput = 0; // in Mbps

    // UI elements
    [Header("UI References")]
    [Tooltip("Slider to control the transmitter power output")]
    public Slider powerSlider;

    [Tooltip("Slider to control the transmitter frequency")]
    public Slider frequencySlider;

    [Tooltip("Dropdown for selecting specific WiFi/Bluetooth channels")]
    public Dropdown channelDropdown;

    [Tooltip("Display showing current signal strength at the primary receiver")]
    public Text signalStrengthText;

    [Tooltip("Display showing estimated data throughput based on signal quality")]
    public Text throughputText;

    [Tooltip("Toggle to show/hide wave propagation visualization")]
    public Toggle showPropagationToggle;

    [Tooltip("Toggle to show/hide signal strength heatmap")]
    public Toggle showHeatmapToggle;

    [Tooltip("Toggle to enable/disable interference sources")]
    public Toggle enableInterferenceToggle;

    [Tooltip("Dropdown to select simulation environment presets")]
    public Dropdown environmentPresetDropdown;

    [Tooltip("Button to pause/resume the simulation")]
    public Button pausePlayButton;

    [Tooltip("UI panel holding the MIMO configuration controls")]
    public GameObject mimoConfigPanel;

    [Tooltip("Graph showing signal strength over time")]
    public RectTransform signalGraph;

    void Start()
    {
        InitializeWirelessChannels();
        InitializeEnvironment();
        InitializeReceivers();
        InitializeVisualization();
        InitializeMIMO();
        InitializeInterferenceSources();
        SetupUI();
    }

    // Wireless Channel Definition
    [System.Serializable]
    public class WirelessChannel
    {
        [Tooltip("Channel number according to standard (e.g., WiFi channels 1-14)")]
        public int channelNumber;

        [Tooltip("Center frequency of the channel in MHz")]
        public float frequency;

        [Tooltip("Channel bandwidth in MHz")]
        public float bandwidth;

        [Tooltip("Frequency band this channel belongs to (e.g., '2.4 GHz', '5 GHz')")]
        public string band;
    }

    // Interference Source Component
    public class InterferenceSource : MonoBehaviour
    {
        [Tooltip("Power output of the interference source in milliwatts")]
        public float power = 30.0f;

        [Tooltip("Frequency of the interference source in MHz")]
        public float frequency = 2450.0f;

        [Tooltip("How often the interference source emits a pulse")]
        public float pulseInterval = 0.5f;

        [Tooltip("Whether this interference source pulses or is continuous")]
        public bool isPulsed = true;

        [Tooltip("Effect of interference source on different frequencies (relative impact vs. frequency offset)")]
        public AnimationCurve frequencyResponse = AnimationCurve.EaseInOut(0, 1, 100, 0);

        private float timeSinceLastPulse = 0;
        private bool isActive = false;

        void Update()
        {
            if (isPulsed)
            {
                timeSinceLastPulse += Time.deltaTime;

                if (timeSinceLastPulse >= pulseInterval)
                {
                    // Toggle active state
                    isActive = !isActive;

                    // Reset timer if turning on
                    if (isActive)
                    {
                        timeSinceLastPulse = 0;
                    }
                    // Or turn off after a short duration
                    else
                    {
                        timeSinceLastPulse = pulseInterval * 0.7f; // Shorter off-time than on-time
                    }

                    // Visual feedback for pulse
                    UpdateVisuals();
                }
            }
            else
            {
                // Always active for continuous sources
                isActive = true;
            }
        }

        void UpdateVisuals()
        {
            // Update visual representation based on active state
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // Set alpha based on active state
                Color color = renderer.material.color;
                color.a = isActive ? 0.8f : 0.2f;
                renderer.material.color = color;

                // Optionally, add emission for active state
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    if (isActive)
                    {
                        renderer.material.EnableKeyword("_EMISSION");
                        renderer.material.SetColor("_EmissionColor", new Color(1.0f, 0.5f, 0.0f, 1.0f) * 0.8f);
                    }
                    else
                    {
                        renderer.material.DisableKeyword("_EMISSION");
                    }
                }
            }
        }

        // Calculate interference impact on a specific frequency
        public float GetInterferenceImpact(float targetFrequency)
        {
            if (!isActive)
                return 0;

            // Calculate frequency offset (absolute value)
            float frequencyOffset = Mathf.Abs(targetFrequency - frequency);

            // Use frequency response curve to determine impact
            return frequencyResponse.Evaluate(frequencyOffset);
        }
    }

    void InitializeWirelessChannels()
    {
        // Define standard WiFi channels if none are set
        if (availableChannels.Count == 0)
        {
            // 2.4 GHz channels
            for (int i = 1; i <= 14; i++)
            {
                float frequency = 2412 + ((i - 1) * 5); // in MHz
                if (i > 13) frequency = 2484; // Channel 14 is special

                WirelessChannel channel = new WirelessChannel
                {
                    channelNumber = i,
                    frequency = frequency,
                    bandwidth = 20, // Standard 20 MHz
                    band = "2.4 GHz"
                };

                availableChannels.Add(channel);
            }

            // 5 GHz channels (simplified version - real 5 GHz has many more channels)
            int[] channels5GHz = new int[] { 36, 40, 44, 48, 52, 56, 60, 64, 100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 144 };
            for (int i = 0; i < channels5GHz.Length; i++)
            {
                int channelNum = channels5GHz[i];
                float frequency = 5180 + ((channelNum - 36) * 5); // in MHz

                WirelessChannel channel = new WirelessChannel
                {
                    channelNumber = channelNum,
                    frequency = frequency,
                    bandwidth = 20, // Can be 20, 40, 80, or 160 MHz
                    band = "5 GHz"
                };

                availableChannels.Add(channel);
            }

            // Set default channel
            if (availableChannels.Count > 0)
            {
                currentChannelIndex = 5; // Channel 6 in 2.4GHz is common default
                transmitFrequency = availableChannels[currentChannelIndex].frequency;
                bandwidth = availableChannels[currentChannelIndex].bandwidth;
            }
        }
    }

    void InitializeMIMO()
    {
        if (!enableMIMO)
            return;

        // Create visual representation of MIMO antennas if transmitter exists
        if (transmitterObject != null)
        {
            // Clear any existing antenna visualizations
            Transform antennaParent = transmitterObject.transform.Find("MIMOAntennas");
            if (antennaParent != null)
                Destroy(antennaParent.gameObject);

            // Create parent for antennas
            GameObject antennaParentObj = new GameObject("MIMOAntennas");
            antennaParentObj.transform.parent = transmitterObject.transform;
            antennaParentObj.transform.localPosition = Vector3.zero;

            // Create transmit antennas
            for (int i = 0; i < mimoTxAntennas; i++)
            {
                // Calculate position - arrange in a line
                float offset = ((float)i - (mimoTxAntennas - 1) / 2.0f) * mimoAntennaSeparation * 0.2f;

                GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                antenna.name = $"TxAntenna_{i}";
                antenna.transform.parent = antennaParentObj.transform;
                antenna.transform.localPosition = new Vector3(offset, 0.3f, 0);
                antenna.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);

                // Add material
                Renderer renderer = antenna.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.2f, 0.8f, 1.0f);
                }
            }
        }

        // Create visual representation of MIMO antennas for each receiver
        foreach (var receiver in receiverObjects)
        {
            if (receiver == null)
                continue;

            // Clear any existing antenna visualizations
            Transform antennaParent = receiver.transform.Find("MIMOAntennas");
            if (antennaParent != null)
                Destroy(antennaParent.gameObject);

            // Create parent for antennas
            GameObject antennaParentObj = new GameObject("MIMOAntennas");
            antennaParentObj.transform.parent = receiver.transform;
            antennaParentObj.transform.localPosition = Vector3.zero;

            // Create receive antennas
            for (int i = 0; i < mimoRxAntennas; i++)
            {
                // Calculate position - arrange in a line
                float offset = ((float)i - (mimoRxAntennas - 1) / 2.0f) * mimoAntennaSeparation * 0.2f;

                GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                antenna.name = $"RxAntenna_{i}";
                antenna.transform.parent = antennaParentObj.transform;
                antenna.transform.localPosition = new Vector3(offset, 0.2f, 0);
                antenna.transform.localScale = new Vector3(0.04f, 0.2f, 0.04f);

                // Add material
                Renderer renderer = antenna.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.8f, 0.2f, 1.0f);
                }
            }
        }
    }

    void InitializeInterferenceSources()
    {
        // If interference is enabled but no sources defined, create some example sources
        if (enableInterference && interferenceSources.Count == 0)
        {
            // Create a few interference sources at different locations
            for (int i = 0; i < 3; i++)
            {
                GameObject interferenceObj = new GameObject($"InterferenceSource_{i}");
                interferenceObj.transform.position = new Vector3(
                    Random.Range(-10f, 10f),
                    1.0f,
                    Random.Range(-10f, 10f)
                );

                // Add visual representation
                GameObject visualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visualSphere.transform.parent = interferenceObj.transform;
                visualSphere.transform.localPosition = Vector3.zero;
                visualSphere.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                // Add material
                Renderer renderer = visualSphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(1.0f, 0.5f, 0.0f, 0.8f); // Orange
                }

                // Add interference component
                InterferenceSource source = interferenceObj.AddComponent<InterferenceSource>();
                source.power = Random.Range(10f, 50f); // in milliwatts
                source.frequency = Random.Range(2400f, 2480f); // in MHz
                source.pulseInterval = Random.Range(0.2f, 1.0f);

                interferenceSources.Add(interferenceObj);
            }
        }

        // Enable/disable based on interference setting
        foreach (var source in interferenceSources)
        {
            if (source != null)
                source.SetActive(enableInterference);
        }
    }

    void InitializeEnvironment()
    {
        // Scan the environment for obstacles
        Collider[] obstacles = Physics.OverlapBox(
            Vector3.zero,
            new Vector3(50, 50, 50),
            Quaternion.identity,
            obstaclesMask
        );

        environmentObstacles = new NativeArray<float3>(obstacles.Length, Allocator.Persistent);
        obstacleMaterials = new NativeArray<float>(obstacles.Length, Allocator.Persistent);

        for (int i = 0; i < obstacles.Length; i++)
        {
            environmentObstacles[i] = obstacles[i].transform.position;

            // Determine material properties based on tags
            float attenuationFactor = 0.5f; // Default

            if (obstacles[i].CompareTag("Metal"))
                attenuationFactor = 0.9f;
            else if (obstacles[i].CompareTag("Glass"))
                attenuationFactor = 0.3f;
            else if (obstacles[i].CompareTag("Concrete"))
                attenuationFactor = 0.7f;
            else if (obstacles[i].CompareTag("Wood"))
                attenuationFactor = 0.4f;

            obstacleMaterials[i] = attenuationFactor;
        }
    }

    void InitializeReceivers()
    {
        // If no receivers were set in the inspector, find them in the scene
        if (receiverObjects.Count == 0)
        {
            ReceiverComponent[] receivers = FindObjectsOfType<ReceiverComponent>();
            foreach (var receiver in receivers)
            {
                receiverObjects.Add(receiver.gameObject);
                receiverSignalStrengths[receiver.gameObject] = float.MinValue;
            }
        }
        else
        {
            // Initialize signal strengths for assigned receivers
            foreach (var receiver in receiverObjects)
            {
                // Add ReceiverComponent if it doesn't exist
                if (receiver.GetComponent<ReceiverComponent>() == null)
                {
                    receiver.AddComponent<ReceiverComponent>();
                }

                receiverSignalStrengths[receiver] = float.MinValue;
            }
        }
    }

    void InitializeVisualization()
    {
        if (showSignalPropagation)
        {
            visualizationPoints = new NativeArray<float3>(waveResolution * waveResolution, Allocator.Persistent);
            signalStrengths = new NativeArray<float>(waveResolution * waveResolution, Allocator.Persistent);

            // Create sphere points for visualization
            int index = 0;
            for (int i = 0; i < waveResolution; i++)
            {
                float phi = i * 2 * Mathf.PI / waveResolution;

                for (int j = 0; j < waveResolution; j++)
                {
                    float theta = j * Mathf.PI / waveResolution;

                    float x = Mathf.Sin(theta) * Mathf.Cos(phi);
                    float y = Mathf.Sin(theta) * Mathf.Sin(phi);
                    float z = Mathf.Cos(theta);

                    visualizationPoints[index] = new float3(x, y, z);
                    index++;
                }
            }
        }

        if (showSignalHeatmap)
        {
            CreateSignalHeatmap();
        }
    }

    void CreateSignalHeatmap()
    {
        // Create a signal strength heatmap visualization
        if (signalHeatmap != null)
        {
            Destroy(signalHeatmap);
        }

        signalHeatmap = new GameObject("SignalHeatmap");
        signalHeatmap.transform.parent = transform;

        // Create mesh for heatmap
        MeshFilter meshFilter = signalHeatmap.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = signalHeatmap.AddComponent<MeshRenderer>();

        int xCount = Mathf.CeilToInt(heatmapSize.x / heatmapResolution);
        int zCount = Mathf.CeilToInt(heatmapSize.z / heatmapResolution);

        Vector3[] vertices = new Vector3[(xCount + 1) * (zCount + 1)];
        Color[] colors = new Color[(xCount + 1) * (zCount + 1)];
        int[] triangles = new int[xCount * zCount * 6];

        // Create vertices grid
        for (int z = 0; z <= zCount; z++)
        {
            for (int x = 0; x <= xCount; x++)
            {
                float xPos = (x * heatmapResolution) - (heatmapSize.x / 2);
                float zPos = (z * heatmapResolution) - (heatmapSize.z / 2);
                vertices[z * (xCount + 1) + x] = new Vector3(xPos, 0.1f, zPos); // Slightly above ground
                colors[z * (xCount + 1) + x] = Color.clear; // Start transparent
            }
        }

        // Create triangles
        int triangleIndex = 0;
        for (int z = 0; z < zCount; z++)
        {
            for (int x = 0; x < xCount; x++)
            {
                int vertexIndex = z * (xCount + 1) + x;

                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + (xCount + 1);
                triangles[triangleIndex + 2] = vertexIndex + 1;

                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + (xCount + 1);
                triangles[triangleIndex + 5] = vertexIndex + (xCount + 1) + 1;

                triangleIndex += 6;
            }
        }

        // Create and assign mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;

        meshFilter.mesh = mesh;
        meshRenderer.material = heatmapMaterial;
        meshRenderer.material.SetColor("_Color", signalColor);
        meshRenderer.material.SetFloat("_Metallic", 0);
        meshRenderer.material.SetFloat("_Glossiness", 0);

        // Set rendering mode to transparent
        meshRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        meshRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        meshRenderer.material.SetInt("_ZWrite", 0);
        meshRenderer.material.DisableKeyword("_ALPHATEST_ON");
        meshRenderer.material.EnableKeyword("_ALPHABLEND_ON");
        meshRenderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        meshRenderer.material.renderQueue = 3000;

        signalHeatmap.SetActive(showSignalHeatmap);
    }

    void UpdateHeatmap()
    {
        if (!showSignalHeatmap || signalHeatmap == null)
            return;

        MeshFilter meshFilter = signalHeatmap.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
            return;

        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;
        Color[] colors = new Color[vertices.Length];

        // Calculate signal strength at each vertex
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = signalHeatmap.transform.TransformPoint(vertices[i]);
            float signalStrengthDBm = CalculateSignalStrengthAt(worldPos);

            // Map signal strength to color based on receiver sensitivity
            float normalizedStrength = Mathf.InverseLerp(-100f, -30f, signalStrengthDBm);
            Color vertexColor = signalStrengthGradient.Evaluate(normalizedStrength);

            // Only show color where signal is detectable
            if (signalStrengthDBm < receiverSensitivity)
            {
                vertexColor.a = 0f; // Transparent where no signal
            }
            else
            {
                vertexColor.a = Mathf.Lerp(0.1f, 0.5f, normalizedStrength); // Semi-transparent based on strength
            }

            colors[i] = vertexColor;
        }

        // Update the mesh colors
        mesh.colors = colors;
    }

    public float CalculateSignalStrengthAt(Vector3 position)
    {
        // Calculate direct path signal strength
        float distance = Vector3.Distance(transmitterObject.transform.position, position);

        // Free space path loss formula: FSPL(dB) = 20*log10(d) + 20*log10(f) + 32.44
        // where d is distance in km and f is frequency in MHz
        float distanceKm = distance / 1000.0f;
        float pathLoss = 20 * Mathf.Log10(distanceKm) + 20 * Mathf.Log10(transmitFrequency) + 32.44f;

        // Convert transmit power from mW to dBm: dBm = 10*log10(mW)
        float transmitPowerDBm = 10 * Mathf.Log10(transmitPower);

        // Calculate received signal strength
        float receivedPowerDBm = transmitPowerDBm - pathLoss;

        // Apply directional antenna pattern
        float transmitterAngleFactor = EvaluateAntennaPattern(transmitterObject, position, transmissionPattern);
        receivedPowerDBm += transmitterAngleFactor;

        // Check for obstacles in path
        RaycastHit hit;
        if (Physics.Linecast(transmitterObject.transform.position, position, out hit, obstaclesMask))
        {
            // Apply material-specific attenuation
            if (hit.collider.CompareTag("Metal"))
                receivedPowerDBm -= 25.0f;
            else if (hit.collider.CompareTag("Glass"))
                receivedPowerDBm -= 6.0f;
            else if (hit.collider.CompareTag("Concrete"))
                receivedPowerDBm -= 15.0f;
            else if (hit.collider.CompareTag("Wood"))
                receivedPowerDBm -= 8.0f;
            else
                receivedPowerDBm -= 10.0f; // Default attenuation

            // Add some signal via diffraction around obstacles
            if (diffractionFactor > 0)
            {
                float diffractionContribution = transmitPowerDBm - pathLoss - 20.0f; // Extra 20dB loss for diffraction
                diffractionContribution *= diffractionFactor;

                // Combine direct path and diffraction using power addition (not simple dB addition)
                float directPower = Mathf.Pow(10, receivedPowerDBm / 10);
                float diffractionPower = Mathf.Pow(10, diffractionContribution / 10);
                float totalPower = directPower + diffractionPower;

                receivedPowerDBm = 10 * Mathf.Log10(totalPower);
            }
        }

        // Add multipath effects if enabled
        if (simulateMultipath)
        {
            // Create several reflection paths
            for (int i = 0; i < 3; i++) // Simulate 3 reflection paths
            {
                // Generate a random reflection point
                Vector3 reflectionPoint = new Vector3(
                    Random.Range(-10f, 10f),
                    Random.Range(0f, 5f),
                    Random.Range(-10f, 10f)
                );

                // Calculate the reflected path lengths
                float distanceToReflection = Vector3.Distance(transmitterObject.transform.position, reflectionPoint);
                float distanceFromReflection = Vector3.Distance(reflectionPoint, position);
                float totalReflectionDistance = distanceToReflection + distanceFromReflection;

                // Calculate path loss for reflection path
                float reflectionDistanceKm = totalReflectionDistance / 1000.0f;
                float reflectionPathLoss = 20 * Mathf.Log10(reflectionDistanceKm) + 20 * Mathf.Log10(transmitFrequency) + 32.44f;

                // Add reflection loss
                reflectionPathLoss += 6.0f; // ~6dB additional loss for a reflection

                // Calculate received power from this reflection
                float reflectionPowerDBm = transmitPowerDBm - reflectionPathLoss;

                // Scale by reflection coefficient
                reflectionPowerDBm += 10 * Mathf.Log10(reflectionCoefficient);

                // Combine with direct power using power addition
                float directPower = Mathf.Pow(10, receivedPowerDBm / 10);
                float reflectionPower = Mathf.Pow(10, reflectionPowerDBm / 10);

                // Phase interference (simplified)
                float pathDifference = totalReflectionDistance - distance;
                float wavelength = 299792458f / (transmitFrequency * 1000000f); // speed of light / frequency in Hz
                float phaseShift = (pathDifference % wavelength) / wavelength; // 0 to 1

                // Convert to radians and calculate interference factor
                float phaseAngle = phaseShift * 2 * Mathf.PI;
                float interferenceFactor = Mathf.Cos(phaseAngle); // -1 to 1

                // Scale reflection power by interference factor
                if (interferenceFactor > 0)
                {
                    // Constructive interference
                    reflectionPower *= (1 + interferenceFactor);
                }
                else
                {
                    // Destructive interference
                    reflectionPower *= (1 + interferenceFactor * 0.5f); // Reduce destructive effect
                }

                float totalPower = directPower + reflectionPower;
                receivedPowerDBm = 10 * Mathf.Log10(totalPower);
            }
        }

        // Apply environmental noise floor
        float noiseFloor = -90.0f + Random.Range(-5f, 5f); // Typical noise floor around -90dBm with variation

        // Combine signal and noise using power addition
        float signalPower = Mathf.Pow(10, receivedPowerDBm / 10);
        float noisePower = Mathf.Pow(10, noiseFloor / 10);
        float totalSignalPower = signalPower + noisePower;

        return 10 * Mathf.Log10(totalSignalPower);
    }

    void SetupUI()
    {
        if (powerSlider != null)
            powerSlider.onValueChanged.AddListener(OnPowerChanged);

        if (frequencySlider != null)
            frequencySlider.onValueChanged.AddListener(OnFrequencyChanged);

        if (channelDropdown != null)
            channelDropdown.onValueChanged.AddListener(OnChannelChanged);

        if (showPropagationToggle != null)
            showPropagationToggle.onValueChanged.AddListener(OnTogglePropagation);

        if (showHeatmapToggle != null)
            showHeatmapToggle.onValueChanged.AddListener(OnToggleHeatmap);

        if (enableInterferenceToggle != null)
            enableInterferenceToggle.onValueChanged.AddListener(OnToggleInterference);

        if (environmentPresetDropdown != null)
            environmentPresetDropdown.onValueChanged.AddListener(OnEnvironmentPresetChanged);

        if (pausePlayButton != null)
            pausePlayButton.onClick.AddListener(OnPausePlayClicked);

        // Initialize channel dropdown
        InitializeChannelDropdown();

        // Initialize environment presets
        InitializeEnvironmentPresets();
    }

    void InitializeChannelDropdown()
    {
        if (channelDropdown == null || availableChannels.Count == 0)
            return;

        // Clear existing options
        channelDropdown.ClearOptions();

        // Create channel options
        List<string> options = new List<string>();
        foreach (var channel in availableChannels)
        {
            options.Add($"Ch {channel.channelNumber} ({channel.frequency} MHz)");
        }

        // Add options to dropdown
        channelDropdown.AddOptions(options);

        // Set current value
        channelDropdown.value = currentChannelIndex;
    }

    void InitializeEnvironmentPresets()
    {
        if (environmentPresetDropdown == null)
            return;

        // Clear existing options
        environmentPresetDropdown.ClearOptions();

        // Add environment options
        List<string> options = new List<string>()
        {
            "Open Space",
            "Home/Apartment",
            "Office",
            "Conference Room",
            "Industrial",
            "Urban Outdoor"
        };

        // Add options to dropdown
        environmentPresetDropdown.AddOptions(options);

        // Set default value
        environmentPresetDropdown.value = 0;
    }

    public void OnChannelChanged(int index)
    {
        if (index < 0 || index >= availableChannels.Count)
            return;

        currentChannelIndex = index;
        WirelessChannel channel = availableChannels[index];

        // Update frequency based on channel
        transmitFrequency = channel.frequency;
        bandwidth = channel.bandwidth;

        // Update frequency slider if it exists
        if (frequencySlider != null)
            frequencySlider.value = transmitFrequency;
    }

    public void OnToggleInterference(bool value)
    {
        enableInterference = value;

        // Enable/disable interference source objects
        foreach (var source in interferenceSources)
        {
            if (source != null)
                source.SetActive(value);
        }
    }

    public void OnEnvironmentPresetChanged(int presetIndex)
    {
        // Apply environment preset
        switch (presetIndex)
        {
            case 0: // Open Space
                airAttenuation = 0.05f;
                reflectionCoefficient = 0.1f;
                diffractionFactor = 0.05f;
                noiseFloor = -100.0f;
                break;

            case 1: // Home/Apartment
                airAttenuation = 0.1f;
                reflectionCoefficient = 0.3f;
                diffractionFactor = 0.15f;
                noiseFloor = -95.0f;
                break;

            case 2: // Office
                airAttenuation = 0.15f;
                reflectionCoefficient = 0.4f;
                diffractionFactor = 0.2f;
                noiseFloor = -90.0f;
                break;

            case 3: // Conference Room
                airAttenuation = 0.2f;
                reflectionCoefficient = 0.5f;
                diffractionFactor = 0.25f;
                noiseFloor = -92.0f;
                break;

            case 4: // Industrial
                airAttenuation = 0.3f;
                reflectionCoefficient = 0.6f;
                diffractionFactor = 0.3f;
                noiseFloor = -85.0f;
                break;

            case 5: // Urban Outdoor
                airAttenuation = 0.25f;
                reflectionCoefficient = 0.2f;
                diffractionFactor = 0.1f;
                noiseFloor = -90.0f;
                break;
        }
    }

    public void OnPausePlayClicked()
    {
        simulationPaused = !simulationPaused;

        // Update button text
        Text buttonText = pausePlayButton.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = simulationPaused ? "Play" : "Pause";
        }
    }

    public void OnPowerChanged(float value)
    {
        transmitPower = value;
    }

    public void OnFrequencyChanged(float value)
    {
        transmitFrequency = value;
    }

    public void OnTogglePropagation(bool value)
    {
        showSignalPropagation = value;

        foreach (var visualizer in waveVisualizers)
        {
            if (visualizer != null)
                visualizer.SetActive(value);
        }
    }

    public void OnToggleHeatmap(bool value)
    {
        showSignalHeatmap = value;

        if (signalHeatmap != null)
            signalHeatmap.SetActive(value);
        else if (value)
            CreateSignalHeatmap();
    }

    void Update()
    {
        // Skip most processing if simulation is paused
        if (simulationPaused)
        {
            // Still allow user interaction when paused
            HandleUserInteraction();
            return;
        }

        float deltaTime = Time.deltaTime * simulationSpeed;

        // Create signal pulses at regular intervals
        timeSinceLastPulse += deltaTime;
        if (timeSinceLastPulse >= pulseInterval)
        {
            EmitSignalPulse();
            timeSinceLastPulse = 0;
        }

        // Calculate signal propagation using Jobs
        CalculateSignalPropagation();

        // Process interference if enabled
        if (enableInterference)
        {
            ProcessInterference();
        }

        // Update signal heatmap
        if (showSignalHeatmap && Time.frameCount % 10 == 0) // Update every 10 frames for performance
        {
            UpdateHeatmap();
        }

        // Update all receivers
        UpdateReceivers();

        // Calculate throughput based on signal quality
        CalculateThroughput();

        // Record signal data for graphing
        timeSinceLastRecording += deltaTime;
        if (timeSinceLastRecording >= recordingInterval)
        {
            RecordSignalData();
            timeSinceLastRecording = 0;
        }

        // Update UI with signal information
        UpdateSignalInfo();

        // Handle user interaction
        HandleUserInteraction();

        // Clean up old waves
        CleanUpOldWaves();
    }

    // Process interference from other sources
    void ProcessInterference()
    {
        foreach (var interferenceSource in interferenceSources)
        {
            if (interferenceSource == null)
                continue;

            // Calculate interference effect on each receiver
            foreach (var receiver in receiverObjects)
            {
                if (receiver == null)
                    continue;

                // Calculate distance from interference source to receiver
                float distance = Vector3.Distance(interferenceSource.transform.position, receiver.transform.position);

                // Skip if too far away to matter
                if (distance > maxWaveDistance * 1.5f)
                    continue;

                // Get the interference component if it exists
                InterferenceSource interference = interferenceSource.GetComponent<InterferenceSource>();
                if (interference == null)
                    continue;

                // Calculate interference power at receiver
                float interferenceDistanceKm = distance / 1000.0f;
                float pathLoss = 20 * Mathf.Log10(interferenceDistanceKm) + 20 * Mathf.Log10(interference.frequency) + 32.44f;
                float interferencePower = 10 * Mathf.Log10(interference.power) - pathLoss;

                // Apply to receiver's signal-to-noise ratio (simplified)
                float currentSignal = receiverSignalStrengths[receiver];

                // Convert both to linear power
                float signalPower = Mathf.Pow(10, currentSignal / 10);
                float interferePower = Mathf.Pow(10, interferencePower / 10);

                // Calculate new signal with interference (SINR - Signal to Interference plus Noise Ratio)
                float noisePower = Mathf.Pow(10, (noiseFloor + Random.Range(-noiseVariation, noiseVariation)) / 10);
                float totalInterferencePower = interferePower + noisePower;

                // If interference is strong enough to affect signal
                if (totalInterferencePower > noisePower * 2)
                {
                    // Calculate new effective signal strength with interference
                    float sinr = 10 * Mathf.Log10(signalPower / totalInterferencePower);

                    // Apply a degradation to the stored signal strength based on interference
                    // This is simplified - in real systems the impact would depend on modulation scheme
                    float degradedSignal = currentSignal - Mathf.Max(0, (8 - sinr)); // Approximate degradation

                    // Update the receiver's effective signal strength
                    receiverSignalStrengths[receiver] = degradedSignal;

                    // Visual feedback for interference
                    ReceiverComponent receiverComp = receiver.GetComponent<ReceiverComponent>();
                    if (receiverComp != null)
                    {
                        receiverComp.OnInterference(interferencePower);
                    }
                }
            }
        }
    }

    // Calculate throughput based on signal quality
    void CalculateThroughput()
    {
        if (receiverObjects.Count == 0 || receiverObjects[0] == null)
            return;

        // Get signal strength for primary receiver
        float signalStrength = receiverSignalStrengths[receiverObjects[0]];

        // Below sensitivity threshold, no connection
        if (signalStrength < receiverSensitivity)
        {
            calculatedThroughput = 0;
            return;
        }

        // Calculate signal-to-noise ratio
        float noiseLevel = noiseFloor + Random.Range(-noiseVariation, noiseVariation);
        float snr = signalStrength - noiseLevel;

        // Shannon-Hartley theorem: C = B * log2(1 + S/N)
        // B is bandwidth in Hz, S/N is linear signal-to-noise ratio
        float linearSNR = Mathf.Pow(10, snr / 10);
        float maxChannelCapacity = (bandwidth * 1000000) * Mathf.Log(1 + linearSNR, 2) / 1000000; // in Mbps

        // Real-world efficiency is lower than theoretical maximum
        float efficiencyFactor = 0.6f; // Most systems achieve 60-70% of theoretical

        // MIMO multiplier if enabled
        float mimoMultiplier = 1.0f;
        if (enableMIMO)
        {
            // Simplified MIMO capacity estimate - in reality this depends on channel conditions
            mimoMultiplier = Mathf.Min(mimoTxAntennas, mimoRxAntennas);

            // Real MIMO systems don't achieve perfect linear scaling
            mimoMultiplier = 1 + (mimoMultiplier - 1) * 0.8f;
        }

        // Calculate real-world throughput
        calculatedThroughput = maxChannelCapacity * efficiencyFactor * mimoMultiplier;

        // Apply some realistic upper bounds based on protocol
        if (transmitFrequency >= 2400 && transmitFrequency <= 2500) // 2.4 GHz band
        {
            calculatedThroughput = Mathf.Min(calculatedThroughput, 600); // 802.11ax max
        }
        else if (transmitFrequency >= 5000 && transmitFrequency <= 6000) // 5 GHz band
        {
            calculatedThroughput = Mathf.Min(calculatedThroughput, 1200); // 802.11ax max
        }
    }

    // Record signal data for time-based analysis
    void RecordSignalData()
    {
        if (receiverObjects.Count == 0 || receiverObjects[0] == null)
            return;

        // Record signal strength
        recordedSignalStrengths.Add(receiverSignalStrengths[receiverObjects[0]]);
        if (recordedSignalStrengths.Count > MaxRecordedPoints)
        {
            recordedSignalStrengths.RemoveAt(0);
        }

        // Record throughput
        recordedThroughputs.Add(calculatedThroughput);
        if (recordedThroughputs.Count > MaxRecordedPoints)
        {
            recordedThroughputs.RemoveAt(0);
        }

        // Update graph if available
        if (signalGraph != null)
        {
            UpdateSignalGraph();
        }
    }

    // Update the signal history graph
    void UpdateSignalGraph()
    {
        // Implementation would depend on your specific graphing system
        // This is a placeholder for the actual implementation

        // Example using a LineRenderer:
        LineRenderer graphRenderer = signalGraph.GetComponent<LineRenderer>();
        if (graphRenderer != null)
        {
            graphRenderer.positionCount = recordedSignalStrengths.Count;

            for (int i = 0; i < recordedSignalStrengths.Count; i++)
            {
                // Map signal strength (-100 to -30 dBm) to y position (0 to 1)
                float normalizedSignal = Mathf.InverseLerp(-100, -30, recordedSignalStrengths[i]);

                // Map time point to x position (0 to 1)
                float normalizedX = (float)i / MaxRecordedPoints;

                // Set point position
                graphRenderer.SetPosition(i, new Vector3(normalizedX, normalizedSignal, 0));
            }
        }
    }

    void EmitSignalPulse()
    {
        if (showSignalPropagation)
        {
            // Create base wave
            GameObject wave = Instantiate(waveVisualizerPrefab, transmitterObject.transform.position, Quaternion.identity);
            wave.GetComponent<Renderer>().material = new Material(wavesMaterial);
            wave.GetComponent<Renderer>().material.SetColor("_Color", signalColor);

            // Set up wave propagation component
            WavePropagation propagation = wave.AddComponent<WavePropagation>();
            propagation.speed = waveSpeed;
            propagation.maxDistance = maxWaveDistance;
            propagation.signalPower = transmitPower;
            propagation.frequency = transmitFrequency;
            propagation.transmitter = transmitterObject;
            propagation.receivers = receiverObjects;
            propagation.wirelessController = this;
            propagation.reflectionPrefab = reflectionEffectPrefab;
            propagation.reflectionCoefficient = reflectionCoefficient;

            waveVisualizers.Add(wave);

            // Create harmonics waves (at different frequencies)
            if (signalHarmonics > 1)
            {
                for (int i = 1; i < signalHarmonics; i++)
                {
                    // Create harmonic wave with slight delay and different frequency
                    StartCoroutine(CreateHarmonicWave(i * 0.05f, transmitFrequency + i * 5f));
                }
            }
        }
    }

    IEnumerator CreateHarmonicWave(float delay, float frequency)
    {
        yield return new WaitForSeconds(delay);

        GameObject wave = Instantiate(waveVisualizerPrefab, transmitterObject.transform.position, Quaternion.identity);
        wave.GetComponent<Renderer>().material = new Material(wavesMaterial);

        // Slightly different color for harmonics
        Color harmonicColor = signalColor;
        harmonicColor.r += Random.Range(-0.1f, 0.1f);
        harmonicColor.g += Random.Range(-0.1f, 0.1f);
        harmonicColor.b += Random.Range(-0.1f, 0.1f);
        harmonicColor.a = signalColor.a * 0.8f;

        wave.GetComponent<Renderer>().material.SetColor("_Color", harmonicColor);

        WavePropagation propagation = wave.AddComponent<WavePropagation>();
        propagation.speed = waveSpeed * Random.Range(0.9f, 1.1f); // Slight speed variation
        propagation.maxDistance = maxWaveDistance;
        propagation.signalPower = transmitPower * 0.5f; // Lower power for harmonics
        propagation.frequency = frequency;
        propagation.transmitter = transmitterObject;
        propagation.receivers = receiverObjects;
        propagation.wirelessController = this;
        propagation.reflectionPrefab = reflectionEffectPrefab;
        propagation.reflectionCoefficient = reflectionCoefficient * 0.8f; // Less reflection for harmonics

        waveVisualizers.Add(wave);
    }

    void CleanUpOldWaves()
    {
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var wave in waveVisualizers)
        {
            if (wave == null || (wave.GetComponent<WavePropagation>() != null &&
                                wave.GetComponent<WavePropagation>().HasReachedMaxDistance()))
            {
                if (wave != null)
                    Destroy(wave);

                toRemove.Add(wave);
            }
        }

        foreach (var item in toRemove)
        {
            waveVisualizers.Remove(item);
        }
    }

    void CalculateSignalPropagation()
    {
        // Don't need to run the job if we're not showing propagation and we've already calculated for receivers
        if (!showSignalPropagation && Time.frameCount % 10 != 0)
            return;

        // Create job to calculate signal propagation
        SignalPropagationJob propagationJob = new SignalPropagationJob
        {
            transmitterPosition = transmitterObject.transform.position,
            obstacles = environmentObstacles,
            obstacleMaterials = obstacleMaterials,
            visualizationPoints = visualizationPoints,
            signalStrengths = signalStrengths,
            transmitPower = transmitPower,
            frequency = transmitFrequency,
            airAttenuation = airAttenuation,
            maxDistance = maxWaveDistance,
            reflectionCoefficient = reflectionCoefficient,
            diffractionFactor = diffractionFactor
        };

        // Schedule the job
        signalPropagationJobHandle = propagationJob.Schedule(showSignalPropagation ? visualizationPoints.Length : 1, 64);

        // Wait for job to complete
        signalPropagationJobHandle.Complete();
    }

    void UpdateReceivers()
    {
        foreach (var receiver in receiverObjects)
        {
            if (receiver == null)
                continue;

            // Calculate signal strength at receiver
            float signalStrength = CalculateSignalStrengthAt(receiver.transform.position);
            receiverSignalStrengths[receiver] = signalStrength;

            // Update receiver component
            ReceiverComponent receiverComp = receiver.GetComponent<ReceiverComponent>();
            if (receiverComp != null)
            {
                receiverComp.UpdateSignalStrength(signalStrength, receiverSensitivity);
            }
        }
    }

    public void NotifyReceiverOfWave(GameObject receiver, float wavePower, float waveFrequency)
    {
        // Called when a wave visualization reaches a receiver
        if (receiver == null)
            return;

        ReceiverComponent receiverComp = receiver.GetComponent<ReceiverComponent>();
        if (receiverComp != null)
        {
            receiverComp.OnWaveContact(wavePower, waveFrequency);
        }
    }

    void UpdateSignalInfo()
    {
        // Update UI with signal information for the primary receiver (first in list)
        if (receiverObjects.Count > 0 && receiverObjects[0] != null)
        {
            float receivedPowerDBm = receiverSignalStrengths[receiverObjects[0]];

            // Update signal strength text
            if (signalStrengthText != null)
                signalStrengthText.text = $"Signal: {receivedPowerDBm.ToString("F1")} dBm";

            // Update throughput text
            if (throughputText != null)
            {
                if (receivedPowerDBm >= receiverSensitivity)
                {
                    string speedUnit = "Mbps";
                    float displaySpeed = calculatedThroughput;

                    // Use appropriate units
                    if (displaySpeed > 1000)
                    {
                        displaySpeed /= 1000;
                        speedUnit = "Gbps";
                    }

                    throughputText.text = $"Data Rate: {displaySpeed.ToString("F1")} {speedUnit}";

                    // Color-code based on speed
                    if (calculatedThroughput > 100)
                        throughputText.color = Color.green;
                    else if (calculatedThroughput > 20)
                        throughputText.color = Color.yellow;
                    else
                        throughputText.color = Color.red;
                }
                else
                {
                    throughputText.text = "Data Rate: No Connection";
                    throughputText.color = Color.red;
                }
            }

            // Update channel information if using specific channel
            if (availableChannels.Count > 0 && currentChannelIndex < availableChannels.Count)
            {
                WirelessChannel currentChannel = availableChannels[currentChannelIndex];

                if (channelDropdown != null && !channelDropdown.gameObject.activeSelf)
                {
                    // Update dropdown to reflect current channel if not actively being used
                    channelDropdown.value = currentChannelIndex;
                }
            }

            // Update MIMO panel if exists
            if (mimoConfigPanel != null)
            {
                mimoConfigPanel.SetActive(enableMIMO);

                // Update MIMO settings display
                if (enableMIMO)
                {
                    // Find and update MIMO specific text elements
                    Text[] mimoTexts = mimoConfigPanel.GetComponentsInChildren<Text>();
                    foreach (var text in mimoTexts)
                    {
                        if (text.name == "TxAntennasText")
                            text.text = $"Tx Antennas: {mimoTxAntennas}";
                        else if (text.name == "RxAntennasText")
                            text.text = $"Rx Antennas: {mimoRxAntennas}";
                        else if (text.name == "SpatialStreamsText")
                            text.text = $"Spatial Streams: {Mathf.Min(mimoTxAntennas, mimoRxAntennas)}";
                    }
                }
            }
        }
    }

    float EvaluateAntennaPattern(GameObject source, Vector3 targetPos, AnimationCurve pattern)
    {
        // Calculate angle between forward direction and target
        Vector3 directionToTarget = (targetPos - source.transform.position).normalized;
        float angle = Vector3.Angle(source.transform.forward, directionToTarget);

        // Normalize angle to 0-1 range for animation curve
        float normalizedAngle = angle / 180.0f;

        // Evaluate pattern and convert to dB gain
        return pattern.Evaluate(normalizedAngle) * 30.0f - 15.0f; // Scale to roughly -15 to +15 dB range
    }

    void HandleUserInteraction()
    {
        // Allow dragging the transmitter with right mouse button
        if (Input.GetMouseButton(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject == transmitterObject)
                {
                    // Create a plane at the object's height
                    Plane dragPlane = new Plane(Vector3.up, transmitterObject.transform.position);
                    float distance;

                    if (dragPlane.Raycast(ray, out distance))
                    {
                        Vector3 point = ray.GetPoint(distance);
                        transmitterObject.transform.position = new Vector3(point.x, transmitterObject.transform.position.y, point.z);
                    }
                }
                // Allow dragging receivers too
                else
                {
                    foreach (var receiver in receiverObjects)
                    {
                        if (receiver != null && hit.collider.gameObject == receiver)
                        {
                            // Create a plane at the object's height
                            Plane dragPlane = new Plane(Vector3.up, receiver.transform.position);
                            float distance;

                            if (dragPlane.Raycast(ray, out distance))
                            {
                                Vector3 point = ray.GetPoint(distance);
                                receiver.transform.position = new Vector3(point.x, receiver.transform.position.y, point.z);
                            }

                            break;
                        }
                    }
                }
            }
        }

        // Rotate transmitter with middle mouse button
        if (Input.GetMouseButton(2))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject == transmitterObject)
                {
                    float rotateSpeed = 100f;
                    float horizontal = Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;

                    transmitterObject.transform.Rotate(Vector3.up, horizontal);
                }
            }
        }
    }

    void OnDestroy()
    {
        // Clean up native arrays
        if (environmentObstacles.IsCreated)
            environmentObstacles.Dispose();

        if (obstacleMaterials.IsCreated)
            obstacleMaterials.Dispose();

        if (visualizationPoints.IsCreated)
            visualizationPoints.Dispose();

        if (signalStrengths.IsCreated)
            signalStrengths.Dispose();
    }
}

// Job to calculate signal propagation in parallel
public struct SignalPropagationJob : IJobParallelFor
{
    // Input data
    public float3 transmitterPosition;
    [ReadOnly] public NativeArray<float3> obstacles;
    [ReadOnly] public NativeArray<float> obstacleMaterials;
    [ReadOnly] public NativeArray<float3> visualizationPoints;

    // Parameters
    public float transmitPower;
    public float frequency;
    public float airAttenuation;
    public float maxDistance;
    public float reflectionCoefficient;
    public float diffractionFactor;

    // Output
    public NativeArray<float> signalStrengths;

    public void Execute(int index)
    {
        // Calculate signal strength at each visualization point
        float3 direction = visualizationPoints[index];
        float3 pointPosition = transmitterPosition + direction * maxDistance;

        // Free space path loss
        float distance = math.distance(transmitterPosition, pointPosition);
        float distanceKm = distance / 1000.0f;
        float wavelength = 299.792458f / frequency; // speed of light (m/s) / frequency (MHz) to get wavelength in meters

        // Free space path loss formula: FSPL(dB) = 20*log10(d) + 20*log10(f) + 32.44
        float pathLoss = 20 * math.log10(distanceKm) + 20 * math.log10(frequency) + 32.44f;

        // Calculate attenuated power
        float transmitPowerDBm = 10 * math.log10(transmitPower);
        float signalPowerDBm = transmitPowerDBm - pathLoss;

        // Apply some randomness for realistic propagation
        signalPowerDBm += (noise.snoise(new float3(pointPosition.x * 0.5f, pointPosition.y * 0.5f, pointPosition.z * 0.5f)) * 2f);

        bool obstructed = false;

        // Check for obstacles in path
        for (int i = 0; i < obstacles.Length; i++)
        {
            float3 obstaclePos = obstacles[i];
            float3 transmitterToObstacle = obstaclePos - transmitterPosition;
            float3 transmitterToPoint = pointPosition - transmitterPosition;

            // Project obstacle onto the ray from transmitter to point
            float projectionLength = math.dot(transmitterToObstacle, math.normalize(transmitterToPoint));

            // Check if obstacle is in the path
            if (projectionLength > 0 && projectionLength < distance)
            {
                float3 projectionPoint = transmitterPosition + math.normalize(transmitterToPoint) * projectionLength;

                // Check if projection point is close to obstacle (simple approximation)
                if (math.distance(projectionPoint, obstaclePos) < 2.0f) // Assuming obstacle radius of ~2 meters
                {
                    // Apply material-specific attenuation
                    signalPowerDBm -= obstacleMaterials[i] * 20.0f; // Scale by material factor
                    obstructed = true;

                    // Accumulate attenuation for multiple obstacles
                    break; // For simplicity, only consider first obstacle hit
                }
            }
        }

        // Apply diffraction for obstructed paths
        if (obstructed && diffractionFactor > 0)
        {
            // Add diffraction component
            float diffractionComponent = transmitPowerDBm - pathLoss - 15.0f; // Extra loss for diffraction
            diffractionComponent *= diffractionFactor;

            // Convert to linear power
            float linearSignalPower = math.pow(10, signalPowerDBm / 10);
            float linearDiffraction = math.pow(10, diffractionComponent / 10);

            // Combine powers
            float totalPower = linearSignalPower + linearDiffraction;

            // Convert back to dBm
            signalPowerDBm = 10 * math.log10(totalPower);
        }

        // Store calculated signal strength
        signalStrengths[index] = signalPowerDBm;
    }
}