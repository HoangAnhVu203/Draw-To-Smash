using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    [Tooltip("Vận tốc góc (độ/giây). Dương = cùng chiều kim đồng hồ, âm = ngược.")]
    public float angularSpeed = 120f;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    void FixedUpdate()
    {
        rb.MoveRotation(rb.rotation + angularSpeed * Time.fixedDeltaTime);
    }

    public void Reverse() => angularSpeed = -angularSpeed;
}
