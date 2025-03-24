using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InteractionMatrixVisualizer : MonoBehaviour
{
    public OptimizedParticleSimulation simulation;
    public InteractionMatrixGenerator matrixGenerator;
    public RectTransform matrixContainer;
    public GameObject cellPrefab;
    public GameObject rowLabelPrefab;
    public GameObject columnLabelPrefab;
    public GameObject cornerLabelPrefab;

    [SerializeField] private Color attractionColor = Color.green;
    [SerializeField] private Color repulsionColor = Color.red;
    [SerializeField] private Color neutralColor = Color.gray;

    private List<GameObject> matrixElements = new List<GameObject>();

    private void Start()
    {
        if (simulation == null)
        {
            simulation = FindObjectOfType<OptimizedParticleSimulation>();
        }

        if (matrixGenerator == null)
        {
            matrixGenerator = FindObjectOfType<InteractionMatrixGenerator>();
        }

        // Initial visualization
        Visualize();
    }

    public void Visualize()
    {
        ClearMatrix();

        if (simulation == null || matrixContainer == null)
        {
            Debug.LogError("Matrix visualization components not set!");
            return;
        }

        float[,] matrix = matrixGenerator.VisualizeMatrix();
        int typeCount = simulation.particleTypes.Count;

        if (typeCount == 0) return;

        // Calculate cell size based on container
        float cellSize = Mathf.Min(
            matrixContainer.rect.width / (typeCount + 1),
            matrixContainer.rect.height / (typeCount + 1)
        );

        // Add corner label (empty cell)
        var cornerLabel = Instantiate(cornerLabelPrefab, matrixContainer);
        cornerLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(cellSize, cellSize);
        cornerLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
        matrixElements.Add(cornerLabel);

        // Add column labels (top row)
        for (int i = 0; i < typeCount; i++)
        {
            var columnLabel = Instantiate(columnLabelPrefab, matrixContainer);
            columnLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(cellSize, cellSize);
            columnLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2((i + 1) * cellSize, 0);

            // Set column label text
            var labelText = columnLabel.GetComponentInChildren<Text>();
            if (labelText != null && i < simulation.particleTypes.Count)
            {
                labelText.text = simulation.particleTypes[i].name;
            }

            matrixElements.Add(columnLabel);
        }

        // Create matrix cells with row labels
        for (int i = 0; i < typeCount; i++)
        {
            // Add row label
            var rowLabel = Instantiate(rowLabelPrefab, matrixContainer);
            rowLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(cellSize, cellSize);
            rowLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -(i + 1) * cellSize);

            // Set row label text
            var labelText = rowLabel.GetComponentInChildren<Text>();
            if (labelText != null && i < simulation.particleTypes.Count)
            {
                labelText.text = simulation.particleTypes[i].name;
            }

            matrixElements.Add(rowLabel);

            // Add cells for this row
            for (int j = 0; j < typeCount; j++)
            {
                var cell = Instantiate(cellPrefab, matrixContainer);
                cell.GetComponent<RectTransform>().sizeDelta = new Vector2(cellSize, cellSize);
                cell.GetComponent<RectTransform>().anchoredPosition = new Vector2((j + 1) * cellSize, -(i + 1) * cellSize);

                // Get attraction value from matrix
                float value = matrix[i, j];

                // Set cell color based on attraction value
                var cellImage = cell.GetComponent<Image>();
                if (cellImage != null)
                {
                    if (value > 0)
                    {
                        // Attraction (green)
                        cellImage.color = Color.Lerp(Color.white, attractionColor, value);
                    }
                    else if (value < 0)
                    {
                        // Repulsion (red)
                        cellImage.color = Color.Lerp(Color.white, repulsionColor, -value);
                    }
                    else
                    {
                        // Neutral (gray)
                        cellImage.color = neutralColor;
                    }
                }

                // Add value text
                var valueText = cell.GetComponentInChildren<Text>();
                if (valueText != null)
                {
                    valueText.text = value.ToString("F1");

                    // Set text color for better contrast
                    valueText.color = (Mathf.Abs(value) > 0.5f) ? Color.white : Color.black;
                }

                matrixElements.Add(cell);
            }
        }
    }

    private void ClearMatrix()
    {
        foreach (var element in matrixElements)
        {
            Destroy(element);
        }

        matrixElements.Clear();
    }
}