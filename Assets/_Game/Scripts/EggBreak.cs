using System.Collections;
using UnityEngine;

public class EggBreak : MonoBehaviour
{
    [Header("Va chạm")]
    public float breakVelocity = 3f;
    public string instantBreakLayer = "Line";

    [Header("Pool")]
    public Pool pool;              // Kéo PoolManager vào
    public string fragKey = "EggFrag";
    public int fragmentCount = 8;
    public float fragmentScale = 0.005f;

    [Header("Vật lý mảnh")]
    public bool inheritEggVelocity = true;
    public float randomAngularVel = 10f;
    public float fragmentsLife = 2f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    bool isBroken = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>() ?? gameObject.AddComponent<CircleCollider2D>();
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (isBroken) return;

        if (LayerMask.NameToLayer(instantBreakLayer) == c.gameObject.layer ||
            c.relativeVelocity.magnitude >= breakVelocity)
        {
            Break();
        }
    }

    void Break()
    {
        if (isBroken) return;
        isBroken = true;

        // Ẩn trứng ngay
        sr.enabled = false;
        col.enabled = false;
        if (rb) rb.simulated = false;

        // ✅ Dùng PoolManager làm host chạy Coroutine (không bị inactive)
        pool.StartCoroutine(SpawnFragmentsSmooth());

        // Hủy vỏ trứng sau 0.1s
        Destroy(gameObject, 0.1f);
    }

    IEnumerator SpawnFragmentsSmooth()
    {
        const int batch = 4; // mỗi frame bật 4 mảnh (tránh spike)
        int count = 0;

        for (int i = 0; i < fragmentCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * 0.02f;
            var frag = pool.Get(fragKey,
                transform.position + (Vector3)offset,
                Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
            if (!frag) continue;

            frag.transform.localScale = Vector3.one * fragmentScale;

            var rbf = frag.GetComponent<Rigidbody2D>();
            if (rbf)
            {
                rbf.velocity = inheritEggVelocity && rb ? rb.velocity : Vector2.zero;
                rbf.angularVelocity = Random.Range(-randomAngularVel, randomAngularVel);
                rbf.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                rbf.interpolation = RigidbodyInterpolation2D.None;
            }

            // ✅ trả về pool sau vài giây
            pool.StartCoroutine(ReturnFragmentAfter(frag, fragmentsLife));

            count++;
            if (count % batch == 0)
                yield return null; // nhường 1 frame (tránh lag)
        }
    }

    IEnumerator ReturnFragmentAfter(GameObject frag, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (frag && pool)
            pool.Return(fragKey, frag);
    }
}
