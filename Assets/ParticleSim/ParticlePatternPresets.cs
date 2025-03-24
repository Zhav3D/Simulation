using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static class containing recommended preset configurations for particle simulations.
/// Can be used with InteractionMatrixGenerator or manually.
/// </summary>
public static class ParticlePatternPresets
{
    /// <summary>
    /// Recommended particle counts for different patterns
    /// </summary>
    public static class RecommendedCounts
    {
        public static int Random = 5;
        public static int Clusters = 6;
        public static int Chains = 6;
        public static int PredatorPrey = 5;
        public static int Crystalline = 8;
        public static int Flocking = 7;
        public static int Lenia = 10;
        public static int Segregation = 6;
    }

    /// <summary>
    /// Recommend simulation settings for a pattern type
    /// </summary>
    public static void ApplyRecommendedSettings(OptimizedParticleSimulation simulation, InteractionMatrixGenerator.PatternType patternType)
    {
        switch (patternType)
        {
            case InteractionMatrixGenerator.PatternType.Random:
                simulation.simulationSpeed = 1.0f;
                simulation.interactionStrength = 1.0f;
                simulation.dampening = 0.95f;
                simulation.minDistance = 0.5f;
                simulation.bounceForce = 0.8f;
                simulation.maxForce = 100f;
                simulation.maxVelocity = 20f;
                simulation.interactionRadius = 10f;
                break;

            case InteractionMatrixGenerator.PatternType.Clusters:
                simulation.simulationSpeed = 1.2f;
                simulation.interactionStrength = 1.5f;
                simulation.dampening = 0.9f;
                simulation.minDistance = 0.6f;
                simulation.bounceForce = 0.8f;
                simulation.maxForce = 120f;
                simulation.maxVelocity = 15f;
                simulation.interactionRadius = 12f;
                break;

            case InteractionMatrixGenerator.PatternType.Chains:
                simulation.simulationSpeed = 1.1f;
                simulation.interactionStrength = 2.0f;
                simulation.dampening = 0.9f;
                simulation.minDistance = 0.4f;
                simulation.bounceForce = 0.8f;
                simulation.maxForce = 150f;
                simulation.maxVelocity = 18f;
                simulation.interactionRadius = 15f;
                break;

            case InteractionMatrixGenerator.PatternType.PredatorPrey:
                simulation.simulationSpeed = 1.5f;
                simulation.interactionStrength = 2.0f;
                simulation.dampening = 0.92f;
                simulation.minDistance = 0.5f;
                simulation.bounceForce = 0.9f;
                simulation.maxForce = 120f;
                simulation.maxVelocity = 25f;
                simulation.interactionRadius = 10f;
                break;

            case InteractionMatrixGenerator.PatternType.Crystalline:
                simulation.simulationSpeed = 0.8f;
                simulation.interactionStrength = 3.0f;
                simulation.dampening = 0.85f;
                simulation.minDistance = 0.6f;
                simulation.bounceForce = 0.5f;
                simulation.maxForce = 200f;
                simulation.maxVelocity = 15f;
                simulation.interactionRadius = 8f;
                break;

            case InteractionMatrixGenerator.PatternType.Flocking:
                simulation.simulationSpeed = 1.8f;
                simulation.interactionStrength = 1.2f;
                simulation.dampening = 0.98f; // Less damping for smoother movement
                simulation.minDistance = 0.4f;
                simulation.bounceForce = 0.9f;
                simulation.maxForce = 80f;
                simulation.maxVelocity = 20f;
                simulation.interactionRadius = 12f;
                break;

            case InteractionMatrixGenerator.PatternType.Lenia:
                simulation.simulationSpeed = 0.8f;
                simulation.interactionStrength = 1.5f;
                simulation.dampening = 0.9f;
                simulation.minDistance = 0.4f;
                simulation.bounceForce = 0.7f;
                simulation.maxForce = 100f;
                simulation.maxVelocity = 12f;
                simulation.interactionRadius = 15f;
                break;

            case InteractionMatrixGenerator.PatternType.Segregation:
                simulation.simulationSpeed = 1.4f;
                simulation.interactionStrength = 2.5f;
                simulation.dampening = 0.85f;
                simulation.minDistance = 0.6f;
                simulation.bounceForce = 0.7f;
                simulation.maxForce = 150f;
                simulation.maxVelocity = 18f;
                simulation.interactionRadius = 10f;
                break;
        }
    }

    /// <summary>
    /// Get descriptive information about each pattern type
    /// </summary>
    public static string GetPatternDescription(InteractionMatrixGenerator.PatternType patternType)
    {
        switch (patternType)
        {
            case InteractionMatrixGenerator.PatternType.Random:
                return "Random interactions with configurable symmetry and attraction bias. " +
                       "A good starting point but often produces chaotic results unless fine-tuned.";

            case InteractionMatrixGenerator.PatternType.Clusters:
                return "Particles are grouped where members of the same group attract each other " +
                       "while repelling other groups. Creates stable separated clusters.";

            case InteractionMatrixGenerator.PatternType.Chains:
                return "Creates chain-like structures by making certain pairs of particles " +
                       "attract each other in a specific sequence, while repelling others.";

            case InteractionMatrixGenerator.PatternType.PredatorPrey:
                return "Implements a circular food chain where each particle type is attracted " +
                       "to its 'prey' and repelled by its 'predator'. Creates dynamic chase patterns.";

            case InteractionMatrixGenerator.PatternType.Crystalline:
                return "Creates regular lattice-like arrangements through alternating attraction " +
                       "and repulsion based on 'distance' in type space.";

            case InteractionMatrixGenerator.PatternType.Flocking:
                return "Models bird flocking behaviors with particles of the same family attracting " +
                       "each other, plus designated 'leaders' that others follow.";

            case InteractionMatrixGenerator.PatternType.Lenia:
                return "Inspired by the Lenia cellular automaton, creates complex living system-like " +
                       "behaviors with local attraction and medium-range repulsion.";

            case InteractionMatrixGenerator.PatternType.Segregation:
                return "Based on Schelling's segregation model, creates strong group identity with " +
                       "optional 'bridge' particles that can connect different groups.";

            default:
                return "Unknown pattern type.";
        }
    }
}