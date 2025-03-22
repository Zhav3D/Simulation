// Tardigrade Cell Type Definitions
// Replace the current CellType enum with this tardigrade-specific version

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

// Tardigrade-specific cell types
public enum TardigradeCell
{
    Epidermal,    // Outer protective layer
    Muscle,       // Both longitudinal and circular muscles for movement
    Nerve,        // Simple brain and ganglia
    Digestive,    // Simple gut tube
    Storage,      // Store nutrients and energy
    Reproductive, // Germ cells
    Leg           // Cells that form the distinctive legs
}

// Tardigrade body segments
public enum BodySegment
{
    Head,
    Segment1,
    Segment2,
    Segment3,
    Segment4
}

// Tardigrade metabolic states
public enum MetabolicState
{
    Active,         // Normal metabolic activity
    Stressed,       // Response to mild environmental stress
    Tun,            // Dehydrated cryptobiotic state (tun formation)
    Cyst,           // Encystment for protection
    Anhydrobiotic   // Complete dehydration state (extreme survival)
}

// Enhanced cell data structure specific to tardigrades
public struct TardigradeCellData
{
    // Basic properties
    public float3 position;
    public float3 velocity;
    public float energy;
    public float age;
    public TardigradeCell cellType;
    public CellState cellState;
    public MetabolicState metabolicState;

    // Segmentation and organization
    public BodySegment segment;
    public float segmentPosition; // 0-1 position within segment

    // Cell adhesion and structure
    public float adhesionStrength;
    public float structuralIntegrity;

    // Tardigrade-specific properties
    public float waterContent;       // 0-1, critical for cryptobiosis
    public float trehaloseLevel;     // Sugar used for dry-state protection
    public float heatShockProteins;  // Stress proteins for protection

    // Movement and behavior
    public bool isLegCell;
    public int legPair;              // 1-4 for the four leg pairs
    public float contractionState;   // For muscle cells
}

// TardigradeEnvironment class to handle tardigrade-specific environment factors
public class TardigradeEnvironment : MonoBehaviour
{
    // Environment parameters
    [Header("Environmental Parameters")]
    [Range(0, 100)] public float humidity = 80f;
    [Range(-20, 100)] public float temperature = 25f;
    [Range(0, 14)] public float pH = 7.0f;
    [Range(0, 100)] public float radiationLevel = 0f;
    [Range(0, 100)] public float oxygenLevel = 21f;

    [Header("Stress Conditions")]
    public bool simulateDehydration = false;
    public bool simulateHeatStress = false;
    public bool simulateRadiation = false;
    public bool simulateFreezing = false;
    public bool simulateVacuum = false;

    [Header("Transition Parameters")]
    public float cryptobiosisThreshold = 30f; // Humidity below this triggers cryptobiosis
    public float recoveryRate = 0.1f;         // How quickly they recover from cryptobiosis
    public float stressResponseTime = 10f;    // Time to transition to cryptobiosis

    // Simulation methods
    public void ApplyEnvironmentToCells(NativeArray<TardigradeCellData> cells, float deltaTime)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            TardigradeCellData cell = cells[i];

            // Apply environmental effects based on conditions
            if (humidity < cryptobiosisThreshold)
            {
                // Trigger dehydration response
                DehydrateCell(ref cell, deltaTime);
            }
            else if (cell.metabolicState != MetabolicState.Active && humidity > 60f)
            {
                // Recover from cryptobiosis
                RehydrateCell(ref cell, deltaTime);
            }

            // Apply temperature effects
            ApplyTemperatureEffects(ref cell, deltaTime);

            // Apply radiation effects if present
            if (radiationLevel > 0)
            {
                ApplyRadiationEffects(ref cell, deltaTime);
            }

