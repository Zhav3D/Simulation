// Cluster visualization component
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class CellClusterController : MonoBehaviour
{
    public int cellCount;
    public CellType clusterType;

    // Visual elements
    private List<GameObject> visualCells = new List<GameObject>();

    public void Initialize(int count, CellType type)
    {
        cellCount = count;
        clusterType = type;

        // Create visual representation
        for (int i = 0; i < Mathf.Min(count, 100); i++)  // Limit visualization count
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.parent = transform;
            sphere.transform.localScale = Vector3.one * 0.5f;
            sphere.GetComponent<Renderer>().material.color = GetTypeColor(type);
            visualCells.Add(sphere);
        }
    }

    public void UpdateCluster(NativeArray<CellData> cellData)
    {
        // Find cells of this type and update visuals
        int visualIndex = 0;

        for (int i = 0; i < cellData.Length && visualIndex < visualCells.Count; i++)
        {
            if (cellData[i].type == clusterType)
            {
                // Update position
                visualCells[visualIndex].transform.position = new Vector3(
                    cellData[i].position.x,
                    cellData[i].position.y,
                    cellData[i].position.z
                );

                // Update scale based on energy
                float scale = 0.3f + cellData[i].energy * 0.5f;
                visualCells[visualIndex].transform.localScale = Vector3.one * scale;

                visualIndex++;
            }
        }
    }

    private Color GetTypeColor(CellType type)
    {
        switch (type)
        {
            case CellType.Epithelial: return new Color(0.8f, 0.2f, 0.2f);
            case CellType.Neuron: return new Color(0.2f, 0.8f, 0.2f);
            case CellType.Muscle: return new Color(0.2f, 0.2f, 0.8f);
            case CellType.Immune: return new Color(0.8f, 0.8f, 0.2f);
            case CellType.FatBody: return new Color(0.8f, 0.6f, 0.2f);
            default: return new Color(0.7f, 0.7f, 0.7f);
        }
    }
}