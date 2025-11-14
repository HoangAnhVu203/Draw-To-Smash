using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(SpriteRenderer))]
public class HandHintMover : MonoBehaviour
{
    [Header("Path/Speed")]
    public Transform[] waypoints;
    public float moveSpeed = 3f;
    public float pauseAtNode = 0.25f;

    [Header("Loop")]
    public bool loop = true;
    public float loopDelay = 0.6f;

    [Header("Visual FX (optional)")]
    public bool tapPulse = true;
    public float pulseScale = 1.2f;
    public float pulseTime = 0.12f;

    [Header("Finish")]
    public bool destroyOnEnd = true;       // ✅ hết đường thì biến mất
    public float fadeOutTime = 0.35f;      // ✅ mờ dần trước khi destroy
    public UnityEvent OnFinished;          // ✅ callback khi kết thúc (1 lần)

    LineRenderer lr;                       // nếu có
    SpriteRenderer sr;
    Coroutine co;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        lr = GetComponent<LineRenderer>(); // optional
        // đảm bảo scale theo prefab
        transform.localScale = transform.localScale;
    }

    void OnEnable()
    {
        Restart();
    }

    public void SetPath(Transform[] points)
    {
        waypoints = points;
        Restart();
    }

    void Restart()
    {
        if (!isActiveAndEnabled) return;
        if (co != null) StopCoroutine(co);
        if (waypoints != null && waypoints.Length > 0)
            co = StartCoroutine(CoRun());
    }

    IEnumerator CoRun()
    {
        if (waypoints == null || waypoints.Length == 0) yield break;

        transform.position = waypoints[0].position;

        do
        {
            for (int i = 1; i < waypoints.Length; i++)
            {
                if (tapPulse) yield return CoPulse();

                Vector3 from = transform.position;
                Vector3 to = waypoints[i].position;

                float dist = Vector3.Distance(from, to);
                float dur = Mathf.Max(0.01f, dist / Mathf.Max(0.01f, moveSpeed));
                float t = 0f;

                while (t < dur)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Clamp01(t / dur);
                    // ease in-out
                    u = u * u * (3f - 2f * u);
                    transform.position = Vector3.Lerp(from, to, u);
                    yield return null;
                }

                transform.position = to;
                if (tapPulse) yield return CoPulse();
                if (pauseAtNode > 0f) yield return new WaitForSeconds(pauseAtNode);
            }

            if (loop)
            {
                if (loopDelay > 0f) yield return new WaitForSeconds(loopDelay);
                if (waypoints.Length > 0)
                    transform.position = waypoints[0].position;
            }

        } while (loop);

        // ✅ kết thúc một lượt và không lặp
        OnFinished?.Invoke();

        if (destroyOnEnd)
        {
            // mờ dần rồi destroy
            yield return StartCoroutine(CoFadeOut());
            Destroy(gameObject);
        }
    }

    IEnumerator CoPulse()
    {
        Vector3 baseScale = transform.localScale;
        float t = 0f;

        while (t < pulseTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / pulseTime);
            transform.localScale = Vector3.Lerp(baseScale, baseScale * pulseScale, u);
            yield return null;
        }
        t = 0f;
        while (t < pulseTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / pulseTime);
            transform.localScale = Vector3.Lerp(baseScale * pulseScale, baseScale, u);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    IEnumerator CoFadeOut()
    {
        // hỗ trợ cả SpriteRenderer và LineRenderer (nếu dùng)
        float t = 0f;

        // lấy màu ban đầu
        Color cS = sr ? sr.color : Color.white;
        Gradient startGrad = null;
        Gradient grad = null;

        if (lr)
        {
            startGrad = lr.colorGradient;
            grad = new Gradient();
        }

        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeOutTime);

            if (sr)
            {
                var c = cS; c.a = a;
                sr.color = c;
            }

            if (lr && startGrad != null)
            {
                var c0 = startGrad.Evaluate(0f);
                var c1 = startGrad.Evaluate(1f);
                c0.a = a; c1.a = a;
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(c0.a, 0f), new GradientAlphaKey(c1.a, 1f) }
                );
                lr.colorGradient = grad;
            }

            yield return null;
        }
    }
}
