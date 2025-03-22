using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
[ExecuteInEditMode]
public class TMP_TitleText : MonoBehaviour
{
    private void OnEnable()
    {
        if (TryGetComponent(out TextMeshProUGUI text))
        {
            text.SetText(transform.parent.gameObject.name);
        }
    }
}