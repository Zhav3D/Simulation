// This file contains the implementation of cellular behaviors and signaling systems

using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

// Cell Signal Types - different molecular signals cells can exchange
public enum SignalType
{
    Growth,         // Stimulates cell growth and division
    Differentiation, // Causes cells to specialize
    Maintenance,    // Maintains normal cell function
    Stress,         // Indicates cellular stress
    Apoptosis       // Triggers programmed cell death
}

// SignalData structure to represent molecular signals
public struct SignalData
{
    public SignalType type;     // Type of signal
    public float strength;      // Signal strength (0-1)
    public float duration;      // How long the signal has been active
    public int sourceCell;      // Cell that produced the signal
    public float radius;        // Diffusion radius
}

// Expanded CellData with more biological properties
public struct DetailedCellData
{
    // Basic properties
    public float3 position;
    public float3 velocity;
    public float energy;
    public float age;
    public CellType type;
    public CellState state;

    // Additional biological properties
    public float metabolism;        // Rate of energy consumption
    public float divisionProbability; // Likelihood of division when conditions are right
    public float adhesionStrength;   // How strongly it binds to other cells
    public float motility;           // How actively it moves
    public float specialization;     // How specialized/differentiated the cell is

    // Cell membrane properties
    public float membranePermeability; // How easily signals pass through
    public float membraneIntegrity;    // Membrane health

    // Receptors and signaling
    public NativeArray<float> receptors;  // Sensitivity to different signals
    public NativeArray<float> signalProduction; // How much of each signal is produced

    // Internal cell components
    public float mitochondriaCount;   // Energy production
    public float ribosomeActivity;    // Protein synthesis rate
    public float lysosomeActivity;    // Waste processing

    // Cell cycle
    public float cellCyclePhase;      // 0-1 representing cell cycle progress
    public bool inMitosis;            // Whether currently dividing
}

// CellBehaviorManager - manages complex cell behaviors
public class CellBehaviorManager : MonoBehaviour
{
    // Reference to main simulation
    public FruitFlyCellSimulator mainSimulator;

    // Signal propagation parameters
    [Header("Signaling Parameters")]
    public float signalPropagationSpeed = 10f;
    public float signalDecayRate = 0.5f;
    public int maxActiveSignals = 10000;

    // Cell differentiation parameters
    [Header("Differentiation Parameters")]
    public float differentiationThreshold = 0.7f;
    public float spontaneousDifferentiationRate = 0.01f;

    // Active signals in the simulation
    private List<SignalData> activeSignals = new List<SignalData>();
    private NativeArray<SignalData> signalArray;

    // Detailed cell data (for cells that need more complex simulation)
    private List<DetailedCellData> detailedCells = new List<DetailedCellData>();

    // Cell lineage tracking - for developmental simulation
    private Dictionary<int, List<int>> cellLineage = new Dictionary<int, List<int>>();

    void Start()
    {
        signalArray = new NativeArray<SignalData>(maxActiveSignals, Allocator.Persistent);
        InitializeDetailedCells();
    }

    void Update()
    {
        ProcessSignals(Time.deltaTime);
        UpdateDetailedCells(Time.deltaTime);
    }

    private void InitializeDetailedCells()
    {
        // Create detailed data for a subset of cells for more complex simulation
        int detailedCellCount = Mathf.Min(1000, mainSimulator.initialCellCount / 5);

        for (int i = 0; i < detailedCellCount; i++)
        {
            DetailedCellData cell = CreateDetailedCell(mainSimulator.cellData[i], i);
            detailedCells.Add(cell);
        }
    }

