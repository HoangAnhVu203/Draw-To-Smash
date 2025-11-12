using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DrawManager : MonoBehaviour
{
    public static DrawManager Instance { get; private set; }

    [Header("Camera (để trống sẽ tự lấy Main Camera)")]
    public Camera cam;

    [Header("Nơi chứa các nét vẽ (để rọn dẹp)")]
    public Transform strokesRoot;

    [Header("Bead / Quả bóng")]
    [Tooltip("Prefab 1 quả bóng: SpriteRenderer + CircleCollider2D (KHÔNG có Rigidbody2D).")]
    public GameObject beadPrefab;

    [Tooltip("Đường kính 1 quả bóng theo đơn vị world.")]
    public float beadDiameter = 0.25f;

    [Range(0f, 0.99f)]
    [Tooltip("Phần trăm chồng lên nhau. 0 = chỉ chạm; 0.5 = quả mới khi quả trước đi được 1/2.")]
    public float beadOverlap = 0.5f;

    [Tooltip("Khoảng cách tối thiểu di chuyển tay mới xử lý tiếp.")]
    public float minPointDistance = 0.02f;

    [Header("Sorting")]
    public int sortingOrder = 200;

    [Header("Vật lý")]
    public float gravityScale = 1f;
    public float friction = 0.4f;
    public float bounciness = 0.05f;

    [Header("Giới hạn vùng vẽ")]
    public string drawLayerName = "DrawArea";

    [Header("Ẩn vùng khi vẽ xong")]
    public bool fadeOutArea = true;
    public float fadeDuration = 0.25f;

    [Header("Tên Layer đối tượng cần biến mất khi va chạm")]
    public string[] vanishLayers = { "GoodEgg", "BadEgg" };

    [Header("GameObject được bật khi va chạm trứng")]
    public GameObject objectToEnable;
    public float vanishDelay = 1f;
    private string liftLayerName = "Lift";

    GameObject currentStroke;
    Rigidbody2D currentRb;
    Collider2D activeDrawArea;

    readonly List<GameObject> beads = new();
    Vector2 lastBeadPos;
    int drawLayer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!cam) cam = Camera.main;

        drawLayer = LayerMask.NameToLayer(drawLayerName);

        if (!strokesRoot)
        {
            var root = new GameObject("StrokesRoot");
            root.transform.SetParent(transform, false);
            strokesRoot = root.transform;
        }
    }

    void Start()
    {
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) TryBeginStroke(Input.mousePosition);
        if (Input.GetMouseButton(0)) TryContinueStroke(Input.mousePosition);
        if (Input.GetMouseButtonUp(0)) EndStroke();
#else
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) TryBeginStroke(t.position);
            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) TryContinueStroke(t.position);
            if (t.phase == TouchPhase.Ended) EndStroke();
        }
