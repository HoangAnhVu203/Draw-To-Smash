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

    [Tooltip("Kiểm tra ‘theo dõi’ mỗi 0.5s (không quyết định kết quả)")]
    public float pollInterval = 0.5f;

    [Tooltip("Mốc thời gian chấm từ lúc bắt đầu gameplay (chỉ xét THẮNG nếu đến lúc đó không còn BadEgg)")]
    public float tFromStartToJudge = 10f;

    [Tooltip("Mốc thời gian chấm từ sau khi vẽ xong (xét THUA nếu còn BadEgg; xét THẮNG nếu hết)")]
    public float tAfterStrokeToJudge = 10f;

    public GameState state { get; private set; }

    int badEggLayer;
    bool strokeFinished = false;
    bool startJudgeDone = false;      // đã chấm ở mốc 10s từ đầu game?
    bool postStrokeJudgeDone = false; // đã chấm ở mốc 10s sau khi vẽ?

    Coroutine pollingCo;
    Coroutine startJudgeCo;
    Coroutine postStrokeJudgeCo;

    // ====== Thêm: quản lý BadEgg cho EggBreak ======
    readonly HashSet<EggBreak> badEggs = new HashSet<EggBreak>();

    void Start()
    {
        badEggLayer = LayerMask.NameToLayer(badEggLayerName);
        if (badEggLayer < 0)
            Debug.LogWarning($"[GameManager] Layer '{badEggLayerName}' chưa tồn tại.");

        // Bắt đầu poll “theo dõi” (không quyết định kết quả)
        pollingCo = StartCoroutine(PollBadEggs());

        // Đặt mốc 10s kể từ bắt đầu: chỉ THẮNG nếu lúc đó hết BadEgg
        startJudgeCo = StartCoroutine(JudgeFromStart());
    }

    // ======= Bảng quy tắc gốc =======
    // Gameplay bắt đầu:     Còn BadEgg  -> Poll mỗi 0.5s (không chấm)
    // Gameplay bắt đầu:     10s trôi qua không còn BadEgg -> Victory
    // Sau khi vẽ nét:       10s trôi qua vẫn còn BadEgg    -> Fail
    // Sau khi vẽ nét:       10s trôi qua không còn BadEgg  -> Victory
    // ================================

    // ================= API cho EggBreak =================

    /// <summary>
    /// Gọi trong EggBreak.Awake: đăng ký egg. Chỉ thêm nếu là BadEgg.
    /// Không thay đổi luật, chỉ hỗ trợ tối ưu / theo dõi.
    /// </summary>
    public void RegisterEgg(EggBreak egg, bool isBad)
    {
        if (!egg || !isBad) return;
        badEggs.Add(egg);
    }

    /// <summary>
    /// Gọi trong EggBreak.Break: khi egg vỡ.
    /// Nếu là BadEgg -> xoá khỏi danh sách.
    /// Gameplay gốc vẫn dùng ExistsActiveInLayer để quyết định.
    /// </summary>
    public void OnEggBroken(EggBreak egg)
    {
        if (egg == null) return;
        if (egg.isBadEgg)
            badEggs.Remove(egg);
    }

    // ================= Từ DrawManager =================

    public void NotifyStrokeCompleted()
    {
        if (state != GameState.Gameplay || strokeFinished) return;

        strokeFinished = true;
        if (drawController) drawController.enabled = false;

        // Khi đã vào pha “sau khi vẽ”, ta CHỈ chấm ở mốc 10s sau vẽ.
        // Huỷ mốc “10s kể từ đầu” nếu chưa dùng, để tránh xung đột.
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
            // Có thể dùng ExistsActiveInLayer(badEggLayer) hoặc badEggs.Count để debug
            // int count = ...;
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
            // chỉ khi chưa vẽ nét, đến mốc 10s mà hết BadEgg -> Victory
            if (!hasBadEgg)
                SetVictory();
            // còn BadEgg -> không làm gì, tiếp tục gameplay & đợi pha vẽ
        }
        // nếu đã kịp vẽ trước mốc này, kết quả sẽ do JudgeAfterStroke quyết định
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
    //
    // Giữ nguyên behavior cũ, nhưng:
    // - Nếu layer là BadEgg và có danh sách badEggs -> ưu tiên check từ đó.
    // - Nếu không có hoặc không còn active -> fallback sang FindObjectsOfType như cũ.
    //
    bool ExistsActiveInLayer(int layer)
    {
        // Ưu tiên dùng danh sách badEggs nếu đang check BadEggLayer
        if (layer == badEggLayer && badEggs.Count > 0)
        {
            bool has = false;
            var toRemove = new List<EggBreak>();

            foreach (var egg in badEggs)
            {
                if (!egg || !egg.gameObject.activeInHierarchy)
                {
                    toRemove.Add(egg);
                    continue;
                }
                has = true;
            }

            // dọn egg null/inactive
            for (int i = 0; i < toRemove.Count; i++)
                badEggs.Remove(toRemove[i]);

            if (has)
                return true; // vẫn còn ít nhất 1 BadEgg active
            // nếu không còn -> tiếp tục fallback FindObjectsOfType để giữ đúng behavior cũ
        }

        // Fallback: behavior gốc
        var gos = FindObjectsOfType<GameObject>();
        for (int i = 0; i < gos.Length; i++)
        {
            var go = gos[i];
            if (!go || !go.activeInHierarchy) continue;
            if (go.layer == layer) return true;
        }
        return false;
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
        // huỷ các coroutine cũ để tránh sót
        if (pollingCo != null) StopCoroutine(pollingCo);
        if (startJudgeCo != null) StopCoroutine(startJudgeCo);
        if (postStrokeJudgeCo != null) StopCoroutine(postStrokeJudgeCo);

        Time.timeScale = 1f;
        state = GameState.Gameplay;

        strokeFinished = false;
        startJudgeDone = false;
        postStrokeJudgeDone = false;

        badEggs.Clear(); // reset danh sách bad egg cho level mới

        if (drawController) drawController.enabled = true;

        if (victoryPanel) victoryPanel.SetActive(false);
        if (failPanel)    failPanel.SetActive(false);
        if (pausePanel)   pausePanel.SetActive(false);

        // khởi động lại poll + mốc “10s kể từ đầu gameplay”
        if (pollInterval > 0f)
            pollingCo = StartCoroutine(PollBadEggs());

        startJudgeCo = StartCoroutine(JudgeFromStart());
        postStrokeJudgeCo = null; // chỉ tạo khi NotifyStrokeCompleted()
    }
}
