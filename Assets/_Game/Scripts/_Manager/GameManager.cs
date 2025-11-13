using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Gameplay, Victory, Fail, Pause }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        state = GameState.Gameplay;
    }

    [Header("UI (optional)")]
    public GameObject victoryPanel;
    public GameObject failPanel;
    public GameObject pausePanel;

    [Header("Vẽ nét (bị khoá sau khi vẽ xong)")]
    public MonoBehaviour drawController;

    [Header("Thông số luật")]
    [Tooltip("Tên layer của BadEgg")]
    public string badEggLayerName = "BadEgg";
    [Tooltip("Tên layer của GoodEgg (để bật luật mới)")]
    public string goodEggLayerName = "GoodEgg"; 

    [Tooltip("Kiểm tra ‘theo dõi’ mỗi 0.5s (không quyết định kết quả)")]
    public float pollInterval = 0.5f;

    [Tooltip("Mốc thời gian chấm từ lúc bắt đầu gameplay (chỉ xét THẮNG nếu đến lúc đó không còn BadEgg)")]
    public float tFromStartToJudge = 10f;

    [Tooltip("Mốc thời gian chấm từ sau khi vẽ xong (xét THUA nếu còn BadEgg; xét THẮNG nếu hết)")]
    public float tAfterStrokeToJudge = 10f;

    public GameState state { get; private set; }

    int badEggLayer;
    int goodEggLayer;                   // NEW: GoodEgg
    bool strokeFinished = false;
    bool startJudgeDone = false;
    bool postStrokeJudgeDone = false;

    Coroutine pollingCo;
    Coroutine startJudgeCo;
    Coroutine postStrokeJudgeCo;

    // ====== Quản lý Egg cho EggBreak ======
    readonly HashSet<EggBreak> badEggs  = new HashSet<EggBreak>();
    readonly HashSet<EggBreak> goodEggs = new HashSet<EggBreak>();   
    bool hasGoodEggInLevel = false;                                   

    void Start()
    {
        badEggLayer  = LayerMask.NameToLayer(badEggLayerName);
        if (badEggLayer < 0)
            Debug.LogWarning($"[GameManager] Layer '{badEggLayerName}' chưa tồn tại.");

        goodEggLayer = LayerMask.NameToLayer(goodEggLayerName);       
        if (goodEggLayer < 0)
            Debug.LogWarning($"[GameManager] Layer '{goodEggLayerName}' chưa tồn tại.");

        // Poll chỉ để debug/theo dõi
        if (pollInterval > 0f)
            pollingCo = StartCoroutine(PollBadEggs());

        // Mốc 10s kể từ bắt đầu: chỉ THẮNG nếu hết BadEgg
        startJudgeCo = StartCoroutine(JudgeFromStart());
    }

    // ================= API cho EggBreak =================

    /// <summary>
    /// Gọi trong EggBreak.Awake: đăng ký egg.
    /// </summary>
    public void RegisterEgg(EggBreak egg, bool isBad)
    {
        if (!egg) return;

        if (isBad)
        {
            badEggs.Add(egg);
        }
        else
        {
            goodEggs.Add(egg);          
            hasGoodEggInLevel = true;  
        }
    }

    /// <summary>
    /// Gọi trong EggBreak.Break: khi egg vỡ.
    /// </summary>
    public void OnEggBroken(EggBreak egg)
    {
        if (egg == null) return;

        if (egg.isBadEgg)
        {
            badEggs.Remove(egg);
        }
        else
        {
            // NEW: Nếu level có GoodEgg, bất kỳ GoodEgg nào vỡ → THUA NGAY
            goodEggs.Remove(egg);

            if (hasGoodEggInLevel && state == GameState.Gameplay)
            {
                SetFail();
                return;
            }
        }
    }

    // ================= Từ DrawManager =================

    public void NotifyStrokeCompleted()
    {
        if (state != GameState.Gameplay || strokeFinished) return;

        strokeFinished = true;
        if (drawController) drawController.enabled = false;

        if (startJudgeCo != null && !startJudgeDone)
        {
            StopCoroutine(startJudgeCo);
            startJudgeCo = null;
        }

        if (postStrokeJudgeCo == null)
            postStrokeJudgeCo = StartCoroutine(JudgeAfterStroke());
    }

    // -------- Poll: chỉ theo dõi, không quyết định kết quả --------
    IEnumerator PollBadEggs()
    {
        while (state == GameState.Gameplay)
        {
            // Có thể log đếm còn bao nhiêu trứng để debug
            // Debug.Log($"Bad:{badEggs.Count} Good:{goodEggs.Count}");
            yield return new WaitForSecondsRealtime(pollInterval);
        }
    }

    // -------- Mốc 10s kể từ BẮT ĐẦU gameplay: chỉ xét THẮNG --------
    IEnumerator JudgeFromStart()
    {
        yield return new WaitForSecondsRealtime(tFromStartToJudge);
        if (state != GameState.Gameplay) yield break;

        startJudgeDone = true;

        bool hasBadEgg = ExistsActiveInLayer(badEggLayer);
        if (!strokeFinished)
        {
            if (!hasBadEgg)
                SetVictory();
            // còn BadEgg -> chờ pha sau khi vẽ
        }
    }

    // -------- Mốc 10s kể từ SAU KHI VẼ: xét THẮNG/THUA --------
    IEnumerator JudgeAfterStroke()
    {
        yield return new WaitForSecondsRealtime(tAfterStrokeToJudge);
        if (state != GameState.Gameplay) yield break;

        postStrokeJudgeDone = true;

        bool hasBadEgg = ExistsActiveInLayer(badEggLayer);
        if (hasBadEgg) SetFail();
        else SetVictory();
    }

    // ================= ExistsActiveInLayer =================
    bool ExistsActiveInLayer(int layer)
    {
        // Ưu tiên dùng set nếu khớp layer
        if (layer == badEggLayer && badEggs.Count > 0)
        {
            CleanSet(badEggs);
            if (badEggs.Count > 0) return true;
        }

        if (layer == goodEggLayer && goodEggs.Count > 0) // NEW
        {
            CleanSet(goodEggs);
            if (goodEggs.Count > 0) return true;
        }

        // Fallback
        var gos = FindObjectsOfType<GameObject>();
        for (int i = 0; i < gos.Length; i++)
        {
            var go = gos[i];
            if (!go || !go.activeInHierarchy) continue;
            if (go.layer == layer) return true;
        }
        return false;
    }

    void CleanSet(HashSet<EggBreak> set)
    {
        var tmp = ListPool<EggBreak>.Get();
        foreach (var e in set)
        {
            if (!e || !e.gameObject.activeInHierarchy)
                tmp.Add(e);
        }
        for (int i = 0; i < tmp.Count; i++) set.Remove(tmp[i]);
        ListPool<EggBreak>.Release(tmp);
    }

    // ---------------- State change ----------------
    void SetVictory()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Victory;
        Time.timeScale = 0f;
        UIManager.Instance.OpenUI<CanvasVictory>();

        if (victoryPanel) victoryPanel.SetActive(true);
        if (failPanel)    failPanel.SetActive(false);
    }

    void SetFail()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Fail;
        Time.timeScale = 0f;
        UIManager.Instance.OpenUI<CanvasFail>();

        if (failPanel)    failPanel.SetActive(true);
        if (victoryPanel) victoryPanel.SetActive(false);
    }

    public void Pause()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Pause;
        Time.timeScale = 0f;
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (state != GameState.Pause) return;
        state = GameState.Gameplay;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
    }

    public void ReplayLevel()
    {
        Time.timeScale = 1f;
        DrawManager.Instance?.ClearAllStrokes();
        LevelManager.Instance?.Replay();
    }

    public void NextLevel()
    {
        Time.timeScale = 1f;
        DrawManager.Instance?.ClearAllStrokes();
        LevelManager.Instance?.NextLevel();
    }

    // Gọi từ LevelManager sau khi load level mới
    public void ResetForNewLevel()
    {
        if (pollingCo != null) StopCoroutine(pollingCo);
        if (startJudgeCo != null) StopCoroutine(startJudgeCo);
        if (postStrokeJudgeCo != null) StopCoroutine(postStrokeJudgeCo);

        Time.timeScale = 1f;
        state = GameState.Gameplay;

        strokeFinished = false;
        startJudgeDone = false;
        postStrokeJudgeDone = false;

        badEggs.Clear();
        goodEggs.Clear();           // NEW
        hasGoodEggInLevel = false;  // NEW

        if (drawController) drawController.enabled = true;

        if (victoryPanel) victoryPanel.SetActive(false);
        if (failPanel)    failPanel.SetActive(false);
        if (pausePanel)   pausePanel.SetActive(false);

        if (pollInterval > 0f)
            pollingCo = StartCoroutine(PollBadEggs());
        startJudgeCo = StartCoroutine(JudgeFromStart());
        postStrokeJudgeCo = null;
    }
}

/// <summary>
/// ListPool đơn giản để tránh GC khi dọn set (có thể bỏ nếu không cần).
/// </summary>
static class ListPool<T>
{
    static readonly Stack<List<T>> pool = new Stack<List<T>>();
    public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>();
    public static void Release(List<T> list) { list.Clear(); pool.Push(list); }
}
