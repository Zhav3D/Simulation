
// Script for reflection points
using UnityEngine;

public class ReflectionPoint : MonoBehaviour
{
    public WavePropagation originalWave;
    public float reflectionCoefficient = 0.3f;
    public float incidentAngle = 0f;
    public Vector3 reflectedDirection = Vector3.forward;

    private float activationTime;
    private bool hasActivated = false;
    private ParticleSystem reflectionParticles;

    void Start()
    {
        // Set up activation based on when the wave would reach this point
        if (originalWave != null)
        {
            float distanceFromSource = Vector3.Distance(transform.position, originalWave.transform.position);
            activationTime = Time.time + (distanceFromSource / originalWave.speed);

            // Set up particles
            reflectionParticles = GetComponent<ParticleSystem>();
            if (reflectionParticles != null)
            {
                var main = reflectionParticles.main;
                main.startSize = 0.2f;
                main.startColor = originalWave.GetComponent<Renderer>().material.color;
                reflectionParticles.Stop();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (!hasActivated && Time.time >= activationTime)
        {
            ActivateReflection();
            hasActivated = true;
        }
    }

    void ActivateReflection()
    {
        // Show reflection effect
        if (reflectionParticles != null)
        {
            reflectionParticles.Play();
        }

        // Calculate reflection power
        float reflectionPower = originalWave.signalPower * reflectionCoefficient;

        // Adjust reflection coefficient based on incident angle (grazing angles reflect better)
        float angleAdjustment = Mathf.Clamp01(incidentAngle / 90.0f); // 0-1 range
        reflectionPower *= (1f - angleAdjustment * 0.5f); // Less loss for shallow angles

        // Create a visual pulse in the reflected direction
        CreateReflectedPulse(reflectionPower);
    }

    void CreateReflectedPulse(float power)
    {
        // This could create another expanding sphere or a directional pulse
        // For simplicity, we'll just use the particle effect
    }
}