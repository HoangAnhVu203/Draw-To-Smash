using System.Collections.Generic;
using UnityEngine;
using System.Collections;


[DisallowMultipleComponent]
public class DrawFall : MonoBehaviour
{
    [Header("Camera (để trống sẽ tự lấy Main Camera)")]
    public Camera cam;

    [Header("Nét vẽ")]
    public float thickness = 0.25f;
    public float minPointDistance = 0.05f;
    public Color strokeColor = new(0.2f, 0.8f, 0.2f, 1f);
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
    public GameObject objectToEnable;         // hiệu ứng hoặc UI bật khi va chạm
    public float vanishDelay = 1f;            // trễ trước khi trứng biến mất

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

    void Awake()
    {
        if (!cam) cam = Camera.main;
        drawLayer = LayerMask.NameToLayer(drawLayerName);
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) TryBeginStroke(Input.mousePosition);
        if (Input.GetMouseButton(0))     TryContinueStroke(Input.mousePosition);
        if (Input.GetMouseButtonUp(0))   EndStroke();
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

    // --- VẼ CHỈ TRONG DrawArea ---

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
        var hit = Physics2D.OverlapPoint(worldPos);
        if (!hit) return null;
        return hit.gameObject.layer == drawLayer ? hit : null;
    }

    Vector2 ScreenToWorld(Vector2 screen) =>
        (Vector2)cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0));

    // --- TẠO NÉT (MESH + COLLIDER) ---

    void BeginStroke(Vector2 start)
{
    strokeGO = new GameObject("Line");
    strokeGO.transform.position = Vector3.zero;

    // gán layer "Line" cho nét vẽ
    int lineLayer = LayerMask.NameToLayer("Line");
    if (lineLayer != -1)
        strokeGO.layer = lineLayer;              
    else
        Debug.LogWarning("Layer 'Line' chưa tồn tại. Hãy tạo trong Project Settings → Tags and Layers."); // ← NEW

    mf = strokeGO.AddComponent<MeshFilter>();
    mr = strokeGO.AddComponent<MeshRenderer>();
    poly = strokeGO.AddComponent<PolygonCollider2D>();

    Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
    if (!s) s = Shader.Find("Sprites/Default");
    if (!s) s = Shader.Find("Unlit/Color");
    var mat = new Material(s) { color = strokeColor };
    mat.renderQueue = 3000;
    mr.material = mat;
    mr.sortingOrder = sortingOrder;

    mesh = new Mesh { name = "StrokeMeshRuntime" };
    mf.sharedMesh = mesh;

    pts.Clear(); verts.Clear(); tris.Clear(); uvs.Clear(); outline.Clear();
    AddPoint(start, true);
}


    void ContinueStroke(Vector2 p)
    {
        if (pts.Count == 0 || Vector2.Distance(pts[^1], p) >= minPointDistance)
            AddPoint(p, false);
    }

    void AddPoint(Vector2 p, bool first)
    {
        pts.Add(p);
        Vector2 dir = (pts.Count >= 2) ? (pts[^1] - pts[^2]).normalized : Vector2.right;
        Vector2 n = new(-dir.y, dir.x);
        float half = thickness * 0.5f;

        Vector2 left = p + n * half;
        Vector2 right = p - n * half;

        verts.Add(new Vector3(left.x, left.y, 0));
        verts.Add(new Vector3(right.x, right.y, 0));

        float v = pts.Count / 10f;
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
            rb = strokeGO.AddComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.WakeUp();

            var pm = new PhysicsMaterial2D("StrokePM") { friction = friction, bounciness = bounciness };
            poly.sharedMaterial = pm;

            // gắn trình xử lý va chạm
            var handler = strokeGO.AddComponent<StrokeCollisionHandler>();
            handler.vanishLayers = vanishLayers;
            handler.objectToEnable = objectToEnable;
            handler.vanishDelay = vanishDelay;
        }

        if (activeDrawArea)
        {
            if (fadeOutArea)
                StartCoroutine(FadeAndDisable(activeDrawArea.gameObject, fadeDuration));
            else
                activeDrawArea.gameObject.SetActive(false);
        }

        strokeGO = null; mf = null; mr = null; poly = null; mesh = null; rb = null;
        pts.Clear(); verts.Clear(); tris.Clear(); uvs.Clear(); outline.Clear();
        activeDrawArea = null;
    }

    System.Collections.IEnumerator FadeAndDisable(GameObject go, float dur)
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

//=================== XỬ LÝ VA CHẠM ===================//

public class StrokeCollisionHandler : MonoBehaviour
{
    [HideInInspector] public string[] vanishLayers;
    [HideInInspector] public GameObject objectToEnable;
    [HideInInspector] public float vanishDelay = 1f;

    // Ngăn hẹn giờ nhiều lần cho cùng 1 object
    private static readonly HashSet<int> scheduled = new HashSet<int>();

    void OnCollisionEnter2D(Collision2D col)
    {
        var target = col.gameObject;
        if (!IsInVanishLayers(target.layer)) return;

        int id = target.GetInstanceID();
        if (scheduled.Contains(id)) return;          // đã hẹn rồi → bỏ

        scheduled.Add(id);

        if (objectToEnable && !objectToEnable.activeSelf)
            objectToEnable.SetActive(true);

        StartCoroutine(DisableAfterDelay(target, id));
    }

    bool IsInVanishLayers(int layer)
    {
        if (vanishLayers == null) return false;
        for (int i = 0; i < vanishLayers.Length; i++)
            if (layer == LayerMask.NameToLayer(vanishLayers[i])) return true;
        return false;
    }

    IEnumerator DisableAfterDelay(GameObject target, int id)
    {
        float t = 0f;
        while (t < vanishDelay)
        {
            // target có thể đã bị Destroy/SetActive(false) ở nơi khác
            if (target == null)
            {
                scheduled.Remove(id);
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        // kiểm tra lần cuối trước khi thao tác
        if (target != null)
        {
            // nếu đã bị ẩn rồi thì thôi; nếu còn đang bật thì tắt
            if (target.activeInHierarchy)
                target.SetActive(false);
        }

        scheduled.Remove(id);
    }
}
