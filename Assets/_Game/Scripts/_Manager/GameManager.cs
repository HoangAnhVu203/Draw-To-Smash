using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    Demo,
    Gameplay,
    Victory,
    Fail,
    Pause
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState state { get; private set; }
    public GameState CurrentState => state;   // cho script khác dùng

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        state = GameState.Gameplay; // sẽ bị override trong Start nếu là DEMO
    }

    [Header("UI (optional)")]
    public GameObject victoryPanel;
    public GameObject failPanel;
    public GameObject pausePanel;

    [Header("Vẽ nét (bị khoá sau khi vẽ xong)")]
    public MonoBehaviour drawController;

    [Header("Thông số luật (Bad/Good Egg)")]
    [Tooltip("Tên layer của BadEgg")]
    public string badEggLayerName = "BadEgg";
    [Tooltip("Tên layer của GoodEgg (vỡ là thua ngay)")]
    public string goodEggLayerName = "GoodEgg";

    [Tooltip("Kiểm tra ‘theo dõi’ mỗi 0.5s (không quyết định kết quả)")]
    public float pollInterval = 0.5f;

    [Tooltip("Mốc thời gian chấm từ lúc bắt đầu gameplay (chỉ xét THẮNG nếu đến lúc đó không còn BadEgg)")]
    public float tFromStartToJudge = 10f;

    [Tooltip("Mốc thời gian chấm từ sau khi vẽ xong (xét THUA nếu còn BadEgg; xét THẮNG nếu hết)")]
    public float tAfterStrokeToJudge = 10f;

    public AudioClip winSFX;
    public AudioClip failSFX;

    int badEggLayer;
    int goodEggLayer;
    bool strokeFinished = false;
    bool startJudgeDone = false;
    bool postStrokeJudgeDone = false;

    [Header("Debug")]
    [Tooltip("Ép lúc start luôn chạy demo, bỏ qua save (chỉ dùng để test)")]
    public bool forceDemoOnStart = false;

    Coroutine pollingCo;
    Coroutine startJudgeCo;
    Coroutine postStrokeJudgeCo;

    // ====== Quản lý Egg cho EggBreak ======
    readonly HashSet<EggBreak> badEggs = new HashSet<EggBreak>();
    readonly HashSet<EggBreak> goodEggs = new HashSet<EggBreak>();
    bool hasGoodEggInLevel = false;

    public event Action<float> OnPostStrokeTimerTick; // gửi remaining time (giây)
    public event Action OnPostStrokeTimerStart;
    public event Action OnPostStrokeTimerEnd;

    // =============================================
    //                DEMO SETTINGS
    // =============================================

    [Header("Demo Settings")]
    [Tooltip("Index level dùng để demo (thường là 0 hoặc 0,1)")]
    public int[] demoLevelIndices = { 0 };

    [Tooltip("Thời gian dừng mỗi level demo trước khi chuyển")]
    public float demoSwitchDelay = 11f;

    [Tooltip("Level thật đầu tiên sau demo (ví dụ: 1 hoặc 2 tuỳ bạn)")]
    public int firstRealLevelIndex = 1;

    bool firstStart = true;    // true = lần vào game đầu tiên
    Coroutine demoLoopCo;
    int demoSlot = 0;

    // =============================================
    //                    START
    // =============================================
    void Start()
    {
        var lm = LevelManager.Instance;

        badEggLayer = LayerMask.NameToLayer(badEggLayerName);
        if (badEggLayer < 0)
            Debug.LogWarning($"[GameManager] Layer '{badEggLayerName}' chưa tồn tại.");

        goodEggLayer = LayerMask.NameToLayer(goodEggLayerName);
        if (goodEggLayer < 0)
            Debug.LogWarning($"[GameManager] Layer '{goodEggLayerName}' chưa tồn tại.");

        // ===== CASE 0: DEV ép luôn chạy DEMO (bỏ qua save) =====
        if (forceDemoOnStart)
        {
            PlayerPrefs.DeleteKey(LevelManager.PP_LEVEL_INDEX);
            PlayerPrefs.Save();

            state = GameState.Demo;
            firstStart = true;


            StartCoroutine(WaitStartDemo());
            return;
        }

        // ===== Lấy flag saveProgress từ LevelManager =====
        bool saveProgress = lm.saveProgress;

        // ============================
        // CASE 1: TẮT saveProgress
        // ============================
        // → Luôn chạy DEMO như game mới
        // → Đồng thời xoá LV_INDEX để nếu sau này bật lại saveProgress
        //   thì sẽ KHÔNG có save cũ => chạy demo lại.
        if (!saveProgress)
        {
            PlayerPrefs.DeleteKey(LevelManager.PP_LEVEL_INDEX);
            PlayerPrefs.Save();

            state = GameState.Demo;
            firstStart = true;

            StartCoroutine(WaitStartDemo());
            return;
        }

        // ============================
        // CASE 2: BẬT saveProgress
        // ============================
        bool hasSave = PlayerPrefs.HasKey(LevelManager.PP_LEVEL_INDEX);

        // 2A. Bật saveProgress nhưng CHƯA CÓ SAVE nào
        //     -> luôn chạy DEMO
        if (!hasSave)
        {
            state = GameState.Demo;
            firstStart = true;


            StartCoroutine(WaitStartDemo());
            return;
        }

        // 2B. Bật saveProgress + ĐÃ CÓ SAVE
        //     -> nhảy thẳng vào level đã chơi dở
        firstStart = false;
        state = GameState.Gameplay;

        int index = PlayerPrefs.GetInt(LevelManager.PP_LEVEL_INDEX);
        index = Mathf.Clamp(index, 0, Mathf.Max(0, lm.levels.Count - 1));

        LevelManager.Instance.LoadLevel(index);
        // ResetForNewLevel sẽ mở CanvasGamePlay và setup timer/state
        // nhưng để chắc ăn ta vẫn mở một lần ở đây (không sao):
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }


    void Update()
    {
        // Bỏ qua check thắng nếu không phải gameplay
        if (state != GameState.Gameplay) return;

        // Với game trứng: thắng/thua do EggBreak + timer quyết
        // Nếu muốn auto-thắng kiểu layer, có thể thêm vào đây.
    }

    // ================= API cho EggBreak =================

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

    public void OnEggBroken(EggBreak egg)
    {
        if (egg == null) return;

        if (egg.isBadEgg)
        {
            badEggs.Remove(egg);
        }
        else
        {
            goodEggs.Remove(egg);

            // Nếu level có GoodEgg, bất kỳ GoodEgg nào vỡ → THUA NGAY
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
        float duration = tAfterStrokeToJudge;
        float t = 0f;

        OnPostStrokeTimerStart?.Invoke();

        while (t < duration)
        {
            if (state != GameState.Gameplay)
            {
                OnPostStrokeTimerEnd?.Invoke();
                yield break;
            }

            t += Time.unscaledDeltaTime;
            float remaining = Mathf.Max(0f, duration - t);
            OnPostStrokeTimerTick?.Invoke(remaining);

            yield return null;
        }

        OnPostStrokeTimerEnd?.Invoke();

        if (state != GameState.Gameplay) yield break;

        postStrokeJudgeDone = true;

        bool hasBadEgg = ExistsActiveInLayer(badEggLayer);
        if (hasBadEgg) SetFail();
        else SetVictory();
    }

    // ================= ExistsActiveInLayer =================
    bool ExistsActiveInLayer(int layer)
    {
        if (layer == badEggLayer && badEggs.Count > 0)
        {
            CleanSet(badEggs);
            if (badEggs.Count > 0) return true;
        }

        if (layer == goodEggLayer && goodEggs.Count > 0)
        {
            CleanSet(goodEggs);
            if (goodEggs.Count > 0) return true;
        }

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
        OnPostStrokeTimerEnd?.Invoke();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(winSFX);

        Time.timeScale = 0f;
        UIManager.Instance.OpenUI<CanvasVictory>();

        if (victoryPanel) victoryPanel.SetActive(true);
        if (failPanel) failPanel.SetActive(false);
    }

    void SetFail()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Fail;
        OnPostStrokeTimerEnd?.Invoke();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(failSFX);

        Time.timeScale = 0f;
        UIManager.Instance.OpenUI<CanvasFail>();

        if (failPanel) failPanel.SetActive(true);
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
        // Nếu đang DEMO → không chạy logic timer/thắng thua,
        // chỉ đảm bảo timeScale & UI đúng.
        if (state == GameState.Demo)
        {
            Time.timeScale = 1f;

            strokeFinished = false;
            startJudgeDone = false;
            postStrokeJudgeDone = false;

            badEggs.Clear();
            goodEggs.Clear();
            hasGoodEggInLevel = false;

            if (drawController) drawController.enabled = false;

            if (victoryPanel) victoryPanel.SetActive(false);
            if (failPanel) failPanel.SetActive(false);
            if (pausePanel) pausePanel.SetActive(false);

            UIManager.Instance.CloseUIDirectly<CanvasGamePlay>();
            UIManager.Instance.OpenUI<PanelDemo>();
            return;
        }

        // ------ GAMEPLAY BÌNH THƯỜNG ------
        if (pollingCo != null) StopCoroutine(pollingCo);
        if (startJudgeCo != null) StopCoroutine(startJudgeCo);
        if (postStrokeJudgeCo != null) StopCoroutine(postStrokeJudgeCo);

        Time.timeScale = 1f;
        state = GameState.Gameplay;

        strokeFinished = false;
        startJudgeDone = false;
        postStrokeJudgeDone = false;

        badEggs.Clear();
        goodEggs.Clear();
        hasGoodEggInLevel = false;

        if (drawController) drawController.enabled = true;

        if (victoryPanel) victoryPanel.SetActive(false);
        if (failPanel) failPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);

        UIManager.Instance.OpenUI<CanvasGamePlay>();

        if (pollInterval > 0f)
            pollingCo = StartCoroutine(PollBadEggs());
        startJudgeCo = StartCoroutine(JudgeFromStart());
        postStrokeJudgeCo = null;
    }

    // =============================================
    //                 DEMO FLOW
    // =============================================

    IEnumerator WaitStartDemo()
    {
        // nếu muốn có loading animation thì để > 0
        yield return new WaitForSeconds(0f);
        StartDemo();
    }

    void StartDemo()
    {
        state = GameState.Demo;
        Time.timeScale = 1f;
        firstStart = true;
        demoSlot = 0;

        LoadCurrentDemoLevel();

        if (demoLoopCo != null) StopCoroutine(demoLoopCo);
        demoLoopCo = StartCoroutine(DemoLoop());
    }

    void LoadCurrentDemoLevel()
    {
        if (demoLevelIndices == null || demoLevelIndices.Length == 0)
            return;

        int lvIndex = demoLevelIndices[Mathf.Clamp(demoSlot, 0, demoLevelIndices.Length - 1)];

        LevelManager.Instance.LoadLevel(lvIndex);

        UIManager.Instance.CloseUIDirectly<CanvasGamePlay>();
        UIManager.Instance.CloseUIDirectly<CanvasVictory>();
        UIManager.Instance.CloseUIDirectly<CanvasFail>();

        UIManager.Instance.OpenUI<PanelDemo>();
    }

    IEnumerator DemoLoop()
    {
        while (state == GameState.Demo)
        {
            yield return new WaitForSeconds(demoSwitchDelay);

            demoSlot = (demoSlot + 1) % demoLevelIndices.Length;
            LoadCurrentDemoLevel();
        }
    }

    // Gọi từ PanelDemo button "Play" / "Tap to Play"
    public void StartRealGame()
    {
        if (demoLoopCo != null)
        {
            StopCoroutine(demoLoopCo);
            demoLoopCo = null;
        }

        firstStart = false;
        state = GameState.Gameplay;

        UIManager.Instance.CloseUIDirectly<PanelDemo>();

        int targetIndex = Mathf.Clamp(
            firstRealLevelIndex,
            0,
            LevelManager.Instance.levels.Count - 1
        );

        LevelManager.Instance.LoadLevel(targetIndex);
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }
}

/// <summary>
/// ListPool đơn giản để tránh GC khi dọn set (giữ nguyên như cũ)
/// </summary>
static class ListPool<T>
{
    static readonly Stack<List<T>> pool = new Stack<List<T>>();
    public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>();
    public static void Release(List<T> list) { list.Clear(); pool.Push(list); }
}
