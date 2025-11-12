using UnityEngine;

[DisallowMultipleComponent]
public class StrokeAttachToLift : MonoBehaviour
{
    int liftLayer = -1;
    bool attached = false;

    // Hàm Init — bắt buộc phải có, đúng tên, đúng kiểu public
    public void Init(string liftLayerName)
    {
        liftLayer = LayerMask.NameToLayer(liftLayerName);
        if (liftLayer < 0)
            Debug.LogWarning($"[StrokeAttachToLift] Layer '{liftLayerName}' chưa tồn tại.");
    }

    void TryAttach(Transform other)
    {
        if (attached || liftLayer < 0 || other == null) return;

        if (other.gameObject.layer == liftLayer)
        {
            transform.SetParent(other, true);
            attached = true;
        }
    }

    void OnCollisionEnter2D(Collision2D c) => TryAttach(c.transform);
    void OnTriggerEnter2D(Collider2D c) => TryAttach(c.transform);
}
