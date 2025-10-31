using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Bom : MonoBehaviour
{
    [Header("Fuse")]
    public float fuseTime = 2f;                  // thời gian chờ trước khi nổ
    public bool startOnTouch = true;             // có bắt đầu đếm khi chạm không

    [Header("Explosion")]
    public float explosionRadius = 3f;
    public float explosionForce = 500f;          // lực văng
    public LayerMask affectedLayers = ~0;        // layer mask các object bị ảnh hưởng (mặc định tất cả)

    [Header("Effects (optional)")]
    public GameObject explosionVFX;              // prefab particle/animation (optional)
    public AudioClip explosionSfx;
    public float destroyDelay = 0.2f;            // sau nổ, huỷ object (để VFX phát)

    bool fuseStarted = false;
    Coroutine fuseCoroutine;

    private void Reset()
    {
        // đảm bảo collider là trigger để dễ chạm
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!startOnTouch) return;
        StartFuseIfNeeded(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (startOnTouch)
            StartFuseIfNeeded(collision.gameObject);
    }

    void StartFuseIfNeeded(GameObject triggerer)
    {
        if (fuseStarted) return;
        // (tuỳ chọn) bạn có thể kiểm tra tag hoặc component ở đây
        fuseStarted = true;
        fuseCoroutine = StartCoroutine(FuseAndExplode());
    }

    IEnumerator FuseAndExplode()
    {
        // bạn có thể play beeping sound hoặc thay đổi sprite ở đây
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    void Explode()
    {
        if (explosionVFX) { var v = Instantiate(explosionVFX, transform.position, Quaternion.identity); Destroy(v, 5f); }
        if (explosionSfx) AudioSource.PlayClipAtPoint(explosionSfx, transform.position);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, affectedLayers);

        foreach (var hit in hits)
        {
            Rigidbody2D rb = hit.attachedRigidbody;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic && !rb.isKinematic)
            {
                Vector2 dir = (rb.worldCenterOfMass - (Vector2)transform.position);
                float dist = Mathf.Max(0.01f, dir.magnitude);

                // Giảm lực theo khoảng cách + đặt sàn tối thiểu
                float attenuation = Mathf.Clamp01(1f - dist / explosionRadius);
                float minAtten = 0.2f; // không thấp hơn 20%
                attenuation = Mathf.Max(attenuation, minAtten);

                // Xung lực tức thời
                Vector2 impulse = dir.normalized * explosionForce * attenuation;

                // (tuỳ chọn) thêm chút thành phần hướng ra ngoài màn để cảm giác “bật” rõ hơn
                // impulse += Vector2.up * (explosionForce * 0.1f);

                rb.WakeUp();
                rb.AddForce(impulse, ForceMode2D.Impulse);
            }

            // Damage/SendMessage như cũ…
            var dmg = hit.GetComponent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(10f);
            else hit.SendMessage("OnExploded", SendMessageOptions.DontRequireReceiver);
        }

        var rend = GetComponent<Renderer>(); if (rend) rend.enabled = false;
        var col = GetComponent<Collider2D>(); if (col) col.enabled = false;
        Destroy(gameObject, destroyDelay);
    }


    // để minh hoạ trong scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}

// Interface mẫu (tuỳ chọn) để các object có thể nhận damage
public interface IDamageable
{
    void TakeDamage(float amount);
}
