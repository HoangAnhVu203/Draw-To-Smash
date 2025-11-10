using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class DrawManager : MonoBehaviour
{
    public static DrawManager Instance { get; private set; }

    [Header("Camera (để trống sẽ tự lấy Main Camera)")]
    public Camera cam;

    [Header("Nơi chứa các nét vẽ (để rọn dẹp)")]
    public Transform strokesRoot;

    [Header("Nét vẽ cơ bản")]
    public float thickness = 0.25f;
    public float minPointDistance = 0.05f;
    public Color strokeColor = new(0.2f, 0.8f, 0.2f, 1f);
    public int sortingOrder = 200;

    [Header("Vật liệu / Texture cho nét vẽ kiểu hạt")]
    [Tooltip("Gán material có texture hình tròn xanh, WrapMode=Repeat để được chuỗi hạt.")]
    public Material strokeMaterial;          // NEW: nếu null sẽ fallback dùng màu solid
    [Tooltip("Hệ số lặp texture theo độ dài (tăng = hạt dày hơn)")]
    public float uvScale = 3f;               // NEW: chỉnh cho spacing hạt

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

    GameObject strokeGO;
    MeshFilter mf;
    MeshRenderer mr;
    PolygonCollider2D poly;
    Mesh mesh;
    Rigidbody2D rb;
    Collider2D activeDrawArea;

    readonly List<Vector2> pts = new();
    readonly List<Vector3> verts = new();
    readonly List<int> tris = new();
    readonly List<Vector2> uvs = new();
    readonly List<Vector2> outline = new();

    int drawLayer;
    float pathLength;               // NEW: tích lũy độ dài để map UV

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

    // ===== VẼ TRONG DrawArea =====

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
        if (!strokeGO || !activeDrawArea) return;
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
        (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0));

    // ===== TẠO NÉT =====

    void BeginStroke(Vector2 start)
    {
        strokeGO = new GameObject("Line");
        strokeGO.transform.SetParent(strokesRoot, false);
        strokeGO.transform.position = Vector3.zero;

        int lineLayer = LayerMask.NameToLayer("Line");
        if (lineLayer != -1) strokeGO.layer = lineLayer;
        else Debug.LogWarning("Layer 'Line' chưa tồn tại. Hãy tạo trong Project Settings → Tags and Layers.");

        mf = strokeGO.AddComponent<MeshFilter>();
        mr = strokeGO.AddComponent<MeshRenderer>();
        poly = strokeGO.AddComponent<PolygonCollider2D>();

        // NEW: dùng material custom nếu có, nếu không thì tạo material màu xanh như cũ
        if (strokeMaterial)
        {
            mr.material = new Material(strokeMaterial); // clone để không sửa shared
        }
        else
        {
            Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (!s) s = Shader.Find("Sprites/Default");
            if (!s) s = Shader.Find("Unlit/Color");
            var mat = new Material(s) { color = strokeColor };
            mat.renderQueue = 3000;
            mr.material = mat;
        }
        mr.sortingOrder = sortingOrder;

        mesh = new Mesh { name = "StrokeMeshRuntime" };
        mf.sharedMesh = mesh;

        pts.Clear(); verts.Clear(); tris.Clear(); uvs.Clear(); outline.Clear();
        pathLength = 0f;                    // NEW
        AddPoint(start, true);
    }

    void ContinueStroke(Vector2 p)
    {
        if (pts.Count == 0 || Vector2.Distance(pts[^1], p) >= minPointDistance)
            AddPoint(p, false);
    }

    void AddPoint(Vector2 p, bool first)
    {
        // length cho UV
        if (pts.Count > 0)
            pathLength += Vector2.Distance(pts[^1], p);

        pts.Add(p);

        Vector2 dir = (pts.Count >= 2) ? (pts[^1] - pts[^2]).normalized : Vector2.right;
        Vector2 n = new(-dir.y, dir.x);
        float half = thickness * 0.5f;

        Vector2 left = p + n * half;
        Vector2 right = p - n * half;

        verts.Add(new Vector3(left.x, left.y, 0));
        verts.Add(new Vector3(right.x, right.y, 0));

        // NEW: UV theo độ dài -> texture lặp thành chuỗi hạt
        float v = pathLength * (uvScale * 0.2f);
        uvs.Add(new Vector2(0, v));
        uvs.Add(new Vector2(1, v));

        if (!first && verts.Count >= 4)
        {
            int i = verts.Count - 1;
            int i0 = i - 3, i1 = i - 2, i2 = i - 1;
            tris.Add(i0); tris.Add(i1); tris.Add(i2);
            tris.Add(i2); tris.Add(i1); tris.Add(i);
        }

        RebuildOutline();

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        if (outline.Count >= 3)
            poly.SetPath(0, outline.ToArray());
    }

    void RebuildOutline()
    {
        int pair = verts.Count / 2;
        if (pair < 2) return;
        var lefts = new List<Vector2>(pair);
        var rights = new List<Vector2>(pair);
        for (int i = 0; i < pair; i++)
        {
            lefts.Add(new Vector2(verts[i * 2].x, verts[i * 2].y));
            rights.Add(new Vector2(verts[i * 2 + 1].x, verts[i * 2 + 1].y));
        }
        outline.Clear();
        outline.AddRange(lefts);
        rights.Reverse();
        outline.AddRange(rights);
    }

    void EndStroke()
    {
        if (!strokeGO) { activeDrawArea = null; return; }

        if (pts.Count < 2 || verts.Count < 4)
        {
            Destroy(strokeGO);
        }
        else
        {
            // Re-center mesh quanh pivot
            Vector3 center = Vector3.zero;
            for (int i = 0; i < verts.Count; i++) center += verts[i];
            center /= verts.Count;

            for (int i = 0; i < verts.Count; i++) verts[i] -= center;
            for (int i = 0; i < outline.Count; i++) outline[i] -= (Vector2)center;

            strokeGO.transform.position = center;

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            if (outline.Count >= 3)
                poly.SetPath(0, outline.ToArray());

            rb = strokeGO.AddComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.WakeUp();

            var pm = new PhysicsMaterial2D("StrokePM") { friction = friction, bounciness = bounciness };
            poly.sharedMaterial = pm;

            //var handler = strokeGO.AddComponent<StrokeCollisionHandler>();
            //handler.vanishLayers = vanishLayers;
            //handler.objectToEnable = objectToEnable;
            //handler.vanishDelay = vanishDelay;
        }

        if (activeDrawArea)
        {
            if (fadeOutArea) StartCoroutine(FadeAndDisable(activeDrawArea.gameObject, fadeDuration));
            else activeDrawArea.gameObject.SetActive(false);
        }

        strokeGO = null; mf = null; mr = null; poly = null; mesh = null; rb = null;
        pts.Clear(); verts.Clear(); tris.Clear(); uvs.Clear(); outline.Clear();
        activeDrawArea = null;

        GameManager.Instance?.NotifyStrokeCompleted();
    }

    public void ClearAllStrokes()
    {
        if (!strokesRoot) return;
        for (int i = strokesRoot.childCount - 1; i >= 0; i--)
            Destroy(strokesRoot.GetChild(i).gameObject);
        Debug.Log("[DrawManager] Cleared all strokes.");
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
