#if UNITY_EDITOR
// Custom editor for InteractionMatrixGenerator
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InteractionMatrixGenerator))]
public class InteractionMatrixGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        InteractionMatrixGenerator generator = (InteractionMatrixGenerator)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate New Matrix"))
        {
            generator.GenerateMatrix();
            EditorUtility.SetDirty(generator.gameObject);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate All Matrix Types"))
        {
            foreach (InteractionMatrixGenerator.PatternType patternType in
                     System.Enum.GetValues(typeof(InteractionMatrixGenerator.PatternType)))
            {
                generator.patternType = patternType;
                generator.GenerateMatrix();
                Debug.Log("Generated matrix type: " + patternType.ToString());
                EditorUtility.SetDirty(generator.gameObject);

                // OPTIONAL: Could take a screenshot or record data for each type
            }
        }

        // Add description of the current pattern type
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            ParticlePatternPresets.GetPatternDescription(generator.patternType),
            MessageType.Info
        );

        // Show recommended particle count
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Particle Information:", EditorStyles.boldLabel);

        if (generator.generateParticleTypes)
        {
            int baseParticleCount = ParticlePatternPresets.GetRecommendedCounts(generator.patternType);
            int totalParticles = Mathf.RoundToInt(baseParticleCount * generator.particleSpawnMultiplier * 50);

            EditorGUILayout.LabelField($"Particle Types: {baseParticleCount}");
            EditorGUILayout.LabelField($"Total Particles: ~{totalParticles} (with {generator.particleSpawnMultiplier}x multiplier)");
            EditorGUILayout.LabelField($"Particle Size: {generator.particleRadiusMultiplier}x default");
        }
        else
        {
            EditorGUILayout.LabelField("Enable 'Generate Particle Types' to see particle stats");
        }
    }
}
#endif