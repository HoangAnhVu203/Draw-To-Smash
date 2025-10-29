using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EggBreak : MonoBehaviour
{
    [Header("Va chạm")]
    public float breakVelocity = 5f;
    public string instantBreakLayer = "Line";

    [Header("Pool (5 key – 5 mảnh + VFX)")]
    public Pool pool;
    public string[] fragKeys = { "EggFrag1", "EggFrag2", "EggFrag3", "EggFrag4", "EggFrag5" };
    public string VFXkey = "VFX";
    public float fragmentScale = 0.03f;

    [Header("Vật lý mảnh")]
    public bool inheritEggVelocity = true;
    public float randomAngularVel = 10f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    bool isBroken;

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>() ?? gameObject.AddComponent<CircleCollider2D>();
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (isBroken) return;

        // nếu va vào line hoặc lực đủ mạnh
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

        // Ẩn trứng nhưng không xóa object
        sr.enabled = false;
        col.enabled = false;
        if (rb) rb.simulated = false;

        if (pool && !string.IsNullOrEmpty(VFXkey))
        {
            var vfx = pool.Get(VFXkey, transform.position, Quaternion.identity);
            if (vfx)
            {
                // nếu prefab VFX có ParticleSystem, phát hiệu ứng
                var ps = vfx.GetComponent<ParticleSystem>();
                if (ps)
                {
                    ps.Clear(true);
                    ps.Play(true);
                    pool.StartCoroutine(ReturnVFXWhenDone(ps, VFXkey));
                }
            }
        }

        foreach (var key in fragKeys)
        {
            var pos = (Vector2)transform.position + Random.insideUnitCircle * 0.02f;
            var rot = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            var frag = pool.Get(key, pos, rot);
            if (!frag) continue; // nếu hết hàng thì bỏ qua

            frag.transform.localScale = Vector3.one * fragmentScale;

            var rbf = frag.GetComponent<Rigidbody2D>();
            if (rbf)
            {
                rbf.velocity = (inheritEggVelocity && rb) ? rb.velocity : Vector2.zero;
                rbf.angularVelocity = Random.Range(-randomAngularVel, randomAngularVel);
            }
        }
    }

    IEnumerator ReturnVFXWhenDone(ParticleSystem ps, string key)
    {
        yield return new WaitWhile(() => ps && ps.IsAlive(true));
        if (ps && pool)
            pool.Return(key, ps.gameObject);
    }
}