    private DetailedCellData CreateDetailedCell(CellData basicCell, int index)
    {
        // Create receptors and signal production arrays
        NativeArray<float> receptors = new NativeArray<float>(System.Enum.GetValues(typeof(SignalType)).Length, Allocator.Persistent);
        NativeArray<float> signalProduction = new NativeArray<float>(System.Enum.GetValues(typeof(SignalType)).Length, Allocator.Persistent);

        // Initialize with type-specific values
        for (int i = 0; i < receptors.Length; i++)
        {
            receptors[i] = UnityEngine.Random.Range(0.2f, 0.8f);
            signalProduction[i] = UnityEngine.Random.Range(0.0f, 0.3f);
        }

        // Set cell type specific values
        float metabolism = 0.05f;
        float adhesion = 0.5f;
        float motility = 0.5f;
        float specialization = 0.2f;

        switch (basicCell.type)
        {
            case CellType.Epithelial:
                adhesion = 0.9f;  // Strong adhesion
                motility = 0.2f;  // Low motility
                specialization = 0.7f;
                // Higher receptors for maintenance signals
                receptors[(int)SignalType.Maintenance] = 0.9f;
                break;

            case CellType.Neuron:
                metabolism = 0.08f; // Higher metabolism
                adhesion = 0.4f;
                motility = 0.1f;    // Mostly stationary
                specialization = 0.9f; // Highly specialized
                // Strong signal production
                signalProduction[(int)SignalType.Differentiation] = 0.8f;
                break;

            case CellType.Muscle:
                metabolism = 0.07f;
                adhesion = 0.7f;
                motility = 0.3f;
                specialization = 0.8f;
                // Muscle cells produce contraction signals
                signalProduction[(int)SignalType.Differentiation] = 0.5f;
                break;

            case CellType.Immune:
                metabolism = 0.06f;
                adhesion = 0.3f;
                motility = 0.9f;    // Highly mobile
                specialization = 0.7f;
                // Sensitive to stress signals
                receptors[(int)SignalType.Stress] = 0.9f;
                signalProduction[(int)SignalType.Apoptosis] = 0.7f;
                break;

            case CellType.FatBody:
                metabolism = 0.03f;  // Low metabolism
                adhesion = 0.6f;
                motility = 0.2f;
                specialization = 0.6f;
                // High sensitivity to growth signals
                receptors[(int)SignalType.Growth] = 0.9f;
                break;

            default: // Other cell types
                metabolism = 0.05f;
                adhesion = 0.5f;
                motility = 0.6f;
                specialization = 0.4f;
                break;
        }

        // Create the detailed cell data
        DetailedCellData detailedCell = new DetailedCellData
        {
            // Copy basic properties
            position = basicCell.position,
            velocity = basicCell.velocity,
            energy = basicCell.energy,
            age = basicCell.age,
            type = basicCell.type,
            state = basicCell.state,

            // Set additional properties
            metabolism = metabolism,
            divisionProbability = UnityEngine.Random.Range(0.01f, 0.05f),
            adhesionStrength = adhesion,
            motility = motility,
            specialization = specialization,

            // Cell membrane properties
            membranePermeability = UnityEngine.Random.Range(0.3f, 0.7f),
            membraneIntegrity = 1.0f,

            // Receptors and signal production
            receptors = receptors,
            signalProduction = signalProduction,

            // Cell components
            mitochondriaCount = UnityEngine.Random.Range(10, 50),
            ribosomeActivity = UnityEngine.Random.Range(0.5f, 1.0f),
            lysosomeActivity = UnityEngine.Random.Range(0.3f, 0.8f),

            // Cell cycle
            cellCyclePhase = UnityEngine.Random.Range(0f, 1f),
            inMitosis = false
        };

        return detailedCell;
    }

    private void UpdateDetailedCells(float deltaTime)
    {
        for (int i = 0; i < detailedCells.Count; i++)
        {
            DetailedCellData cell = detailedCells[i];

            // Update based on cell type
            UpdateCellMetabolism(ref cell, deltaTime);
            UpdateCellCycle(ref cell, i, deltaTime);
            UpdateCellSignaling(ref cell, deltaTime);

            // Handle cell state transitions
            HandleCellStateTransitions(ref cell, deltaTime);

            // Save updated cell
            detailedCells[i] = cell;

            // Update the corresponding basic cell in the main simulation
            if (i < mainSimulator.initialCellCount)
            {
                CellData basicCell = mainSimulator.cellData[i];
                basicCell.position = cell.position;
                basicCell.velocity = cell.velocity;
                basicCell.energy = cell.energy;
                basicCell.age = cell.age;
                basicCell.state = cell.state;

                mainSimulator.cellData[i] = basicCell;
            }
        }
    }

