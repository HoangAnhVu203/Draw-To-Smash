using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StrokeAttachToLift : MonoBehaviour
{
    // Lưu sẵn id của các layer hợp lệ
    readonly HashSet<int> targetLayers = new HashSet<int>();
    bool attached = false;

    // Nhận nhiều tên layer
    public void Init(params string[] layerNames)
    {
        targetLayers.Clear();
        if (layerNames == null) return;

        for (int i = 0; i < layerNames.Length; i++)
        {
            if (string.IsNullOrEmpty(layerNames[i])) continue;
            int id = LayerMask.NameToLayer(layerNames[i]);
            if (id >= 0) targetLayers.Add(id);
            else Debug.LogWarning($"[StrokeAttachToLift] Layer '{layerNames[i]}' chưa tồn tại.");
        }
    }

    void TryAttach(Transform other)
    {
        if (attached || other == null || targetLayers.Count == 0) return;

        int l = other.gameObject.layer;
        if (!targetLayers.Contains(l)) return;

        // Nếu là bánh răng -> tạo HingeJoint nối vào tâm bánh răng
        var gears = other.GetComponentInParent<Gears>();
        if (gears)
        {
            var lineRB = GetComponent<Rigidbody2D>();
            var gearRB = gears.GetComponent<Rigidbody2D>();
            if (lineRB && gearRB)
            {
                Vector2 center = gearRB.worldCenterOfMass;

                var hinge = gameObject.AddComponent<HingeJoint2D>();
                hinge.connectedBody = gearRB;
                hinge.autoConfigureConnectedAnchor = false;
                hinge.anchor          = lineRB.transform.InverseTransformPoint(center);
                hinge.connectedAnchor = gearRB.transform.InverseTransformPoint(center);
                hinge.enableCollision = false;

                attached = true; // đã “gắn” theo nghĩa vật lý
                return;          // KHÔNG SetParent với gears
            }
        }

        // Ngược lại (ví dụ Lift) -> SetParent để đi cùng platform
        transform.SetParent(other, true);
        attached = true;
    }


    void OnCollisionEnter2D(Collision2D c) => TryAttach(c.transform);
    void OnTriggerEnter2D(Collider2D c)    => TryAttach(c.transform);
}
