using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Bom : MonoBehaviour
{
    public enum BombMode
    {
        WithRigidbody,   // dùng va chạm vật lý (OnCollisionEnter2D)
        TriggerOnly      // dùng trigger, không cần lực va chạm
    }

    [Header("Chế độ hoạt động")]
    public BombMode bombMode = BombMode.WithRigidbody;

    [Header("Fuse")]
    [Tooltip("Thời gian từ lúc bật kíp → nổ")]
    public float fuseTime = 2f;
    public bool startOnTouch = true;

    [Header("Explosion")]
    public float explosionRadius = 3f;
    public float explosionForce = 500f;
    [Tooltip("Chỉ các layer này mới bị lực nổ tác động")]
    public LayerMask affectedLayers = ~0;

    public GameObject explosionVFX;
    public AudioClip explosionSfx;
    public float destroyDelay = 0.2f;

    [Header("Fuse VFX/SFX (bật NGAY khi châm ngòi)")]
    public GameObject fuseStartVFX;
    public AudioClip fuseStartSfx;
    [Tooltip("Offset LOCAL của vfx châm ngòi (dây bom)")]
    public Vector3 fuseVfxOffset = new Vector3(0f, 0.2f, 0f);
    public bool attachFuseVFXToBomb = true;

    [Header("Điều kiện bật kíp (chỉ dùng cho WithRigidbody)")]
    [Tooltip("Bỏ qua va chạm trong X giây đầu sau khi spawn để tránh nổ ngay khi Play")]
    public float armDelay = 0.2f;
    [Tooltip("Chỉ cho phép các layer này châm kíp (0 = mọi layer)")]
    public LayerMask armByLayers = 0;
    [Tooltip("Tốc độ tối thiểu của bản thân bom để tính va chạm (m/s)")]
    public float minSelfSpeed = 0.1f;
    [Tooltip("Tốc độ tương đối tối thiểu của va chạm (m/s)")]
    public float minRelativeSpeed = 0.2f;

    // ========= internal =========
    bool fuseStarted;
    Coroutine fuseCo;
    GameObject fuseVfxInstance;
    AudioSource audioSrc;
    float spawnTime;

    void Awake()
    {
        audioSrc = GetComponent<AudioSource>();
        if (!audioSrc)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;
        }

        spawnTime = Time.time;

        var rb = GetComponent<Rigidbody2D>();
        var cols = GetComponents<Collider2D>();

        if (bombMode == BombMode.WithRigidbody)
        {
            // MODE 1: có Rigidbody
            if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;

            // tất cả collider là collider thường
            foreach (var c in cols) c.isTrigger = false;
        }
        else
        {
            // MODE 2: TriggerOnly
            if (rb)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
            }

            bool hasSolid = false;
            bool hasTrigger = false;
            foreach (var c in cols)
            {
                if (c.isTrigger) hasTrigger = true;
                else hasSolid = true;
            }

            // đảm bảo có ít nhất 1 collider thường để chặn vật
            if (!hasSolid && cols.Length > 0)
                cols[0].isTrigger = false;

            // đảm bảo có 1 trigger để bắt kíp
            if (!hasTrigger)
            {
                var trig = gameObject.AddComponent<CircleCollider2D>();
                trig.isTrigger = true;
            }
        }
    }

    // ========= MODE 1: WithRigidbody (dùng Collision) =========
    void OnCollisionEnter2D(Collision2D col)
    {
        if (bombMode != BombMode.WithRigidbody) return;
        if (!startOnTouch || fuseStarted) return;

        // 1) tránh auto-arm ngay frame đầu khi đang đè lên tường
        if (Time.time - spawnTime < armDelay) return;

        // 2) lọc layer được phép châm kíp
        if (armByLayers.value != 0)
        {
            int otherLayer = col.collider.gameObject.layer;
            if ((armByLayers & (1 << otherLayer)) == 0)
                return;
        }

        // 3) kiểm tra tốc độ của chính bom
        var rb = GetComponent<Rigidbody2D>();
        float selfSpeed = rb ? rb.velocity.magnitude : 0f;
        if (selfSpeed < minSelfSpeed) return;

        // 4) kiểm tra tốc độ tương đối của va chạm
        float relSpeed = col.relativeVelocity.magnitude;
        if (relSpeed < minRelativeSpeed) return;

        StartFuse();
    }

    // ========= MODE 2: TriggerOnly (dùng Trigger) =========
    void OnTriggerEnter2D(Collider2D other)
    {
        if (bombMode != BombMode.TriggerOnly) return;
        if (!startOnTouch || fuseStarted) return;

        // nếu cũng muốn delay cho mode này thì bỏ comment dòng dưới
        // if (Time.time - spawnTime < armDelay) return;

        StartFuse();
    }

    // ========= BẮT ĐẦU CHÂM NGÒI =========
    void StartFuse()
    {
        if (fuseStarted) return;
        fuseStarted = true;

        // VFX châm ngòi
        if (fuseStartVFX)
        {
            Vector3 spawnPos = transform.TransformPoint(fuseVfxOffset);
            fuseVfxInstance = Instantiate(fuseStartVFX, spawnPos, transform.rotation);

            if (attachFuseVFXToBomb)
                fuseVfxInstance.transform.SetParent(transform, true);

            fuseVfxInstance.transform.localScale = Vector3.one;

            fuseVfxInstance.transform.up = transform.up;

            var ps = fuseVfxInstance.GetComponent<ParticleSystem>();
            if (ps)
            {
                var main = ps.main;
                if (!main.loop) main.loop = true;
                ps.Play(true);
            }
        }

        if (fuseStartSfx && audioSrc)
            audioSrc.PlayOneShot(fuseStartSfx);

        fuseCo = StartCoroutine(FuseAndExplode());
    }

    IEnumerator FuseAndExplode()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    // ========= NỔ =========
    void Explode()
    {
        // tắt VFX châm ngòi
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
            if (audioSrc) audioSrc.PlayOneShot(explosionSfx);
            else AudioSource.PlayClipAtPoint(explosionSfx, transform.position);
        }

        // Lực nổ
        var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, affectedLayers);
        foreach (var hit in hits)
        {
            var rb = hit.attachedRigidbody;
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic && !rb.isKinematic)
            {
                Vector2 dir = rb.worldCenterOfMass - (Vector2)transform.position;
                float dist = Mathf.Max(0.01f, dir.magnitude);

                float att = Mathf.Clamp01(1f - dist / explosionRadius);
                att = Mathf.Max(att, 0.2f);

                rb.WakeUp();
                rb.AddForce(dir.normalized * explosionForce * att, ForceMode2D.Impulse);
            }

            var dmg = hit.GetComponent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(10f);
            else hit.SendMessage("OnExploded", SendMessageOptions.DontRequireReceiver);
        }

        var rend = GetComponent<Renderer>(); if (rend) rend.enabled = false;
        var col = GetComponent<Collider2D>(); if (col) col.enabled = false;
        Destroy(gameObject, destroyDelay);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(fuseVfxOffset), 0.06f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

public interface IDamageable
{
    void TakeDamage(float amount);
}
