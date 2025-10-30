using System.Collections;
using UnityEngine;

public enum GameState { Gameplay, Victory, Fail, Pause }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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

    // ======= Bảng quy tắc bạn yêu cầu =======
    // Gameplay bắt đầu:     Còn BadEgg  -> Poll mỗi 0.5s (không chấm)
    // Gameplay bắt đầu:     10s trôi qua không còn BadEgg -> Victory
    // Sau khi vẽ nét:       10s trôi qua vẫn còn BadEgg    -> Fail
    // Sau khi vẽ nét:       10s trôi qua không còn BadEgg  -> Victory
    // ========================================

    public void NotifyStrokeCompleted()
    {
        if (state != GameState.Gameplay || strokeFinished) return;

        strokeFinished = true;
        if (drawController) drawController.enabled = false;

        // Khi đã vào pha “sau khi vẽ”, ta CHỈ chấm ở mốc 10s sau vẽ.
        // Có thể huỷ kết quả “10s kể từ đầu” nếu muốn tránh xung đột:
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
            // Bạn có thể cập nhật UI đếm số trứng ở đây nếu muốn
            // int count = CountBadEggs();
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

    bool ExistsActiveInLayer(int layer)
    {
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
    }

    void SetFail()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Fail;
        Time.timeScale = 0f;
        UIManager.Instance.OpenUI<CanvasFail>();
    }

    public void Pause()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Pause;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (state != GameState.Pause) return;
        state = GameState.Gameplay;
        Time.timeScale = 1f;
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

        if (drawController) drawController.enabled = true;

        if (victoryPanel) victoryPanel.SetActive(false);
        if (failPanel)    failPanel.SetActive(false);
        if (pausePanel)   pausePanel.SetActive(false);

        // khởi động lại poll + mốc “10s kể từ đầu gameplay”
        pollingCo = StartCoroutine(PollBadEggs());
        startJudgeCo = StartCoroutine(JudgeFromStart());
        postStrokeJudgeCo = null; // chỉ tạo khi NotifyStrokeCompleted()
    }
}
