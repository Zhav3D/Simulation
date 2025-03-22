// Script to handle wave propagation visualization
using System.Collections.Generic;
using UnityEngine;

public class WavePropagation : MonoBehaviour
{
    public float speed = 3.0f;
    public float maxDistance = 20.0f;
    public float signalPower;
    public float frequency;
    public GameObject transmitter;
    public List<GameObject> receivers;
    public WirelessSimulationController wirelessController;
    public GameObject reflectionPrefab;
    public float reflectionCoefficient = 0.3f;

    private float currentRadius = 0.1f;
    private float startTime;
    private Dictionary<GameObject, bool> receiverContactRecord = new Dictionary<GameObject, bool>();
    private List<GameObject> reflectionObjects = new List<GameObject>();

    void Start()
    {
        startTime = Time.time;
        transform.localScale = new Vector3(currentRadius, currentRadius, currentRadius);

        // Initialize receiver contact record
        if (receivers != null)
        {
            foreach (var receiver in receivers)
            {
                if (receiver != null)
                    receiverContactRecord[receiver] = false;
            }
        }

        // Generate reflection points when wave starts
        if (reflectionPrefab != null && reflectionCoefficient > 0)
        {
            GenerateReflectionPoints();
        }
    }

    void GenerateReflectionPoints()
    {
        // Find potential reflection surfaces (walls, floor, etc.)
        Collider[] obstacles = Physics.OverlapSphere(transmitter.transform.position, maxDistance);

        foreach (var obstacle in obstacles)
        {
            // Skip non-reflective objects
            if (obstacle.gameObject.layer != LayerMask.NameToLayer("Obstacles"))
                continue;

            // Find reflection points (simplified approach)
            Vector3 direction = (obstacle.transform.position - transmitter.transform.position).normalized;
            float distance = Vector3.Distance(transmitter.transform.position, obstacle.transform.position);

            if (distance < 2.0f || distance > maxDistance * 0.8f)
                continue; // Too close or too far

            // Create reflection point
            RaycastHit hit;
            if (Physics.Raycast(transmitter.transform.position, direction, out hit, maxDistance))
            {
                // Create reflection visualization
                GameObject reflection = Instantiate(reflectionPrefab, hit.point, Quaternion.identity);

                // Configure reflection
                ReflectionPoint reflectionScript = reflection.AddComponent<ReflectionPoint>();
                reflectionScript.originalWave = this;
                reflectionScript.reflectionCoefficient = reflectionCoefficient;
                reflectionScript.incidentAngle = Vector3.Angle(direction, hit.normal);
                reflectionScript.reflectedDirection = Vector3.Reflect(direction, hit.normal);

                // Store reflection for cleanup
                reflectionObjects.Add(reflection);

                // Limit number of reflections
                if (reflectionObjects.Count >= 3)
                    break;
            }
        }
    }

    void Update()
    {
        // Expand the wave
        currentRadius += speed * Time.deltaTime;
        transform.localScale = new Vector3(currentRadius, currentRadius, currentRadius);

        // Fade out based on distance
        float normalizedDistance = currentRadius / maxDistance;
        Color color = GetComponent<Renderer>().material.color;
        color.a = Mathf.Lerp(0.5f, 0.0f, normalizedDistance);
        GetComponent<Renderer>().material.color = color;

        // Check for receiver contacts
        CheckReceiverContacts();

        // Clean up when max distance is reached
        if (currentRadius >= maxDistance)
        {
            // Clean up reflections
            foreach (var reflection in reflectionObjects)
            {
                if (reflection != null)
                    Destroy(reflection);
            }

            Destroy(gameObject);
        }
    }

    void CheckReceiverContacts()
    {
        if (receivers == null || wirelessController == null)
            return;

        foreach (var receiver in receivers)
        {
            if (receiver == null || receiverContactRecord[receiver])
                continue;

            // Check if wave reached this receiver
            float distanceToReceiver = Vector3.Distance(transform.position, receiver.transform.position);

            if (distanceToReceiver <= currentRadius)
            {
                // Calculate actual power at this distance based on free space path loss
                float actualDistance = Vector3.Distance(transmitter.transform.position, receiver.transform.position);
                float distanceKm = actualDistance / 1000.0f;
                float pathLoss = 20 * Mathf.Log10(distanceKm) + 20 * Mathf.Log10(frequency) + 32.44f;
                float receivedPowerDBm = 10 * Mathf.Log10(signalPower) - pathLoss;

                // Notify controller that receiver got hit by wave
                wirelessController.NotifyReceiverOfWave(receiver, receivedPowerDBm, frequency);

                // Mark as contacted
                receiverContactRecord[receiver] = true;
            }
        }
    }

    public bool HasReachedMaxDistance()
    {
        return currentRadius >= maxDistance;
    }
}