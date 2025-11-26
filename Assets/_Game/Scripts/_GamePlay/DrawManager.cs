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

    [Header("Layer cha để attach stroke (Lift, Gears ...)")]
    public string[] attachParentLayers = { "Lift", "Gears" };

    [Header("Layer chặn vẽ")]
    public string[] blockDrawLayers = { "Default", "Fence", "Lift" };

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

    // ❌ BỎ OpenUI Gameplay ở đây, UI do GameManager xử lý
    void Start() { }

    void Update()
    {
        // Nếu có GameManager thì chỉ cho vẽ khi đang Gameplay
        if (GameManager.Instance != null)
        {
            var s = GameManager.Instance.CurrentState;
            if (s != GameState.Gameplay)
                return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) TryBeginStroke(Input.mousePosition);
        if (Input.GetMouseButton(0)) TryContinueStroke(Input.mousePosition);
        if (Input.GetMouseButtonUp(0)) EndStroke();
#else
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)      TryBeginStroke(t.position);
            if (t.phase == TouchPhase.Moved ||
                t.phase == TouchPhase.Stationary) TryContinueStroke(t.position);
            if (t.phase == TouchPhase.Ended)      EndStroke();
        }
#endif
    }

    // =========================================================
    //     API PUBLIC cho DEMO (tay tự vẽ dùng world position)
    // =========================================================

    /// <summary>
    /// Bắt đầu stroke từ world position (dùng cho tay DEMO).
    /// </summary>
    public void StartStroke(Vector3 worldPos)
    {
        // Nếu đang có stroke khác -> bỏ qua (cho đơn giản)
        if (currentStroke != null) return;

        Vector2 pos = worldPos;

        // Vẫn tôn trọng DrawArea (nếu path đặt đúng trong vùng vẽ)
        var col = HitDrawArea(pos);
        if (!col) return;

        activeDrawArea = col;
        BeginStroke(pos);
    }

    /// <summary>
    /// Thêm điểm vào stroke từ world position (dùng cho tay DEMO).
    /// </summary>
    public void AddStrokePoint(Vector3 worldPos)
    {
        if (!currentStroke) return;

        Vector2 pos = worldPos;

        // Nếu có DrawArea, đảm bảo tay vẫn trong cùng vùng
        if (activeDrawArea)
        {
            var col = HitDrawArea(pos);
            if (col != activeDrawArea) return;
        }

        ContinueStroke(pos);
    }

    // EndStroke() bên dưới dùng chung cho cả input người chơi và DEMO

    // ================== DRAW AREA (INPUT NGƯỜI CHƠI) ==================

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
        // Lấy tất cả collider ở đúng điểm chạm (mọi layer)
        var hits = Physics2D.OverlapPointAll(worldPos);
        Collider2D draw = null;
        bool blocked = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (!c || !c.gameObject.activeInHierarchy || !c.enabled) continue;

            int l = c.gameObject.layer;
            if (l == drawLayer) draw = c;               // có DrawArea tại điểm
            else if (IsInBlockList(l)) blocked = true;  // có vật “che” tại cùng điểm
        }

        // Nếu có vật chặn → coi như không thể vẽ
        if (blocked) return null;
        return draw; // chỉ trả về DrawArea khi không bị chặn
    }

    Vector2 ScreenToWorld(Vector2 screen) =>
        (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));

    // ================== STROKE CORE ==================

    // private: core tạo stroke
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

        if (HintSystem.Instance != null)
        {
            HintSystem.Instance.HideHint();
        }

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

    public void EndStroke()
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
            // handler.vanishLayers   = vanishLayers;
            // handler.objectToEnable = objectToEnable;
            // handler.vanishDelay    = vanishDelay;

            var attach = currentStroke.AddComponent<StrokeAttachToLift>();
            attach.Init(attachParentLayers);
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
        // GameObject đã bị huỷ từ trước
        if (!go) yield break;

        var sr = go.GetComponent<SpriteRenderer>();

        // Không có SpriteRenderer hoặc thời gian fade <= 0
        if (!sr || dur <= 0f)
        {
            if (go) go.SetActive(false);
            yield break;
        }

        Color startColor = sr.color;
        float t = 0f;

        while (t < dur)
        {
            // Nếu trong lúc fade object / renderer bị Destroy -> dừng luôn
            if (!go || !sr)
                yield break;

            t += Time.deltaTime;
            float a = Mathf.Lerp(startColor.a, 0f, t / dur);

            // Kiểm tra thêm phòng trường hợp sr bị null đúng frame này
            if (sr)
                sr.color = new Color(startColor.r, startColor.g, startColor.b, a);

            yield return null;
        }

        if (go)
        {
            if (sr)
                sr.color = new Color(startColor.r, startColor.g, startColor.b, 0f);

            go.SetActive(false);
        }
    }


    bool IsInBlockList(int layer)
    {
        for (int i = 0; i < blockDrawLayers.Length; i++)
            if (LayerMask.NameToLayer(blockDrawLayers[i]) == layer)
                return true;
        return false;
    }
}
