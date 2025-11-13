using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Bom : MonoBehaviour
{
    [Header("Fuse")]
    [Tooltip("Thời gian từ lúc bật kíp tới khi nổ")]
    public float fuseTime = 2f;

    [Tooltip("Bật kíp khi VA CHẠM (collision). Không dùng trigger.")]
    public bool startOnTouch = true;

    [Header("Arming rules (lọc điều kiện bật kíp)")]
    [Tooltip("Chỉ các layer này mới được PHÉP bật kíp. 0 = cho tất cả.")]
    public LayerMask armByLayers = 0;

    [Tooltip("Bỏ qua va chạm trong vài trăm ms đầu để tránh auto-arm khi spawn đang đè nền")]
    public float armDelay = 0.2f;

    [Tooltip("Yêu cầu vận tốc TỐI THIỂU của chính bom để bật kíp (m/s)")]
    public float minSelfSpeed = 0.2f;

    [Tooltip("Yêu cầu vận tốc TƯƠNG ĐỐI của va chạm để bật kíp (m/s)")]
    public float minRelativeSpeed = 0.3f;

    [Header("Explosion")]
    public float explosionRadius = 3f;
    public float explosionForce = 500f;

    [Tooltip("Chỉ các layer này mới bị ảnh hưởng bởi VỤ NỔ")]
    public LayerMask affectedLayers = ~0;

    [Header("Effects (Explode)")]
    public GameObject explosionVFX;      
    public AudioClip  explosionSfx;
    public float      destroyDelay = 0.2f;

    [Header("Effects (Fuse start)")]
    [Tooltip("VFX xuất hiện NGAY khi bật kíp (nên là particle Loop)")]
    public GameObject fuseStartVFX;

    [Tooltip("SFX khi bắt đầu cháy kíp")]
    public AudioClip  fuseStartSfx;

    [Tooltip("Offset LOCAL để đặt VFX mồi đúng vị trí dây bom")]
    public Vector3    fuseVfxOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("Cho VFX mồi bám theo quả bom trong suốt thời gian chờ nổ")]
    public bool       attachFuseVFXToBomb = true;

    // ===== internal =====
    bool        fuseStarted = false;
    Coroutine   fuseCoroutine;
    GameObject  fuseVfxInstance;
    AudioSource audioOneShot;
    float       _spawnTime;

    void Reset()
    {
        // DÙNG COLLIDER THƯỜNG (KHÔNG TRIGGER) để bám tường/sàn
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = false;
    }

    void OnValidate()
    {
        // Đảm bảo vẫn không phải trigger nếu bạn lỡ bật ở Inspector
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = false;
    }

    void Awake()
    {
        // Audio nhẹ để phát one-shot
        audioOneShot = GetComponent<AudioSource>();
        if (!audioOneShot)
        {
            audioOneShot = gameObject.AddComponent<AudioSource>();
            audioOneShot.playOnAwake = false;
            audioOneShot.spatialBlend = 0f; // 2D
        }
        _spawnTime = Time.time;
    }

    // ====== ARm by COLLISION (không dùng trigger) ======
    void OnCollisionEnter2D(Collision2D c)
    {
        if (!startOnTouch) return;
        TryArmByCollision(c);
    }

    void TryArmByCollision(Collision2D c)
    {
        if (fuseStarted || c == null) return;

        // 1) Anti auto-arm ngay khi spawn (đang đè sẵn lên nền)
        if (Time.time - _spawnTime < armDelay) return;

        // 2) Lọc layer được phép bật kíp (nếu có chọn)
        if (armByLayers.value != 0)
        {
            int otherLayer = c.collider.gameObject.layer;
            if ((armByLayers & (1 << otherLayer)) == 0)
                return; // layer này KHÔNG được phép bật kíp
        }

        // 3) Yêu cầu vận tốc tối thiểu
        var rb = GetComponent<Rigidbody2D>();
        float selfSpeed = rb ? rb.velocity.magnitude : 0f;
        if (selfSpeed < minSelfSpeed) return;

        // 4) Yêu cầu vận tốc tương đối tối thiểu của va chạm
        float relSpeed = c.relativeVelocity.magnitude;
        if (relSpeed < minRelativeSpeed) return;

        // ---- Bật kíp ----
        StartFuse();
    }

    void StartFuse()
    {
        fuseStarted = true;

        // VFX mồi (cháy bấc)
        if (fuseStartVFX)
        {
            Vector3 spawnPos = transform.position + transform.TransformDirection(fuseVfxOffset);
            Quaternion spawnRot = transform.rotation;

            fuseVfxInstance = Instantiate(fuseStartVFX, spawnPos, spawnRot);
            if (attachFuseVFXToBomb) fuseVfxInstance.transform.SetParent(transform, true);

            // Hướng VFX cùng hướng "up" của bom (tuỳ prefab)
            fuseVfxInstance.transform.up = transform.up;
            fuseVfxInstance.transform.localScale = Vector3.one;

            var ps = fuseVfxInstance.GetComponent<ParticleSystem>();
            if (ps)
            {
                var main = ps.main;
                if (!main.loop) main.loop = true;
                ps.Play(true);
            }
        }

        if (fuseStartSfx && audioOneShot) audioOneShot.PlayOneShot(fuseStartSfx);

        fuseCoroutine = StartCoroutine(FuseAndExplode());
    }

    IEnumerator FuseAndExplode()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    // ====== EXPLODE ======
    void Explode()
    {
        // TẮT/DỌN VFX mồi
        if (fuseVfxInstance)
        {
            var ps = fuseVfxInstance.GetComponent<ParticleSystem>();
            if (ps)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(fuseVfxInstance, 1f);
            }
            else Destroy(fuseVfxInstance);
            fuseVfxInstance = null;
        }

        // VFX/SFX nổ
        if (explosionVFX)
        {
            var v = Instantiate(explosionVFX, transform.position, Quaternion.identity);
            Destroy(v, 5f);
        }
        if (explosionSfx)
        {
            if (audioOneShot) audioOneShot.PlayOneShot(explosionSfx);
            else AudioSource.PlayClipAtPoint(explosionSfx, transform.position);
        }

        // Ảnh hưởng vật lý
        var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, affectedLayers);
        foreach (var hit in hits)
        {
            var trb = hit.attachedRigidbody;
            if (trb && trb.bodyType == RigidbodyType2D.Dynamic && !trb.isKinematic)
            {
                Vector2 dir = (trb.worldCenterOfMass - (Vector2)transform.position);
                float dist = Mathf.Max(0.01f, dir.magnitude);

                // giảm theo khoảng cách, có sàn 20%
                float attenuation = Mathf.Clamp01(1f - dist / explosionRadius);
                attenuation = Mathf.Max(attenuation, 0.2f);

                Vector2 impulse = dir.normalized * explosionForce * attenuation;
                trb.WakeUp();
                trb.AddForce(impulse, ForceMode2D.Impulse);
            }

            var dmg = hit.GetComponent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(10f);
            else hit.SendMessage("OnExploded", SendMessageOptions.DontRequireReceiver);
        }

        // Ẩn bom, huỷ trễ để VFX nổ phát xong
        var rend = GetComponent<Renderer>(); if (rend) rend.enabled = false;
        var col  = GetComponent<Collider2D>(); if (col)  col.enabled = false;
        Destroy(gameObject, destroyDelay);
    }

    void OnDrawGizmosSelected()
    {
        // Vẽ vị trí VFX mồi (offset local) để canh dây bom
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position + transform.TransformDirection(fuseVfxOffset);
        Gizmos.DrawSphere(p, 0.06f);

        // Vẽ bán kính nổ
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

// Interface mẫu (tuỳ chọn)
public interface IDamageable
{
    void TakeDamage(float amount);
}
