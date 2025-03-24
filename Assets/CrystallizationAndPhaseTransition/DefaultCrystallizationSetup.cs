using UnityEngine;
using System.Collections.Generic;
using C1;

[RequireComponent(typeof(CrystallizationSimulation))]
public class DefaultCrystallizationSetup : MonoBehaviour
{
    void Awake()
    {
        // Get the simulation component
        CrystallizationSimulation simulation = GetComponent<CrystallizationSimulation>();

        // Only configure if it exists
        if (simulation == null)
            return;

        ConfigureSimulation(simulation);
    }

    void ConfigureSimulation(CrystallizationSimulation simulation)
    {
        // Clear existing configurations
        simulation.atomTypes.Clear();
        simulation.molecularBonds.Clear();

        // 1. Configure atom types
        // Oxygen atom
        var oxygen = new CrystallizationSimulation.AtomType
        {
            name = "Oxygen",
            color = new Color(0.8f, 0.1f, 0.1f, 1.0f), // Red
            mass = 16f,
            radius = 0.65f,
            bondingStrength = 0.9f,
            spawnAmount = 60
        };

        // Hydrogen atom
        var hydrogen = new CrystallizationSimulation.AtomType
        {
            name = "Hydrogen",
            color = new Color(0.9f, 0.9f, 0.9f, 1.0f), // White
            mass = 1f,
            radius = 0.4f,
            bondingStrength = 0.7f,
            spawnAmount = 120
        };

        // Carbon atom
        var carbon = new CrystallizationSimulation.AtomType
        {
            name = "Carbon",
            color = new Color(0.2f, 0.2f, 0.2f, 1.0f), // Black
            mass = 12f,
            radius = 0.6f,
            bondingStrength = 1.2f,
            spawnAmount = 30
        };

        // Add atom types to simulation
        simulation.atomTypes.Add(oxygen);
        simulation.atomTypes.Add(hydrogen);
        simulation.atomTypes.Add(carbon);

        // 2. Configure molecular bonds
        // Hydrogen-Oxygen bond (water)
        var h2oBond = new CrystallizationSimulation.MolecularBond
        {
            typeIndexA = 1, // Hydrogen
            typeIndexB = 0, // Oxygen
            optimalDistance = 1.2f, // Distance between H and O in water
            bondStrength = 5.0f,     // Strong bond
            breakThreshold = 350.0f   // Water breaks apart at high temps
        };

        // Carbon-Carbon bond
        var ccBond = new CrystallizationSimulation.MolecularBond
        {
            typeIndexA = 2, // Carbon
            typeIndexB = 2, // Carbon
            optimalDistance = 1.5f,
            bondStrength = 8.0f,     // Very strong bond
            breakThreshold = 500.0f   // High temperature required to break C-C bonds
        };

        // Carbon-Hydrogen bond
        var chBond = new CrystallizationSimulation.MolecularBond
        {
            typeIndexA = 2, // Carbon
            typeIndexB = 1, // Hydrogen
            optimalDistance = 1.3f,
            bondStrength = 4.0f,
            breakThreshold = 400.0f
        };

        // Carbon-Oxygen bond
        var coBond = new CrystallizationSimulation.MolecularBond
        {
            typeIndexA = 2, // Carbon
            typeIndexB = 0, // Oxygen
            optimalDistance = 1.4f,
            bondStrength = 6.0f,
            breakThreshold = 450.0f
        };

        // Add bonds to simulation
        simulation.molecularBonds.Add(h2oBond);
        simulation.molecularBonds.Add(ccBond);
        simulation.molecularBonds.Add(chBond);
        simulation.molecularBonds.Add(coBond);

        // 3. Configure simulation parameters
        simulation.simulationBounds = new Vector3(100f, 100f, 100f);
        simulation.temperature = 100f; // Cold enough for crystals to form
        simulation.pressureMultiplier = 1.5f;
        simulation.dampening = 0.92f;
        simulation.interactionStrength = 1.5f;
        simulation.minDistance = 0.4f;
        simulation.bounceForce = 0.8f;
        simulation.maxForce = 120f;
        simulation.maxVelocity = 25f;
        simulation.interactionRadius = 3.5f;

        // Phase transition settings
        simulation.meltingPoint = 273.15f;
        simulation.boilingPoint = 373.15f;
        simulation.crystalNucleationThreshold = 0.7f;
        simulation.coolingRate = 0.2f;
        simulation.heatingRate = 0.2f;

        // Start with a body-centered cubic lattice
        simulation.initialLattice = CrystallizationSimulation.LatticeType.BodyCenteredCubic;
        simulation.latticeSpacing = 1.2f;

        // Spatial partitioning for better performance
        simulation.cellSize = 3.5f;
        simulation.useGridPartitioning = true;
        simulation.useJobSystem = true;

        // Visualization settings
        simulation.showBonds = true;
        simulation.bondThickness = 0.12f;
        simulation.colorByPhase = true;
        simulation.solidColor = new Color(0.2f, 0.2f, 0.8f, 1.0f);
        simulation.liquidColor = new Color(0.2f, 0.6f, 0.8f, 0.8f);
        simulation.gasColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        Debug.Log("Crystal simulation configured with default parameters");
    }

    // Optional: Add UI controls for runtime parameter adjustment
    void OnGUI()
    {
        CrystallizationSimulation simulation = GetComponent<CrystallizationSimulation>();
        if (simulation == null)
            return;

        // Simple UI for heating and cooling control
        GUILayout.BeginArea(new Rect(Screen.width - 210, 10, 200, 120));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Temperature Control");

        if (GUILayout.Button("Heat System"))
        {
            simulation.temperature += 20f;
        }

        if (GUILayout.Button("Cool System"))
        {
            simulation.temperature = Mathf.Max(0, simulation.temperature - 20f);
        }

        if (GUILayout.Button("Reset Temperature"))
        {
            simulation.temperature = 100f;
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}