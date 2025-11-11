using System.Collections;
using Spine.Unity;
using UnityEngine;

[DisallowMultipleComponent]
public class EggBreak : MonoBehaviour
{
    [Header("Loại trứng")]
    public bool isBadEgg = false;   // BadEgg = true, GoodEgg = false

    [Header("Điều kiện vỡ")]
    public float breakVelocity = 5f;
    public string instantBreakLayer = "Line";

    [Header("Nhóm mảnh vỡ (ShellGroup)")]
    [Tooltip("Empty chứa Manh1..Manh5, mặc định SetActive(false).")]
    public GameObject shellObject;
    [Tooltip("Cho mảnh có physics hay không. Đa số để false cho nhẹ.")]
    public bool shellHasPhysics = false;
    [Tooltip("Tách ShellGroup ra khỏi Egg trước khi tắt Egg.")]
    public bool detachShell = true;

    [Header("Pool (VFX)")]
    public Pool pool;
    public string VFXkey = "VFX";

    Rigidbody2D rb;
    Collider2D col;
    SkeletonAnimation ske;
    bool isBroken;
    int instantBreakLayerId;

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>() ?? gameObject.AddComponent<CircleCollider2D>();
        ske = GetComponent<SkeletonAnimation>();

        instantBreakLayerId = LayerMask.NameToLayer(instantBreakLayer);
        if (instantBreakLayerId < 0)
            Debug.LogWarning($"[EggBreak] Layer '{instantBreakLayer}' chưa tồn tại.");

        // Đăng ký BadEgg với GameManager
        if (GameManager.Instance)
            GameManager.Instance.RegisterEgg(this, isBadEgg);

        // Chuẩn bị ShellGroup
        if (shellObject)
        {
            // Nếu trong Shell có Spine thì init trước
            var shellSke = shellObject.GetComponent<SkeletonAnimation>();
            if (shellSke)
            {
                shellSke.Initialize(false);
                shellSke.enabled = false;
            }

            // if (!shellHasPhysics)
            // {
            //     var srb = shellObject.GetComponent<Rigidbody2D>();
            //     if (srb) srb.simulated = false;

            //     var cols = shellObject.GetComponentsInChildren<Collider2D>(true);
            //     foreach (var c in cols) c.enabled = false;
            // }

            shellObject.SetActive(false);
        }

        // Prewarm VFX trong pool (nếu có)
        if (pool && !string.IsNullOrEmpty(VFXkey))
        {
            var v = pool.Get(VFXkey, transform.position, Quaternion.identity, null);
            if (v) pool.Return(v);
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (isBroken) return;

        bool hitLine   = (instantBreakLayerId >= 0 && c.gameObject.layer == instantBreakLayerId);
        bool strongHit = c.relativeVelocity.sqrMagnitude >= breakVelocity * breakVelocity;

        if (hitLine || strongHit)
            Break();
    }

    void Break()
    {
        if (isBroken) return;
        isBroken = true;

        // Tắt logic / hình trứng gốc
        if (col) col.enabled = false;
        if (ske) ske.enabled = false;
        if (rb)  rb.simulated = false;

        // ===== BẬT NHÓM MẢNH VỠ =====
        if (shellObject)
        {
            // đặt vị trí tại chỗ trứng
            shellObject.transform.position = transform.position;

            // Tách khỏi Egg TRƯỚC KHI tắt Egg
            if (detachShell)
            {
                var parent = LevelManager.Instance ? LevelManager.Instance.RuntimeRoot : null;
                if (parent)
                    shellObject.transform.SetParent(parent, true);
                else
                    shellObject.transform.SetParent(null, true);
            }

            shellObject.SetActive(true);

            // Nếu shell có Spine
            var shellSke = shellObject.GetComponent<SkeletonAnimation>();
            if (shellSke)
                shellSke.enabled = true;
        }

        // ===== VFX =====
        if (pool && !string.IsNullOrEmpty(VFXkey))
        {
            var parent = LevelManager.Instance ? LevelManager.Instance.RuntimeRoot : null;
            var vfx = pool.Get(VFXkey, transform.position, Quaternion.identity, parent);
            if (vfx)
            {
                var ps = vfx.GetComponent<ParticleSystem>();
                if (ps)
                {
                    ps.Clear(true);
                    ps.Play(true);
                    StartCoroutine(ReturnVFX(ps, vfx));
                }
                else
                {
                    StartCoroutine(ReturnAfterDelay(vfx, 0.7f));
                }
            }
        }

        // Thông báo GameManager
        if (GameManager.Instance)
            GameManager.Instance.OnEggBroken(this);

        // Cuối cùng mới tắt Egg gốc
        gameObject.SetActive(false);
    }

    // ===== Pool helpers =====

    IEnumerator ReturnVFX(ParticleSystem ps, GameObject go)
    {
        while (ps != null && ps.IsAlive(true))
            yield return null;

        if (pool != null && go != null)
            pool.Return(go);
    }

    IEnumerator ReturnAfterDelay(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (pool != null && go != null)
            pool.Return(go);
    }
}
