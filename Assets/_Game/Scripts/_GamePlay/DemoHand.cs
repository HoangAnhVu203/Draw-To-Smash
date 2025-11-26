using System.Collections;
using UnityEngine;

/// <summary>
/// Tay DEMO tự vẽ theo path:
/// - Khi GameManager.State == Demo -> tay chạy & vẽ
/// - Đi hết path -> EndStroke() để nét rơi xuống
/// - Đợi 1 lúc -> clear nét -> lặp lại
/// </summary>
public class DemoHand : MonoBehaviour
{
    [Header("Đường đi của tay (kéo các điểm vào)")]
    public Transform[] pathPoints;

    [Header("Tốc độ di chuyển")]
    public float moveSpeed = 4f;

    [Header("Đợi 1 chút trước khi nét rơi")]
    public float delayBeforeFall = 0.15f;

    [Header("Đợi trước khi lặp lại demo tiếp")]
    public float delayBeforeRestart = 1.5f;

    [Header("Ẩn tay khi không ở state Demo")]
    public bool hideWhenNotDemo = true;

    Coroutine loopCo;

    void OnEnable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = StartCoroutine(DemoLoop());
    }

    void OnDisable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = null;
    }

    IEnumerator DemoLoop()
    {
        while (true)
        {
            // chờ đến khi GameManager sẵn sàng
            while (GameManager.Instance == null ||
                   GameManager.Instance.CurrentState != GameState.Demo)
            {
                if (hideWhenNotDemo) SetVisible(false);
                yield return null;
            }

            SetVisible(true);

            // đảm bảo có path & DrawManager
            if (pathPoints == null || pathPoints.Length < 2 || DrawManager.Instance == null)
            {
                yield return null;
                continue;
            }

            yield return StartCoroutine(PlayOneDemoStroke());

            yield return new WaitForSeconds(delayBeforeRestart);
        }
    }

    IEnumerator PlayOneDemoStroke()
    {
        if (pathPoints.Length < 2) yield break;

        // đặt tay ở điểm đầu
        Vector3 startPos = pathPoints[0].position;
        transform.position = startPos;

        // BẮT ĐẦU VẼ
        DrawManager.Instance.StartStroke(startPos);   // ← đổi tên hàm nếu khác

        // đi qua từng đoạn path
        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            Vector3 from = pathPoints[i].position;
            Vector3 to = pathPoints[i + 1].position;

            float t = 0f;
            float dist = Vector3.Distance(from, to);
            float duration = Mathf.Max(0.01f, dist / moveSpeed);

            while (t < duration)
            {
                // nếu đã thoát demo giữa chừng -> dừng
                if (GameManager.Instance.CurrentState != GameState.Demo)
                {
                    DrawManager.Instance.EndStroke();
                    yield break;
                }

                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / duration);
                Vector3 pos = Vector3.Lerp(from, to, lerp);
                transform.position = pos;

                // mỗi frame thêm 1 điểm vào stroke
                DrawManager.Instance.AddStrokePoint(pos);

                yield return null;
            }
        }

        // đến điểm cuối -> đợi 1 chút rồi thả nét rơi xuống
        yield return new WaitForSeconds(delayBeforeFall);

        DrawManager.Instance.EndStroke(); // ← stroke trở thành rigidbody và rơi xuống (theo code của bạn)

        // không clear stroke ở đây, cho người chơi nhìn 1 lúc
        // clear sẽ do LevelManager/DrawManager/Erase tự lo, hoặc bạn có thể:
        // DrawManager.Instance.ClearAllStrokes();
    }

    void SetVisible(bool v)
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend) rend.enabled = v;

        var canvasGroup = GetComponentInChildren<CanvasGroup>();
        if (canvasGroup) canvasGroup.alpha = v ? 1f : 0f;
    }
}
