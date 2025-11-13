using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StrokeAttachToLift : MonoBehaviour
{
    readonly HashSet<int> targetLayers = new HashSet<int>();
    bool attached = false;

    public void Init(params string[] layerNames)
    {
        targetLayers.Clear();
        if (layerNames == null) return;
        foreach (var name in layerNames)
        {
            int id = LayerMask.NameToLayer(name);
            if (id >= 0) targetLayers.Add(id);
            else Debug.LogWarning($"[StrokeAttachToLift] Layer '{name}' chưa tồn tại!");
        }
    }

    // ---- phiên bản dùng Collision2D để lấy contact point ----
    void OnCollisionEnter2D(Collision2D c)
    {
        if (!enabled) return;
        TryAttachByCollision(c);
    }
    void OnTriggerEnter2D(Collider2D c)
    {
        if (!enabled) return;
        TryAttachByCollider(c);
    }

    void TryAttachByCollision(Collision2D c)
    {
        if (attached || c == null) return;
        var otherCol = c.collider;
        if (!otherCol || !targetLayers.Contains(otherCol.gameObject.layer)) return;

        var gear = otherCol.GetComponentInParent<Gears>();
        if (gear)
        {
            // --- Attach vào Gears: Hinge ở tâm + Distance ở điểm tiếp xúc ---
            var lineRB = GetComponent<Rigidbody2D>();
            var gearRB = gear.GetComponent<Rigidbody2D>();
            if (!lineRB || !gearRB) return;

            Vector2 gearCenter = gearRB.worldCenterOfMass;
            Vector2 contact    = c.GetContact(0).point;   // điểm chạm thực

            // Hinge: chốt tại tâm
            var hinge = gameObject.AddComponent<HingeJoint2D>();
            hinge.connectedBody = gearRB;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor          = lineRB.transform.InverseTransformPoint(gearCenter);
            hinge.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenter);
            hinge.enableCollision = false;

            // Distance: giữ điểm tiếp xúc nằm trên vành (kéo quay theo)
            var dist = gameObject.AddComponent<DistanceJoint2D>();
            dist.connectedBody = gearRB;
            dist.autoConfigureConnectedAnchor = false;
            dist.anchor          = lineRB.transform.InverseTransformPoint(contact);
            dist.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenter);
            dist.autoConfigureDistance = false;
            dist.distance = Vector2.Distance(contact, gearCenter);
            dist.maxDistanceOnly = true; // chỉ chặn kéo ra xa, không nén

            attached = true;
            // Debug.Log("[StrokeAttachToLift] Attached to Gears with Hinge+Distance");
            return;
        }

        // Không phải Gears (ví dụ Lift) -> set parent
        transform.SetParent(otherCol.transform, true);
        attached = true;
        // Debug.Log("[StrokeAttachToLift] Parented to Lift");
    }

    // fallback nếu gặp Trigger (không có Collision2D để lấy contact)
    void TryAttachByCollider(Collider2D other)
    {
        if (attached || other == null || !targetLayers.Contains(other.gameObject.layer)) return;

        var gear = other.GetComponentInParent<Gears>();
        if (gear)
        {
            var lineRB = GetComponent<Rigidbody2D>();
            var gearRB = gear.GetComponent<Rigidbody2D>();
            if (!lineRB || !gearRB) return;

            Vector2 gearCenter = gearRB.worldCenterOfMass;

            var hinge = gameObject.AddComponent<HingeJoint2D>();
            hinge.connectedBody = gearRB;
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor          = lineRB.transform.InverseTransformPoint(gearCenter);
            hinge.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenter);
            hinge.enableCollision = false;

            // Không có contact chính xác -> ước lượng điểm gần tâm nhất của một collider con
            var anyCol = GetComponentInChildren<Collider2D>();
            Vector2 contactGuess = anyCol ? anyCol.ClosestPoint(gearCenter) : (Vector2)transform.position;

            var dist = gameObject.AddComponent<DistanceJoint2D>();
            dist.connectedBody = gearRB;
            dist.autoConfigureConnectedAnchor = false;
            dist.anchor          = lineRB.transform.InverseTransformPoint(contactGuess);
            dist.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenter);
            dist.autoConfigureDistance = false;
            dist.distance = Vector2.Distance(contactGuess, gearCenter);
            dist.maxDistanceOnly = true;

            attached = true;
            return;
        }

        transform.SetParent(other.transform, true);
        attached = true;
    }
}
