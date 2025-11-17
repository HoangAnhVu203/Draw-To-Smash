using UnityEngine;

public class InputHintCancel : MonoBehaviour
{
    void Update()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
        {
            if (HintSystem.Instance != null)
                HintSystem.Instance.HideHint();
        }

        // Trường hợp kéo tay trên mobile
        if (Input.touchCount > 0)
        {
            if (HintSystem.Instance != null)
                HintSystem.Instance.HideHint();
        }
    }
}
