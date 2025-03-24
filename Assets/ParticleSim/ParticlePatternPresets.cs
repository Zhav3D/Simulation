using UnityEngine;

public static class ParticlePatternPresets
{
    // Recommended counts for each pattern type
    public static int GetRecommendedCounts(InteractionMatrixGenerator.PatternType patternType)
    {
        switch (patternType)
        {
            case InteractionMatrixGenerator.PatternType.Random:
                return 5;

            case InteractionMatrixGenerator.PatternType.Clusters:
                return 6;

            case InteractionMatrixGenerator.PatternType.Chains:
                return 6;

            case InteractionMatrixGenerator.PatternType.PredatorPrey:
                return 5;

            case InteractionMatrixGenerator.PatternType.Crystalline:
                return 8;

            case InteractionMatrixGenerator.PatternType.Flocking:
                return 7;

            case InteractionMatrixGenerator.PatternType.Lenia:
                return 10;

            case InteractionMatrixGenerator.PatternType.Segregation:
                return 6;

            default:
                return 5;
        }
    }

    // Apply recommended simulation settings based on pattern type
    public static void ApplyRecommendedSettings(OptimizedParticleSimulation simulation, InteractionMatrixGenerator.PatternType patternType)
    {
        switch (patternType)
        {
            case InteractionMatrixGenerator.PatternType.Random:
                ApplyRandomSettings(simulation);
                break;

            case InteractionMatrixGenerator.PatternType.Clusters:
                ApplyClusterSettings(simulation);
                break;

            case InteractionMatrixGenerator.PatternType.Chains:
                ApplyChainSettings(simulation);
                break;

            case InteractionMatrixGenerator.PatternType.PredatorPrey:
                ApplyPredatorPreySettings(simulation);
                break;

            case InteractionMatrixGenerator.PatternType.Crystalline:
                ApplyCrystallineSettings(simulation);
                break;

            case InteractionMatrixGenerator.PatternType.Flocking:
                ApplyFlockingSettings(simulation);
                break;

            case InteractionMatrixGenerator.PatternType.Lenia:
                ApplyLeniaSettings(simulation);
                break;

            case InteractionMatrixGenerator.PatternType.Segregation:
                ApplySegregationSettings(simulation);
                break;
        }
    }

    private static void ApplyRandomSettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 1.0f;
        simulation.collisionElasticity = 0.5f;
        simulation.dampening = 0.97f;
        simulation.interactionStrength = 1.0f;
        simulation.minDistance = 0.5f;
        simulation.bounceForce = 0.8f;
        simulation.maxForce = 100f;
        simulation.maxVelocity = 20f;
        simulation.interactionRadius = 10f;
    }

    private static void ApplyClusterSettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 1.0f;
        simulation.collisionElasticity = 0.3f;
        simulation.dampening = 0.95f;
        simulation.interactionStrength = 1.5f;
        simulation.minDistance = 0.6f;
        simulation.bounceForce = 0.7f;
        simulation.maxForce = 80f;
        simulation.maxVelocity = 15f;
        simulation.interactionRadius = 8f;
    }

    private static void ApplyChainSettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 0.8f;
        simulation.collisionElasticity = 0.7f;
        simulation.dampening = 0.96f;
        simulation.interactionStrength = 1.2f;
        simulation.minDistance = 0.4f;
        simulation.bounceForce = 0.9f;
        simulation.maxForce = 70f;
        simulation.maxVelocity = 12f;
        simulation.interactionRadius = 6f;
    }

    private static void ApplyPredatorPreySettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 1.5f;
        simulation.collisionElasticity = 0.6f;
        simulation.dampening = 0.98f;
        simulation.interactionStrength = 1.8f;
        simulation.minDistance = 0.5f;
        simulation.bounceForce = 0.8f;
        simulation.maxForce = 120f;
        simulation.maxVelocity = 25f;
        simulation.interactionRadius = 12f;
    }

    private static void ApplyCrystallineSettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 0.7f;
        simulation.collisionElasticity = 0.2f;
        simulation.dampening = 0.9f;
        simulation.interactionStrength = 2.0f;
        simulation.minDistance = 0.7f;
        simulation.bounceForce = 0.6f;
        simulation.maxForce = 60f;
        simulation.maxVelocity = 8f;
        simulation.interactionRadius = 5f;
    }

    private static void ApplyFlockingSettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 1.2f;
        simulation.collisionElasticity = 0.4f;
        simulation.dampening = 0.99f;
        simulation.interactionStrength = 1.0f;
        simulation.minDistance = 0.5f;
        simulation.bounceForce = 0.9f;
        simulation.maxForce = 50f;
        simulation.maxVelocity = 18f;
        simulation.interactionRadius = 10f;
    }

    private static void ApplyLeniaSettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 0.8f;
        simulation.collisionElasticity = 0.3f;
        simulation.dampening = 0.93f;
        simulation.interactionStrength = 1.6f;
        simulation.minDistance = 0.6f;
        simulation.bounceForce = 0.7f;
        simulation.maxForce = 90f;
        simulation.maxVelocity = 12f;
        simulation.interactionRadius = 8f;
    }

    private static void ApplySegregationSettings(OptimizedParticleSimulation simulation)
    {
        simulation.simulationSpeed = 1.0f;
        simulation.collisionElasticity = 0.5f;
        simulation.dampening = 0.95f;
        simulation.interactionStrength = 2.0f;
        simulation.minDistance = 0.6f;
        simulation.bounceForce = 0.8f;
        simulation.maxForce = 100f;
        simulation.maxVelocity = 15f;
        simulation.interactionRadius = 7f;
    }

    // Get description for each pattern type
    public static string GetPatternDescription(InteractionMatrixGenerator.PatternType patternType)
    {
        switch (patternType)
        {
            case InteractionMatrixGenerator.PatternType.Random:
                return "Creates a random interaction matrix with varying levels of attraction and repulsion. " +
                       "Produces chaotic, unpredictable behaviors.";

            case InteractionMatrixGenerator.PatternType.Clusters:
                return "Generates groups of particles that self-attract and repel other groups. " +
                       "Forms clear clusters of similar particles.";

            case InteractionMatrixGenerator.PatternType.Chains:
                return "Creates chain-like structures where particles attract their neighbors in a sequence. " +
                       "Forms long, linked structures like polymers or strings.";

            case InteractionMatrixGenerator.PatternType.PredatorPrey:
                return "Simulates predator-prey relationships with cyclic attraction and repulsion. " +
                       "Creates dynamic chasing and fleeing behaviors.";

            case InteractionMatrixGenerator.PatternType.Crystalline:
                return "Forms crystal-like structures with alternating attraction and repulsion. " +
                       "Creates ordered, lattice-like arrangements.";

            case InteractionMatrixGenerator.PatternType.Flocking:
                return "Creates flocking behaviors where similar particles align and move together. " +
                       "Similar to bird flocks or fish schools with coordinated movement.";

            case InteractionMatrixGenerator.PatternType.Lenia:
                return "Inspired by cellular automata, creates complex emergent patterns. " +
                       "Produces rich, organic-looking systems with potential for self-organization.";

            case InteractionMatrixGenerator.PatternType.Segregation:
                return "Models segregation patterns with strong in-group attraction and out-group repulsion. " +
                       "Demonstrates how simple preference rules can lead to large-scale segregation.";

            default:
                return "No description available for this pattern type.";
        }
    }
}