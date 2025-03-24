using UnityEngine;

public class BondRenderer : MonoBehaviour
{
    private AtomParticle atomA;
    private AtomParticle atomB;
    private LineRenderer lineRenderer;

    public void Initialize(AtomParticle a, AtomParticle b, Material material, float thickness)
    {
        atomA = a;
        atomB = b;

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = thickness;
        lineRenderer.endWidth = thickness;
        lineRenderer.material = material;
        lineRenderer.positionCount = 2;

        // Update positions
        UpdatePositions();
    }

    void Update()
    {
        // Update bond positions
        if (atomA != null && atomB != null)
        {
            UpdatePositions();
        }
        else
        {
            // Destroy if atoms are gone
            Destroy(gameObject);
        }
    }

    private void UpdatePositions()
    {
        lineRenderer.SetPosition(0, atomA.transform.position);
        lineRenderer.SetPosition(1, atomB.transform.position);
    }
}