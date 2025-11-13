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

    [Header("Tự quay bánh răng")]
    public bool isAutoRotate = false;
    public float rotateSpeed = 180f; // độ/giây
    public bool rotateClockwise = true;

    Rigidbody2D gearRB;
    int lineLayer;
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

    void FixedUpdate()
    {
        if (isAutoRotate)
        {
            float dir = rotateClockwise ? -1f : 1f;
            gearRB.MoveRotation(gearRB.rotation + dir * rotateSpeed * Time.fixedDeltaTime);
        }
    }

    void OnCollisionEnter2D(Collision2D c) => TryPin(c);
    void OnCollisionStay2D(Collision2D c) => TryPin(c);

    void TryPin(Collision2D col)
    {
        if (lineLayer >= 0 && col.gameObject.layer != lineLayer) return;
        if (activeJoints.Count >= maxPins) return;

        var lineRB = col.rigidbody;
        if (!lineRB) return;
        if (AlreadyPinned(lineRB)) return;

        lineRB.gravityScale = Mathf.Max(1f, lineRB.gravityScale);
        lineRB.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        lineRB.interpolation = RigidbodyInterpolation2D.Interpolate;

        Vector2 gearCenterWorld = gearRB.worldCenterOfMass;
        Vector2 contactWorld = col.GetContact(0).point;

        bool passesCenter = false;
        var poly = lineRB.GetComponent<PolygonCollider2D>();
        if (poly)
            passesCenter = poly.OverlapPoint(gearCenterWorld);

        // Hinge tại tâm
        var hinge = lineRB.gameObject.AddComponent<HingeJoint2D>();
        hinge.connectedBody = gearRB;
        hinge.autoConfigureConnectedAnchor = false;
        hinge.anchor = lineRB.transform.InverseTransformPoint(gearCenterWorld);
        hinge.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenterWorld);
        hinge.useLimits = false;
        hinge.enableCollision = false;

        if (breakForce > 0f) hinge.breakForce = breakForce;
        if (breakTorque > 0f) hinge.breakTorque = breakTorque;

        activeJoints.Add(hinge);

        // Thêm dây giữ (nếu không qua tâm)
        if (!passesCenter)
        {
            var dist = lineRB.gameObject.AddComponent<DistanceJoint2D>();
            dist.connectedBody = gearRB;
            dist.autoConfigureConnectedAnchor = false;
            dist.anchor = lineRB.transform.InverseTransformPoint(contactWorld);
            dist.connectedAnchor = gearRB.transform.InverseTransformPoint(gearCenterWorld);
            dist.autoConfigureDistance = false;
            dist.distance = Vector2.Distance(contactWorld, gearCenterWorld);
            dist.maxDistanceOnly = true;

            if (breakForce > 0f) dist.breakForce = breakForce;
            if (breakTorque > 0f) dist.breakTorque = breakTorque;

            activeJoints.Add(dist);
        }

        lineRB.WakeUp();
        lineRB.AddForce(Vector2.down * downImpulse, ForceMode2D.Impulse);
    }

    bool AlreadyPinned(Rigidbody2D lineRB)
    {
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
        for (int i = 0; i < activeJoints.Count; i++)
            if (activeJoints[i]) Destroy(activeJoints[i]);
        activeJoints.Clear();
    }
}