#endif
    }

    // ================== DRAW AREA ==================

    void TryBeginStroke(Vector2 screenPos)
    {
        var world = ScreenToWorld(screenPos);
        var col = HitDrawArea(world);
        if (!col) return;

        activeDrawArea = col;
        BeginStroke(world);
    }

    void TryContinueStroke(Vector2 screenPos)
    {
        if (!currentStroke || !activeDrawArea) return;

        var world = ScreenToWorld(screenPos);
        var col = HitDrawArea(world);
        if (col != activeDrawArea) return;

        ContinueStroke(world);
    }

    Collider2D HitDrawArea(Vector2 worldPos)
    {
        int mask = 1 << LayerMask.NameToLayer(drawLayerName);
        return Physics2D.OverlapPoint(worldPos, mask);
    }

    Vector2 ScreenToWorld(Vector2 screen) =>
        (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));

    // ================== STROKE ==================

    void BeginStroke(Vector2 startWorld)
    {
        if (!beadPrefab)
        {
            Debug.LogError("[DrawManager] Chưa gán beadPrefab!");
            return;
        }

        currentStroke = new GameObject("Stroke_Beads");
        currentStroke.transform.SetParent(strokesRoot, false);

        int lineLayer = LayerMask.NameToLayer("Line");
        if (lineLayer != -1)
            currentStroke.layer = lineLayer;

        beads.Clear();

        // spawn bead đầu tiên
        SpawnBead(startWorld, true);
    }

    void ContinueStroke(Vector2 worldPos)
    {
        if (!currentStroke) return;
        if (Vector2.Distance(lastBeadPos, worldPos) < minPointDistance)
            return;

        float spacing = beadDiameter * (1f - beadOverlap);
        if (spacing <= 0f) spacing = 0.0001f;

        // có thể cần thêm nhiều bead nếu tay kéo nhanh
        Vector2 dir = (worldPos - lastBeadPos).normalized;
        float dist = Vector2.Distance(lastBeadPos, worldPos);

        while (dist >= spacing)
        {
            lastBeadPos += dir * spacing;
            SpawnBead(lastBeadPos, false);
            dist -= spacing;
        }
    }

    void SpawnBead(Vector2 pos, bool first)
    {
        var go = Instantiate(beadPrefab, pos, Quaternion.identity, currentStroke.transform);

        // đảm bảo Sorting Order
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr)
        {
            sr.sortingOrder = sortingOrder;

            // auto scale theo beadDiameter (dựa trên bounds hiện tại)
            float curSize = sr.bounds.size.x; // giả sử sprite tròn
            if (curSize > 0f)
            {
                float scale = beadDiameter / curSize;
                go.transform.localScale *= scale;
            }
        }

        // đảm bảo có CircleCollider2D để va chạm
        var cc = go.GetComponent<CircleCollider2D>();
        if (!cc) cc = go.AddComponent<CircleCollider2D>();
        cc.sharedMaterial = new PhysicsMaterial2D("BeadPM")
        {
            friction = friction,
            bounciness = bounciness
        };
        // KHÔNG thêm Rigidbody2D ở đây → dùng chung RB trên object cha

        beads.Add(go);
        lastBeadPos = pos;
    }

    void EndStroke()
    {
        if (!currentStroke)
        {
            activeDrawArea = null;
            return;
        }

        if (beads.Count <= 1)
        {
            Destroy(currentStroke);
        }
        else
        {
            // Thêm Rigidbody2D cho cả chuỗi hạt
            currentRb = currentStroke.AddComponent<Rigidbody2D>();
            currentRb.gravityScale = gravityScale;
            currentRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            currentRb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Nếu muốn xử lý va chạm trứng thì add script tại đây:
            // var handler = currentStroke.AddComponent<StrokeCollisionHandler>();
            // handler.vanishLayers = vanishLayers;
            // handler.objectToEnable = objectToEnable;
            // handler.vanishDelay = vanishDelay;

            var attach = currentStroke.AddComponent<StrokeAttachToLift>();
            attach.Init(liftLayerName);
        }

        // Ẩn vùng vẽ
        if (activeDrawArea)
        {
            if (fadeOutArea) StartCoroutine(FadeAndDisable(activeDrawArea.gameObject, fadeDuration));
            else activeDrawArea.gameObject.SetActive(false);
        }

        currentStroke = null;
        currentRb = null;
        beads.Clear();
        activeDrawArea = null;

        GameManager.Instance?.NotifyStrokeCompleted();
    }

    // ================== UTILS ==================

    public void ClearAllStrokes()
    {
        if (!strokesRoot) return;
        for (int i = strokesRoot.childCount - 1; i >= 0; i--)
            Destroy(strokesRoot.GetChild(i).gameObject);
    }

    IEnumerator FadeAndDisable(GameObject go, float dur)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (!sr || dur <= 0f)
        {
            go.SetActive(false);
            yield break;
        }

        Color c = sr.color;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(c.a, 0f, t / dur);
            sr.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        go.SetActive(false);
    }
}