    private void UpdateCellMetabolism(ref DetailedCellData cell, float deltaTime)
    {
        // Energy consumption based on metabolism rate
        float energyConsumption = cell.metabolism * deltaTime;

        // Add mitochondria influence
        energyConsumption *= (1.0f - (cell.mitochondriaCount / 100f)); // More mitochondria = more efficient

        // Cell state influences energy use
        if (cell.inMitosis)
        {
            energyConsumption *= 3.0f; // Dividing uses more energy
        }

        // Update energy
        cell.energy = Mathf.Max(0, cell.energy - energyConsumption);

        // Try to obtain energy if low
        if (cell.energy < 0.3f && cell.type == CellType.FatBody)
        {
            // Fat body cells can generate energy from reserves
            cell.energy += 0.1f * deltaTime;
        }
    }

    private void UpdateCellCycle(ref DetailedCellData cell, int cellIndex, float deltaTime)
    {
        // Only proceed with cell cycle if not in mitosis and cell is healthy
        if (!cell.inMitosis && cell.state == CellState.Alive)
        {
            // Progress cell cycle based on energy availability
            float cycleProgress = deltaTime * 0.05f * cell.energy;

            // Cell type influences division rate
            switch (cell.type)
            {
                case CellType.Epithelial:
                    cycleProgress *= 1.2f; // Epithelial cells divide more rapidly
                    break;
                case CellType.Neuron:
                    cycleProgress *= 0.1f; // Neurons rarely divide
                    break;
                case CellType.Immune:
                    cycleProgress *= 1.5f; // Immune cells divide quickly when needed
                    break;
            }

            cell.cellCyclePhase += cycleProgress;

            // Check if cell should enter mitosis
            if (cell.cellCyclePhase >= 1.0f && cell.energy > 0.7f)
            {
                cell.inMitosis = true;
                cell.cellCyclePhase = 0f;
                cell.state = CellState.Dividing;

                // Emit division signal
                EmitSignal(SignalType.Growth, cell.position, 10f, cellIndex);
            }
        }
        else if (cell.inMitosis)
        {
            // Progress through mitosis
            cell.cellCyclePhase += deltaTime * 0.2f;

            // Complete division
            if (cell.cellCyclePhase >= 1.0f)
            {
                cell.inMitosis = false;
                cell.energy *= 0.5f; // Division consumes energy
                cell.state = CellState.Alive;

                // In a full implementation, we would create a new cell here
                // For this simplified version, we just reset the cycle
            }
        }
    }

    private void UpdateCellSignaling(ref DetailedCellData cell, float deltaTime)
    {
        // Produce signals based on cell state and type
        for (int i = 0; i < cell.signalProduction.Length; i++)
        {
            float productionRate = cell.signalProduction[i];

            // Adjust based on cell state
            if (cell.state == CellState.Stressed)
            {
                if (i == (int)SignalType.Stress)
                {
                    productionRate *= 3.0f; // Much more stress signal when stressed
                }
            }
            else if (cell.state == CellState.Dying)
            {
                if (i == (int)SignalType.Apoptosis)
                {
                    productionRate *= 5.0f; // Dying cells signal strongly for cleanup
                }
            }

            // Produce signal with some probability
            if (UnityEngine.Random.value < productionRate * deltaTime)
            {
                SignalType signalType = (SignalType)i;
                EmitSignal(signalType, cell.position, 5f + (productionRate * 5f), i);
            }
        }
    }

    private void HandleCellStateTransitions(ref DetailedCellData cell, float deltaTime)
    {
        // Energy-based state transitions
        if (cell.energy <= 0.1f)
        {
            cell.state = CellState.Stressed;
        }
        else if (cell.energy <= 0.05f || cell.age >= 0.95f)
        {
            cell.state = CellState.Dying;
        }

        // Age the cell
        cell.age += deltaTime * 0.01f;

        // Dead cells are removed from the simulation in a full implementation
        if (cell.state == CellState.Dying)
        {
            cell.membraneIntegrity -= deltaTime * 0.1f;

            if (cell.membraneIntegrity <= 0)
            {
                cell.state = CellState.Dead;
            }
        }
    }

