using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MovingPlatform : MonoBehaviour
{
    [Header("Mốc di chuyển")]
    public Transform pointA;
    public Transform pointB;

    [Header("Tốc độ & chờ")]
    [Tooltip("Vận tốc di chuyển theo đơn vị thế giới/giây")]
    public float speed = 2f;
    [Tooltip("Thời gian chờ ở mỗi đầu mút")]
    public float waitAtEnds = 0.2f;

    [Header("Tùy chọn")]
    [Tooltip("Tự chạy ngay khi bắt đầu (bỏ chọn để yêu cầu click mới chạy)")]
    public bool autoStart = false;
    [Tooltip("Dùng Rigidbody2D nếu có (mượt khi va chạm)")]
    public bool preferRigidbody2D = true;

    Rigidbody2D rb2d;
    Coroutine moveCo;
    bool isMoving;

    void Awake()
    {
        rb2d = GetComponent<Rigidbody2D>();
        // Nếu có Rigidbody2D, nên để body type = Kinematic để kéo vật khác an toàn
        if (rb2d && rb2d.bodyType == RigidbodyType2D.Dynamic)
        {
            // Không bắt buộc, nhưng khuyến nghị kinematic cho platform tự hành
            Debug.LogWarning("[MovingPlatform] Nên dùng Rigidbody2D Kinematic cho platform tự di chuyển.");
        }
    }

    void Start()
    {
        if (!pointA || !pointB)
        {
            Debug.LogError("[MovingPlatform] Thiếu pointA/pointB!");
            enabled = false;
            return;
        }

        // Đặt về gần A khi bắt đầu (không bắt buộc)
        if (Vector2.Distance(transform.position, pointA.position) > 0.01f)
            SetPosition(pointA.position);

        if (autoStart) StartMoving();
    }

    // Click bằng chuột (không cần EventSystem)
    void OnMouseDown()
    {
        // Nếu có UI che, có thể không nhận được; khi đó hãy thêm EventSystem + Physics2DRaycaster và dùng IPointerClickHandler
        if (isMoving) StopMoving();
        else StartMoving();
    }

    public void StartMoving()
    {
        if (isMoving) return;
        isMoving = true;
        moveCo = StartCoroutine(MoveLoop());
    }

    public void StopMoving()
    {
        if (!isMoving) return;
        isMoving = false;
        if (moveCo != null) StopCoroutine(moveCo);
        moveCo = null;
    }

    IEnumerator MoveLoop()
    {
        // Di chuyển A -> B -> A lặp lại
        Transform from = pointA;
        Transform to   = pointB;

        // chờ 1 frame để đảm bảo init xong
        yield return null;

        while (isMoving)
        {
            yield return MoveOneLeg(from.position, to.position);

            // nghỉ ở đầu mút
            if (waitAtEnds > 0f)
                yield return new WaitForSeconds(waitAtEnds);

            // đảo chiều
            var tmp = from; from = to; to = tmp;
        }
    }

    IEnumerator MoveOneLeg(Vector3 start, Vector3 end)
    {
        // dùng MoveTowards để tránh vượt đích
        // Nếu có Rigidbody2D & preferRigidbody2D: chạy theo FixedUpdate
        if (preferRigidbody2D && rb2d)
        {
            // đảm bảo kinematic để MovePosition ổn định
            // (Nếu bạn muốn Dynamic, hãy giảm khối lượng hoặc tắt collision với vật nặng)
            var wait = new WaitForFixedUpdate();
            while (Vector2.Distance(CurrentPosition(), end) > 0.001f && isMoving)
            {
                Vector2 next = Vector2.MoveTowards(CurrentPosition(), end, speed * Time.fixedDeltaTime);
                rb2d.MovePosition(next);
                yield return wait;
            }
            SetPosition(end);
        }
        else
        {
            while (Vector2.Distance(CurrentPosition(), end) > 0.001f && isMoving)
            {
                Vector3 next = Vector2.MoveTowards(CurrentPosition(), end, speed * Time.deltaTime);
                SetPosition(next);
                yield return null;
            }
            SetPosition(end);
        }
    }

    Vector2 CurrentPosition()
    {
        return rb2d ? rb2d.position : (Vector2)transform.position;
    }

    void SetPosition(Vector3 pos)
    {
        if (rb2d) rb2d.position = pos;
        else transform.position = pos;
    }

    // Vẽ gizmo để thấy A/B trong Scene
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (pointA) Gizmos.DrawSphere(pointA.position, 0.08f);
        Gizmos.color = Color.red;
        if (pointB) Gizmos.DrawSphere(pointB.position, 0.08f);
        if (pointA && pointB)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pointA.position, pointB.position);
        }
    }
}
