// Tardigrade Simulation UI Controller
// Manages the user interface for interacting with the tardigrade simulation

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TardigradeUIController : MonoBehaviour
{
    [Header("Simulation References")]
    public TardigradeSimulator simulator;
    public TardigradeEnvironment environment;

    [Header("Control UI")]
    public Slider timeScaleSlider;
    public Toggle pauseToggle;
    public Button resetButton;
    public TMP_Dropdown viewModeDropdown;

    [Header("Environment Controls")]
    public Slider humiditySlider;
    public Slider temperatureSlider;
    public Toggle dehydrationToggle;
    public Toggle heatStressToggle;
    public Toggle freezingToggle;
    public Toggle radiationToggle;
    public Slider stressResponseTimeSlider;

    [Header("Statistics UI")]
    public TMP_Text cellCountText;

    [Header("Cell Type Visualization")]
    public Toggle showEpidermalToggle;
    public Toggle showMuscleToggle;
    public Toggle showNerveToggle;
    public Toggle showDigestiveToggle;
    public Toggle showStorageToggle;
    public Toggle showReproductiveToggle;
    public Toggle showLegToggle;

    [Header("Visualization Materials")]
    public Material environmentMaterial;
    public Material waterVisualizationMaterial;
    public Material cryptobiosisEffectMaterial;

    // Internal state
    private bool isCryptobiosisTransitioning = false;
    private float cryptobiosisTransitionAmount = 0f;
    private bool isShowingCellInfo = false;
    private TardigradeCell selectedCellType = TardigradeCell.Epidermal;
    private Dictionary<TardigradeCell, int> cellTypeCounts = new Dictionary<TardigradeCell, int>();
    private Dictionary<MetabolicState, int> stateCounts = new Dictionary<MetabolicState, int>();

    void Start()
    {
        InitializeUI();
        SetupEventHandlers();
    }

    void Update()
    {
        UpdateStatistics();
        UpdateEnvironmentVisualization();
        HandleCryptobiosisTransition();
    }

    private void InitializeUI()
    {
        // Initialize dictionaries
        foreach (TardigradeCell cellType in System.Enum.GetValues(typeof(TardigradeCell)))
        {
            cellTypeCounts[cellType] = 0;
        }

        foreach (MetabolicState state in System.Enum.GetValues(typeof(MetabolicState)))
        {
            stateCounts[state] = 0;
        }

        // Initialize sliders with default values
        if (timeScaleSlider != null)
        {
            timeScaleSlider.value = simulator.simulationSpeed;
        }

        if (humiditySlider != null && environment != null)
        {
            humiditySlider.value = environment.humidity;
        }

        if (temperatureSlider != null && environment != null)
        {
            temperatureSlider.value = environment.temperature;
        }

        // Initialize dropdowns
        if (viewModeDropdown != null)
        {
            viewModeDropdown.ClearOptions();

            List<string> options = new List<string>
            {
                "All Cells",
                "Epidermal",
                "Muscle",
                "Nerve",
                "Digestive",
                "Storage",
                "Reproductive",
                "Legs"
            };

            viewModeDropdown.AddOptions(options);
        }
    }

    private void SetupEventHandlers()
    {
        // Setup listeners for UI controls
        if (timeScaleSlider != null)
        {
            timeScaleSlider.onValueChanged.AddListener(OnTimeScaleChanged);
        }

        if (pauseToggle != null)
        {
            pauseToggle.onValueChanged.AddListener(OnPauseToggled);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }

        if (viewModeDropdown != null)
        {
            viewModeDropdown.onValueChanged.AddListener(OnViewModeChanged);
        }

        // Environment controls
        if (humiditySlider != null)
        {
            humiditySlider.onValueChanged.AddListener(OnHumidityChanged);
        }

        if (temperatureSlider != null)
        {
            temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);
        }

        if (dehydrationToggle != null)
        {
            dehydrationToggle.onValueChanged.AddListener(OnDehydrationToggled);
        }

        if (heatStressToggle != null)
        {
            heatStressToggle.onValueChanged.AddListener(OnHeatStressToggled);
        }

        if (freezingToggle != null)
        {
            freezingToggle.onValueChanged.AddListener(OnFreezingToggled);
        }

        if (radiationToggle != null)
        {
            radiationToggle.onValueChanged.AddListener(OnRadiationToggled);
        }

        // Cell type visualization toggles
        SetupCellTypeToggles();
    }

    private void SetupCellTypeToggles()
    {
        // Setup listeners for cell type toggles
        if (showEpidermalToggle != null)
            showEpidermalToggle.onValueChanged.AddListener((value) => ToggleCellTypeVisibility(TardigradeCell.Epidermal, value));

        if (showMuscleToggle != null)
            showMuscleToggle.onValueChanged.AddListener((value) => ToggleCellTypeVisibility(TardigradeCell.Muscle, value));

        if (showNerveToggle != null)
            showNerveToggle.onValueChanged.AddListener((value) => ToggleCellTypeVisibility(TardigradeCell.Nerve, value));

        if (showDigestiveToggle != null)
            showDigestiveToggle.onValueChanged.AddListener((value) => ToggleCellTypeVisibility(TardigradeCell.Digestive, value));

        if (showStorageToggle != null)
            showStorageToggle.onValueChanged.AddListener((value) => ToggleCellTypeVisibility(TardigradeCell.Storage, value));

        if (showReproductiveToggle != null)
            showReproductiveToggle.onValueChanged.AddListener((value) => ToggleCellTypeVisibility(TardigradeCell.Reproductive, value));

        if (showLegToggle != null)
            showLegToggle.onValueChanged.AddListener((value) => ToggleCellTypeVisibility(TardigradeCell.Leg, value));
    }

    private void UpdateStatistics()
    {
        // Skip if simulator is not assigned
        if (simulator == null) return;

        // Clear counters
        foreach (TardigradeCell cellType in System.Enum.GetValues(typeof(TardigradeCell)))
        {
            cellTypeCounts[cellType] = 0;
        }

        foreach (MetabolicState state in System.Enum.GetValues(typeof(MetabolicState)))
        {
            stateCounts[state] = 0;
        }

        // Count cell types and states
        for (int i = 0; i < simulator.cellData.Length; i++)
        {
            TardigradeCellData cell = simulator.cellData[i];

            // Count by type
            cellTypeCounts[cell.cellType]++;

            // Count by state
            stateCounts[cell.metabolicState]++;
        }

        // Update UI
        UpdateStatisticsUI();
    }

    private void UpdateStatisticsUI()
    {
        if (cellCountText != null)
        {
            cellCountText.text = string.Format("Total Cells: {0}", simulator.cellData.Length);
            cellCountText.text += string.Format(
                "\n\nActive: {0}\nStressed: {1}\nTun: {2}\nCyst: {3}",
                stateCounts[MetabolicState.Active],
                stateCounts[MetabolicState.Stressed],
                stateCounts[MetabolicState.Tun],
                stateCounts[MetabolicState.Cyst]
            );
            cellCountText.text += string.Format("\n\nSimulation Time: {0:F1}s", Time.time);
            cellCountText.text += string.Format(
                "\n\nHumidity: {0:F1}%\nTemperature: {1:F1}°C\n{2}",
                environment.humidity,
                environment.temperature,
                GetEnvironmentalStateDescription()
            );
        }
    }

    private string GetEnvironmentalStateDescription()
    {
        if (environment == null) return "Unknown";

        // Determine current environmental conditions
        if (environment.humidity < environment.cryptobiosisThreshold)
        {
            return "Dehydrated (Cryptobiosis)";
        }
        else if (environment.temperature < 0f)
        {
            return "Freezing (Cryptobiosis)";
        }
        else if (environment.temperature > 40f)
        {
            return "Heat Stress";
        }
        else if (environment.radiationLevel > 50f)
        {
            return "Radiation Exposure";
        }
        else if (environment.simulateVacuum)
        {
            return "Vacuum (Cryptobiosis)";
        }
        else
        {
            return "Normal Conditions";
        }
    }

    private void UpdateEnvironmentVisualization()
    {
        if (environment == null) return;

        // Update environment material properties
        if (environmentMaterial != null)
        {
            environmentMaterial.SetFloat("_Humidity", environment.humidity);
            environmentMaterial.SetFloat("_Temperature", environment.temperature);
            environmentMaterial.SetFloat("_CryptobiosisThreshold", environment.cryptobiosisThreshold);
        }

        // Update water visualization
        if (waterVisualizationMaterial != null)
        {
            waterVisualizationMaterial.SetFloat("_WaterLevel", environment.humidity / 100f);
        }
    }

    private void HandleCryptobiosisTransition()
    {
        if (environment == null) return;

        bool shouldBeCryptobiotic = environment.humidity < environment.cryptobiosisThreshold ||
                                   environment.temperature < 0f ||
                                   environment.simulateVacuum;

        // Check for transition
        if (shouldBeCryptobiotic && !isCryptobiosisTransitioning && cryptobiosisTransitionAmount < 1f)
        {
            // Start transition to cryptobiosis
            isCryptobiosisTransitioning = true;
            StartCoroutine(CryptobiosisTransition(true));
        }
        else if (!shouldBeCryptobiotic && !isCryptobiosisTransitioning && cryptobiosisTransitionAmount > 0f)
        {
            // Start transition from cryptobiosis
            isCryptobiosisTransitioning = true;
            StartCoroutine(CryptobiosisTransition(false));
        }

        // Update shader with transition amount
        if (cryptobiosisEffectMaterial != null)
        {
            cryptobiosisEffectMaterial.SetFloat("_TransitionAmount", cryptobiosisTransitionAmount);
        }
    }

    private IEnumerator CryptobiosisTransition(bool entering)
    {
        float transitionTime = entering ? environment.stressResponseTime : environment.recoveryRate * 10f;
        float startAmount = cryptobiosisTransitionAmount;
        float targetAmount = entering ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < transitionTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionTime;
            cryptobiosisTransitionAmount = Mathf.Lerp(startAmount, targetAmount, t);

            yield return null;
        }

        cryptobiosisTransitionAmount = targetAmount;
        isCryptobiosisTransitioning = false;
    }

    // UI Event Handlers

    private void OnTimeScaleChanged(float value)
    {
        if (simulator != null)
        {
            simulator.simulationSpeed = value;
        }
    }

    private void OnPauseToggled(bool paused)
    {
        if (simulator != null)
        {
            // Handle pause state in the simulator
        }
    }

    private void OnResetClicked()
    {
        // Reset simulation
        ResetSimulation();
    }

    private void OnViewModeChanged(int index)
    {
        // Change visualization mode based on dropdown selection
        if (simulator != null)
        {
            // 0 = All Cells, 1-7 = specific cell types
            if (index == 0)
            {
                // Show all cells
                ShowAllCellTypes();
            }
            else
            {
                // Show only selected cell type
                HideAllCellTypes();
                TardigradeCell selectedType = (TardigradeCell)(index - 1);
                ToggleCellTypeVisibility(selectedType, true);
            }
        }
    }

    private void OnHumidityChanged(float value)
    {
        if (environment != null)
        {
            environment.humidity = value;
        }
    }

    private void OnTemperatureChanged(float value)
    {
        if (environment != null)
        {
            environment.temperature = value;
        }
    }

    private void OnDehydrationToggled(bool enabled)
    {
        if (environment != null)
        {
            if (enabled)
            {
                // Simulate dehydration
                environment.humidity = 20f;
                humiditySlider.value = 20f;
            }
            else
            {
                // Normal humidity
                environment.humidity = 80f;
                humiditySlider.value = 80f;
            }

            environment.simulateDehydration = enabled;
        }
    }

    private void OnHeatStressToggled(bool enabled)
    {
        if (environment != null)
        {
            if (enabled)
            {
                // Simulate heat stress
                environment.temperature = 45f;
                temperatureSlider.value = 45f;
            }
            else
            {
                // Normal temperature
                environment.temperature = 25f;
                temperatureSlider.value = 25f;
            }

            environment.simulateHeatStress = enabled;
        }
    }

    private void OnFreezingToggled(bool enabled)
    {
        if (environment != null)
        {
            if (enabled)
            {
                // Simulate freezing
                environment.temperature = -10f;
                temperatureSlider.value = -10f;
            }
            else
            {
                // Normal temperature
                environment.temperature = 25f;
                temperatureSlider.value = 25f;
            }

            environment.simulateFreezing = enabled;
        }
    }

    private void OnRadiationToggled(bool enabled)
    {
        if (environment != null)
        {
            environment.simulateRadiation = enabled;
            environment.radiationLevel = enabled ? 80f : 0f;
        }
    }

    private void ToggleCellTypeVisibility(TardigradeCell cellType, bool visible)
    {
        // Implementation will depend on how you're handling visibility
        // This is just a placeholder
        Debug.Log("Toggle visibility for cell type: " + cellType + " to " + visible);
    }

    private void ShowAllCellTypes()
    {
        foreach (TardigradeCell cellType in System.Enum.GetValues(typeof(TardigradeCell)))
        {
            ToggleCellTypeVisibility(cellType, true);
        }
    }

    private void HideAllCellTypes()
    {
        foreach (TardigradeCell cellType in System.Enum.GetValues(typeof(TardigradeCell)))
        {
            ToggleCellTypeVisibility(cellType, false);
        }
    }

    private void ResetSimulation()
    {
        // Reset to default values
        if (environment != null)
        {
            environment.humidity = 80f;
            environment.temperature = 25f;
            environment.radiationLevel = 0f;
            environment.simulateDehydration = false;
            environment.simulateHeatStress = false;
            environment.simulateFreezing = false;
            environment.simulateRadiation = false;
            environment.simulateVacuum = false;
        }

        // Update UI to match reset values
        if (humiditySlider != null) humiditySlider.value = 80f;
        if (temperatureSlider != null) temperatureSlider.value = 25f;
        if (dehydrationToggle != null) dehydrationToggle.isOn = false;
        if (heatStressToggle != null) heatStressToggle.isOn = false;
        if (freezingToggle != null) freezingToggle.isOn = false;
        if (radiationToggle != null) radiationToggle.isOn = false;

        // Reset visualization
        cryptobiosisTransitionAmount = 0f;
        isCryptobiosisTransitioning = false;

        // Reload simulation
        if (simulator != null)
        {
            // Ideally you would call a reset method in the simulator
            // For now we'll just log that reset was called
            Debug.Log("Simulation Reset Requested");
        }
    }
}