            cells[i] = cell;
        }
    }

    private void DehydrateCell(ref TardigradeCellData cell, float deltaTime)
    {
        // Reduce water content based on environment humidity
        float dehydrationRate = (cryptobiosisThreshold - humidity) / cryptobiosisThreshold;
        dehydrationRate *= 0.1f * deltaTime; // Scale by time

        cell.waterContent = Mathf.Max(0.1f, cell.waterContent - dehydrationRate);

        // Increase protective compounds
        cell.trehaloseLevel = Mathf.Min(1.0f, cell.trehaloseLevel + (0.05f * deltaTime));

        // Adjust metabolic state based on water content
        if (cell.waterContent < 0.3f && cell.metabolicState == MetabolicState.Active)
        {
            cell.metabolicState = MetabolicState.Stressed;
        }
        else if (cell.waterContent < 0.2f && cell.metabolicState == MetabolicState.Stressed)
        {
            cell.metabolicState = MetabolicState.Tun;

            // In tun state, movement stops
            cell.velocity = new float3(0, 0, 0);
        }
    }

    private void RehydrateCell(ref TardigradeCellData cell, float deltaTime)
    {
        // Increase water content based on environment humidity
        float rehydrationRate = (humidity - cryptobiosisThreshold) / (100f - cryptobiosisThreshold);
        rehydrationRate *= recoveryRate * deltaTime;

        cell.waterContent = Mathf.Min(1.0f, cell.waterContent + rehydrationRate);

        // Consume protective compounds during rehydration
        cell.trehaloseLevel = Mathf.Max(0.1f, cell.trehaloseLevel - (0.02f * deltaTime));

        // Adjust metabolic state based on water content
        if (cell.waterContent > 0.6f && cell.metabolicState == MetabolicState.Tun)
        {
            cell.metabolicState = MetabolicState.Stressed;
        }
        else if (cell.waterContent > 0.8f && cell.metabolicState == MetabolicState.Stressed)
        {
            cell.metabolicState = MetabolicState.Active;

            // Restore some movement capability
            if (cell.cellType == TardigradeCell.Muscle)
            {
                cell.contractionState = UnityEngine.Random.value * 0.2f;
            }
        }
    }

    private void ApplyTemperatureEffects(ref TardigradeCellData cell, float deltaTime)
    {
        // Handle extreme temperatures
        if (temperature > 40f)
        {
            // Heat stress
            cell.heatShockProteins = Mathf.Min(1.0f, cell.heatShockProteins + (0.1f * deltaTime));

            if (cell.metabolicState == MetabolicState.Active)
            {
                cell.metabolicState = MetabolicState.Stressed;
            }
        }
        else if (temperature < 0f)
        {
            // Cold stress - trigger cryptobiosis
            if (cell.metabolicState == MetabolicState.Active)
            {
                cell.metabolicState = MetabolicState.Tun;
                cell.velocity = new float3(0, 0, 0);
            }
        }
        else
        {
            // Normal temperature range
            if (cell.metabolicState != MetabolicState.Tun &&
                cell.metabolicState != MetabolicState.Anhydrobiotic)
            {
                // Gradually reduce stress proteins in normal conditions
                cell.heatShockProteins = Mathf.Max(0.1f, cell.heatShockProteins - (0.02f * deltaTime));
            }
        }
    }

    private void ApplyRadiationEffects(ref TardigradeCellData cell, float deltaTime)
    {
        // Tardigrades are highly resistant to radiation
        if (radiationLevel > 70f)
        {
            // Very high radiation, even tardigrades have limits
            cell.structuralIntegrity -= 0.01f * deltaTime;

            // Trigger production of protective compounds
            cell.heatShockProteins = Mathf.Min(1.0f, cell.heatShockProteins + (0.05f * deltaTime));

            if (cell.metabolicState == MetabolicState.Active)
            {
                cell.metabolicState = MetabolicState.Stressed;
            }
        }
        else if (radiationLevel > 20f)
        {
            // Moderate radiation - tardigrades respond with protection mechanisms
            cell.heatShockProteins = Mathf.Min(1.0f, cell.heatShockProteins + (0.02f * deltaTime));
        }
    }
}