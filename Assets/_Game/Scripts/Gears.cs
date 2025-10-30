using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Gears : MonoBehaviour
{
    [Header("Layer của nét vẽ (Line)")]
    public string lineLayerName = "Line";

    [Header("Tối đa số nét ghim được vào 1 bánh răng")]
    public int maxPins = 1;

    [Header("Giới hạn đứt (<=0 = không đứt)")]
    public float breakForce = 0f;
    public float breakTorque = 0f;

    [Header("Nudge nhẹ để tránh kẹt frame đầu")]
    public float downImpulse = 0.02f;

    Rigidbody2D gearRB;
    int lineLayer;
    // lưu các joint được tạo ra để dọn khi disable
    readonly List<Joint2D> activeJoints = new();

    void Awake()
    {
        gearRB = GetComponent<Rigidbody2D>();
        lineLayer = LayerMask.NameToLayer(lineLayerName);
        if (lineLayer < 0)
            Debug.LogWarning($"[Gears] Layer '{lineLayerName}' chưa tồn tại!");

        gearRB.bodyType = RigidbodyType2D.Dynamic;
        gearRB.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezePositionY;
        gearRB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        gearRB.interpolation = RigidbodyInterpolation2D.Interpolate;
        gearRB.angularDrag = 0.05f;
    }

    void OnCollisionEnter2D(Collision2D c) => TryPin(c);
    void OnCollisionStay2D (Collision2D c) => TryPin(c);

    void TryPin(Collision2D col)
    {
        if (lineLayer >= 0 && col.gameObject.layer != lineLayer) return;
        if (activeJoints.Count >= maxPins) return;

        var lineRB = col.rigidbody;
        if (!lineRB) return;
        if (AlreadyPinned(lineRB)) return;

        // đảm bảo nét vẽ có thông số rơi ổn định
        lineRB.gravityScale = Mathf.Max(1f, lineRB.gravityScale);
        lineRB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        lineRB.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Tính các điểm quan trọng
        Vector2 gearCenterWorld = gearRB.worldCenterOfMass;
        Vector2 contactWorld     = col.GetContact(0).point;

        // Kiểm tra "đi qua tâm" bằng PolygonCollider2D của nét vẽ
        bool passesCenter = false;
        var poly = lineRB.GetComponent<PolygonCollider2D>();
        if (poly)
            passesCenter = poly.OverlapPoint(gearCenterWorld);

        // --- Luôn tạo Hinge tại TÂM bánh răng để trục quay đúng tâm ---
        var hinge = lineRB.gameObject.AddComponent<HingeJoint2D>();
        hinge.connectedBody = gearRB;
        hinge.autoConfigureConnectedAnchor = false;

        // Hinge phải dùng CÙNG MỘT điểm thế giới cho cả 2 anchor
        // => đặt anchor của line = tâm, connectedAnchor của gear = tâm
        hinge.anchor          = lineRB.transform.InverseTransformPoint(gearCenterWorld);
        hinge.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenterWorld);

        if (breakForce  > 0f) hinge.breakForce  = breakForce;
        if (breakTorque > 0f) hinge.breakTorque = breakTorque;
        hinge.useLimits = false;
        hinge.enableCollision = false;

        activeJoints.Add(hinge);

        // --- Nhánh B: nếu KHÔNG đi qua tâm, thêm dây "giữ" điểm tiếp xúc ---
        if (!passesCenter)
        {
            var dist = lineRB.gameObject.AddComponent<DistanceJoint2D>();
            dist.connectedBody = gearRB;
            dist.autoConfigureConnectedAnchor = false;

            // Giữ khoảng cách từ điểm chạm đến tâm bánh răng (trên vành)
            dist.anchor          = lineRB.transform.InverseTransformPoint(contactWorld);
            dist.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenterWorld);
            dist.autoConfigureDistance = false;
            dist.distance = Vector2.Distance(contactWorld, gearCenterWorld);

            // Để tránh "giật", chỉ giới hạn MAX distance (không kéo nén)
            dist.maxDistanceOnly = true;

            if (breakForce  > 0f) dist.breakForce  = breakForce;
            if (breakTorque > 0f) dist.breakTorque = breakTorque;

            activeJoints.Add(dist);
        }

        // Nudge nhẹ để thoát kẹt ban đầu
        lineRB.WakeUp();
        lineRB.AddForce(Vector2.down * downImpulse, ForceMode2D.Impulse);
    }

    bool AlreadyPinned(Rigidbody2D lineRB)
    {
        // nếu line đã có joint nối tới gear này thì bỏ qua
        var joints = lineRB.GetComponents<Joint2D>();
        foreach (var j in joints)
            if (j && j.connectedBody == gearRB)
                return true;
        return false;
    }

    void OnJointBreak2D(Joint2D broken)
    {
        activeJoints.Remove(broken);
    }

    void OnDisable()
    {
        // dọn toàn bộ joint đã tạo
        for (int i = 0; i < activeJoints.Count; i++)
            if (activeJoints[i]) Destroy(activeJoints[i]);
        activeJoints.Clear();
    }
}
