using System.Collections.Generic;
using UnityEngine;

public class Gears : MonoBehaviour
{
    [Header("Thiết lập ghim (pin)")]
    [Tooltip("Tên Layer của nét vẽ")]
    public string lineLayerName = "Line";

    [Tooltip("Số điểm ghim tối đa mà bánh răng cho phép")]
    public int maxPins = 1;

    [Tooltip("Lực đứt của ghim (<=0 nghĩa là không đứt)")]
    public float breakForce = 0f;

    [Tooltip("Mô-men đứt (<=0 nghĩa là không đứt)")]
    public float breakTorque = 0f;

    [Tooltip("Neo vào tâm bánh răng hay đúng điểm chạm?")]
    public bool pinAtContactPoint = true;

    [Tooltip("Bật debug điểm ghim bằng gizmo")]
    public bool drawGizmos = false;

    Rigidbody2D gearRB;
    int lineLayer;
    readonly List<FixedJoint2D> activePins = new();

    void Awake()
    {
        gearRB = GetComponent<Rigidbody2D>();
        lineLayer = LayerMask.NameToLayer(lineLayerName);
        if (lineLayer < 0)
        {
            Debug.LogWarning($"[Gears] Layer '{lineLayerName}' chưa tồn tại. Hãy tạo Layer này và gán cho nét vẽ.");
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        TryPin(col);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        // đề phòng xô lệch frame đầu không ghim kịp
        TryPin(col);
    }

    void TryPin(Collision2D col)
    {
        if (lineLayer >= 0 && col.gameObject.layer != lineLayer) return;
        if (activePins.Count >= maxPins) return;

        var lineRB = col.rigidbody;
        if (!lineRB) return;

        // Tránh tạo trùng
        if (HasPinToThisGear(lineRB)) return;

        // Tạo joint
        var joint = lineRB.gameObject.AddComponent<FixedJoint2D>();
        joint.connectedBody = gearRB;
        joint.autoConfigureConnectedAnchor = false;

        // ✅ Neo anchor cho chuẩn, không bị lệch:
        Vector2 worldAnchor;
        if (pinAtContactPoint)
            worldAnchor = col.GetContact(0).point; // dùng điểm va chạm thực tế
        else
            worldAnchor = gearRB.worldCenterOfMass; // neo vào tâm bánh răng

        // anchor của line (local)
        joint.anchor = lineRB.transform.InverseTransformPoint(worldAnchor);
        // anchor của gear (local)
        joint.connectedAnchor = gearRB.transform.InverseTransformPoint(worldAnchor);

        // thêm tùy chọn lực đứt
        if (breakForce > 0f) joint.breakForce = breakForce;
        if (breakTorque > 0f) joint.breakTorque = breakTorque;

        activePins.Add(joint);
    }


    bool HasPinToThisGear(Rigidbody2D lineRB)
    {
        var pins = lineRB.GetComponents<FixedJoint2D>();
        foreach (var p in pins)
        {
            if (p && p.connectedBody == gearRB) return true;
        }
        return false;
    }

    void OnJointBreak2D(Joint2D broken)
    {
        activePins.Remove(broken as FixedJoint2D);
    }

    void OnDisable()
    {
        // dọn các joint còn lưu lại
        for (int i = 0; i < activePins.Count; i++)
        {
            if (activePins[i]) Destroy(activePins[i]);
        }
        activePins.Clear();
    }

    // Vẽ gizmo xem tâm & pin
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }
}