    private void EmitSignal(SignalType type, float3 position, float radius, int sourceCell)
    {
        // Find an empty slot in the signals array or replace oldest
        int signalIndex = -1;
        float oldestDuration = 0f;

        for (int i = 0; i < activeSignals.Count; i++)
        {
            if (activeSignals.Count < maxActiveSignals)
            {
                signalIndex = activeSignals.Count;
                break;
            }
            else if (activeSignals[i].duration > oldestDuration)
            {
                oldestDuration = activeSignals[i].duration;
                signalIndex = i;
            }
        }

        // Create new signal
        SignalData signal = new SignalData
        {
            type = type,
            strength = 1.0f,
            duration = 0f,
            sourceCell = sourceCell,
            radius = radius
        };

        // Add to active signals
        if (signalIndex >= 0 && signalIndex < maxActiveSignals)
        {
            if (signalIndex >= activeSignals.Count)
            {
                activeSignals.Add(signal);
            }
            else
            {
                activeSignals[signalIndex] = signal;
            }
        }
    }

    private void ProcessSignals(float deltaTime)
    {
        // Update all active signals
        for (int i = 0; i < activeSignals.Count; i++)
        {
            SignalData signal = activeSignals[i];

            // Age the signal
            signal.duration += deltaTime;

            // Decay strength over time
            signal.strength *= (1.0f - (signalDecayRate * deltaTime));

            // Increase radius as signal propagates
            signal.radius += signalPropagationSpeed * deltaTime;

            // Remove signals that are too weak
            if (signal.strength < 0.05f)
            {
                activeSignals.RemoveAt(i);
                i--;
                continue;
            }

            // Update in list
            activeSignals[i] = signal;
        }

        // Apply signals to cells
        ApplySignalsToDetailedCells();
    }

    private void ApplySignalsToDetailedCells()
    {
        // For each detailed cell, check all signals
        for (int cellIndex = 0; cellIndex < detailedCells.Count; cellIndex++)
        {
            DetailedCellData cell = detailedCells[cellIndex];

            // Check each active signal
            for (int signalIndex = 0; signalIndex < activeSignals.Count; signalIndex++)
            {
                SignalData signal = activeSignals[signalIndex];

                // Skip if cell produced this signal
                if (signal.sourceCell == cellIndex)
                {
                    continue;
                }

                // Check if cell is within signal radius
                float distance = math.distance(cell.position, detailedCells[signal.sourceCell].position);
                if (distance <= signal.radius)
                {
                    // Calculate signal impact based on distance and receptor sensitivity
                    float receptorSensitivity = cell.receptors[(int)signal.type];
                    float signalImpact = signal.strength * receptorSensitivity * (1.0f - (distance / signal.radius));

                    // Apply signal effects based on type
                    ApplySignalEffect(ref cell, signal.type, signalImpact);
                }
            }

            // Save updated cell
            detailedCells[cellIndex] = cell;
        }
    }

    private void ApplySignalEffect(ref DetailedCellData cell, SignalType signalType, float impact)
    {
        switch (signalType)
        {
            case SignalType.Growth:
                // Growth signals increase division probability
                cell.divisionProbability += impact * 0.1f;
                cell.energy += impact * 0.05f; // Also slight energy boost
                break;

            case SignalType.Differentiation:
                // Increase specialization
                cell.specialization += impact * 0.05f;
                cell.specialization = Mathf.Min(cell.specialization, 1.0f);

                // Highly specialized cells move less
                if (cell.specialization > 0.8f)
                {
                    cell.motility *= (1.0f - (impact * 0.1f));
                }
                break;

            case SignalType.Maintenance:
                // Repair and maintenance
                cell.membraneIntegrity += impact * 0.1f;
                cell.membraneIntegrity = Mathf.Min(cell.membraneIntegrity, 1.0f);
                break;

            case SignalType.Stress:
                // Stress signals reduce function
                cell.energy -= impact * 0.05f;

                // Strong stress can change cell state
                if (impact > 0.5f && cell.state == CellState.Alive)
                {
                    cell.state = CellState.Stressed;
                }
                break;

            case SignalType.Apoptosis:
                // Death signals trigger apoptosis
                if (impact > 0.7f && cell.state != CellState.Dead)
                {
                    cell.state = CellState.Dying;
                }
                break;
        }
    }

    void OnDestroy()
    {
        // Clean up native arrays
        foreach (var cell in detailedCells)
        {
            if (cell.receptors.IsCreated) cell.receptors.Dispose();
            if (cell.signalProduction.IsCreated) cell.signalProduction.Dispose();
        }

        if (signalArray.IsCreated) signalArray.Dispose();
    }
}