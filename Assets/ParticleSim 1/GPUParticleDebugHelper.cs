using UnityEngine;

[ExecuteInEditMode]
public class GPUParticleDebugHelper : MonoBehaviour
{
    public GPUParticleSimulation particleSimulation;
    public bool debugMode = true;
    public bool renderDebugSpheres = true;
    public float debugSphereScale = 1.0f;

    // Debug visualization variables
    private Color[] typeColors;
    private int particleCount;
    private Vector3[] debugPositions;
    private float[] debugRadii;
    private int[] debugTypes;

    // CPU data for debugging
    private struct DebugParticleData
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 force;
        public int typeIndex;
        public float mass;
        public float radius;
        public float padding;
    }

    void OnEnable()
    {
        if (particleSimulation == null)
        {
            particleSimulation = GetComponent<GPUParticleSimulation>();
        }
    }

    void Update()
    {
        if (!debugMode || particleSimulation == null) return;

        // Cache type colors
        if (typeColors == null || typeColors.Length != particleSimulation.particleTypes.Count)
        {
            typeColors = new Color[particleSimulation.particleTypes.Count];
            for (int i = 0; i < particleSimulation.particleTypes.Count; i++)
            {
                typeColors[i] = particleSimulation.particleTypes[i].color;
            }
        }

        // Get particle count
        particleCount = particleSimulation.GetParticleCount();

        // Initialize debug arrays if needed
        if (debugPositions == null || debugPositions.Length != particleCount)
        {
            debugPositions = new Vector3[particleCount];
            debugRadii = new float[particleCount];
            debugTypes = new int[particleCount];
        }

        // Request debug data from GPU
        if (Application.isPlaying)
        {
            ReadParticleDataFromGPU();
        }
    }

    void ReadParticleDataFromGPU()
    {
        // Only attempt to read if the particle buffer exists
        var field = particleSimulation.GetType().GetField("particleBuffer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            var particleBuffer = field.GetValue(particleSimulation) as ComputeBuffer;
            if (particleBuffer != null && particleBuffer.IsValid())
            {
                // Create array to receive data
                DebugParticleData[] particleData = new DebugParticleData[particleCount];
                particleBuffer.GetData(particleData);

                // Copy data to debug arrays
                for (int i = 0; i < particleCount; i++)
                {
                    debugPositions[i] = particleData[i].position;
                    debugRadii[i] = particleData[i].radius;
                    debugTypes[i] = particleData[i].typeIndex;
                }

                Debug.Log($"Debug: Read {particleCount} particles from GPU");
            }
            else
            {
                Debug.LogWarning("Debug: Particle buffer is not valid");
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!debugMode || debugPositions == null || !renderDebugSpheres) return;

        // Draw particles as gizmos
        for (int i = 0; i < particleCount; i++)
        {
            if (i >= debugPositions.Length) break;

            int typeIndex = debugTypes[i];
            if (typeIndex >= 0 && typeIndex < typeColors.Length)
            {
                Gizmos.color = typeColors[typeIndex];
                Gizmos.DrawWireSphere(debugPositions[i], debugRadii[i] * debugSphereScale);
            }
        }
    }
}