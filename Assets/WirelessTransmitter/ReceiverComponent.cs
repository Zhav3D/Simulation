// Receiver component to handle signal reception visualization
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Receiver component to handle signal reception visualization
public class ReceiverComponent : MonoBehaviour
{
    [Header("Receiver Visualization")]
    public GameObject signalIndicatorObject;
    public float signalUpdateRate = 0.2f;
    public ParticleSystem receiveEffectParticles;
    public AudioSource receiveAudio;
    public Color connectedColor = Color.green;
    public Color disconnectedColor = Color.red;

    private float currentSignalStrength = float.MinValue;
    private float currentSignalQuality = 0f;
    private float lastUpdateTime = 0f;
    private Material indicatorMaterial;
    private List<float> recentSignalStrengths = new List<float>();

    void Start()
    {
        // Set up indicator if exists
        if (signalIndicatorObject != null)
        {
            Renderer renderer = signalIndicatorObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                indicatorMaterial = renderer.material;
                indicatorMaterial.color = disconnectedColor;
            }
        }

        // Initialize particles if exists
        if (receiveEffectParticles == null)
        {
            // Try to find or create particles
            receiveEffectParticles = GetComponentInChildren<ParticleSystem>();

            if (receiveEffectParticles == null && signalIndicatorObject != null)
            {
                // Create particle system
                GameObject particleObj = new GameObject("ReceiveParticles");
                particleObj.transform.parent = signalIndicatorObject.transform;
                particleObj.transform.localPosition = Vector3.zero;

                receiveEffectParticles = particleObj.AddComponent<ParticleSystem>();

                // Configure particles
                var main = receiveEffectParticles.main;
                main.startSize = 0.1f;
                main.startColor = connectedColor;
                main.duration = 0.5f;
                main.loop = false;

                var emission = receiveEffectParticles.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

                var shape = receiveEffectParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.2f;

                receiveEffectParticles.Stop();
            }
        }
    }

    public void UpdateSignalStrength(float signalStrength, float sensitivityThreshold)
    {
        currentSignalStrength = signalStrength;

        // Add to recent measurements for smoothing
        recentSignalStrengths.Add(signalStrength);
        if (recentSignalStrengths.Count > 10)
            recentSignalStrengths.RemoveAt(0);

        // Calculate signal quality normalized 0-1
        currentSignalQuality = Mathf.InverseLerp(sensitivityThreshold - 20f, -40f, signalStrength);

        // Update visualization at regular intervals
        if (Time.time - lastUpdateTime >= signalUpdateRate)
        {
            UpdateVisualIndicator();
            lastUpdateTime = Time.time;
        }
    }

    public void OnInterference(float interferencePower)
    {
        // Visual effect when interference affects this receiver
        if (receiveEffectParticles != null)
        {
            // Configure particles to show interference
            var main = receiveEffectParticles.main;

            // Scale effect based on interference power
            float normalizedPower = Mathf.InverseLerp(-90f, -40f, interferencePower);
            main.startSize = 0.1f + normalizedPower * 0.2f;

            // Use orange/red color for interference
            main.startColor = Color.Lerp(Color.yellow, Color.red, normalizedPower);

            // Emit a burst of particles
            receiveEffectParticles.Emit(5);
        }

        // Flash the indicator to show interference
        if (signalIndicatorObject != null && indicatorMaterial != null)
        {
            // Start a brief flash coroutine
            StartCoroutine(FlashInterference());
        }

        // Audio effect if available
        if (receiveAudio != null)
        {
            // Play a brief static/noise sound
            receiveAudio.pitch = 1.5f;
            receiveAudio.volume = Mathf.InverseLerp(-90f, -40f, interferencePower) * 0.3f;

            if (!receiveAudio.isPlaying)
                receiveAudio.Play();
        }
    }

    IEnumerator FlashInterference()
    {
        // Store original color
        Color originalColor = indicatorMaterial.color;

        // Flash orange/red
        indicatorMaterial.color = Color.red;
        if (indicatorMaterial.HasProperty("_EmissionColor"))
        {
            indicatorMaterial.SetColor("_EmissionColor", Color.red * 2f);
        }

        // Wait briefly
        yield return new WaitForSeconds(0.1f);

        // Restore original color
        indicatorMaterial.color = originalColor;
        if (indicatorMaterial.HasProperty("_EmissionColor"))
        {
            Color emissionColor = originalColor * currentSignalQuality * 2f;
            indicatorMaterial.SetColor("_EmissionColor", emissionColor);
        }
    }

    void UpdateVisualIndicator()
    {
        if (indicatorMaterial != null)
        {
            // Set color based on signal quality
            Color indicatorColor = Color.Lerp(disconnectedColor, connectedColor, currentSignalQuality);
            indicatorMaterial.color = indicatorColor;

            // Update emission intensity
            if (indicatorMaterial.HasProperty("_EmissionColor"))
            {
                indicatorMaterial.SetColor("_EmissionColor", indicatorColor * currentSignalQuality * 2f);
                indicatorMaterial.EnableKeyword("_EMISSION");
            }

            // Update scale based on signal strength
            if (signalIndicatorObject != null)
            {
                float baseScale = 0.2f;
                float pulseScale = baseScale * (1f + 0.2f * Mathf.Sin(Time.time * 3f) * currentSignalQuality);
                signalIndicatorObject.transform.localScale = new Vector3(pulseScale, pulseScale, pulseScale);
            }
        }
    }

    public void OnWaveContact(float wavePower, float waveFrequency)
    {
        // Visual effect when a wave hits this receiver
        if (receiveEffectParticles != null && !receiveEffectParticles.isPlaying)
        {
            // Scale effect based on power
            var main = receiveEffectParticles.main;
            float normalizedPower = Mathf.InverseLerp(-90f, -40f, wavePower);
            main.startSize = 0.1f + normalizedPower * 0.3f;

            // Color based on frequency
            float normalizedFreq = Mathf.InverseLerp(2400f, 5000f, waveFrequency);
            main.startColor = Color.Lerp(new Color(0, 0.8f, 1f), new Color(0.8f, 0, 1f), normalizedFreq);

            receiveEffectParticles.Play();
        }

        // Audio effect if available
        if (receiveAudio != null)
        {
            float volume = Mathf.InverseLerp(-90f, -40f, wavePower) * 0.5f;
            receiveAudio.volume = volume;
            receiveAudio.pitch = Mathf.Lerp(0.8f, 1.2f, Mathf.InverseLerp(2400f, 5000f, waveFrequency));
            receiveAudio.Play();
        }
    }
}