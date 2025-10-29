using System.Collections;
using UnityEngine;

public enum GameState { Gameplay, Victory, Fail, Pause }

public class GameManager : MonoBehaviour
{
    // ====== Singleton (đơn giản) ======
    public static GameManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Time.timeScale = 1f;
        state = GameState.Gameplay;
    }

    [Header("Refs (optional UI)")]
    public GameObject victoryPanel;
    public GameObject failPanel;
    public GameObject pausePanel;

    [Header("Vẽ nét")]
    [Tooltip("Script điều khiển vẽ (VD: DrawFall). Sẽ bị disable sau khi vẽ xong.")]
    public MonoBehaviour drawController;

    [Header("Luật chơi")]
    public float checkDelayAfterStroke = 10f;
    [Tooltip("Tên Layer cần còn lại để tính THUA")]
    public string badEggLayerName = "BadEgg";

    public GameState state { get; private set; }

    bool strokeFinished = false;
    bool checkingRoutineStarted = false;
    int badEggLayer;

    void Start()
    {
        badEggLayer = LayerMask.NameToLayer(badEggLayerName);
        if (badEggLayer < 0)
            Debug.LogWarning($"[GameManager] Layer '{badEggLayerName}' chưa tồn tại.");
             
    }

    // ========== API được gọi từ bên khác ==========
    /// <summary>Gọi 1 lần khi người chơi KẾT THÚC vẽ nét đầu tiên.</summary>
    public void NotifyStrokeCompleted()
    {
        if (state != GameState.Gameplay || strokeFinished) return;

        strokeFinished = true;

        // Khoá vẽ: chỉ cho 1 nét
        if (drawController) drawController.enabled = false;

        // Bắt đầu đếm ngược 10s rồi chấm điểm
        if (!checkingRoutineStarted)
            StartCoroutine(CheckWinLoseAfterDelay(checkDelayAfterStroke));
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

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ========== Core ==========

    IEnumerator CheckWinLoseAfterDelay(float t)
    {
        checkingRoutineStarted = true;
        float timer = 0f;
        while (timer < t && state == GameState.Gameplay)
        {
            timer += Time.unscaledDeltaTime; // đếm theo thời gian thực
            yield return null;
        }
        if (state != GameState.Gameplay) yield break;

        bool stillHasBadEgg = ExistsActiveInLayer(badEggLayer);

        if (stillHasBadEgg) SetFail();
        else SetVictory();
    }

    bool ExistsActiveInLayer(int layer)
    {
        // chỉ quét object đang active trong scene
        var gos = FindObjectsOfType<GameObject>();
        for (int i = 0; i < gos.Length; i++)
        {
            if (!gos[i].activeInHierarchy) continue;
            if (gos[i].layer == layer) return true;
        }
        return false;
    }

    void SetVictory()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Victory;
        Time.timeScale = 0f;
        UIManager.Instance.OpenUI<CanvasVictory>();
        
        Debug.Log("[GameManager] VICTORY!");
    }

    void SetFail()
    {
        if (state != GameState.Gameplay) return;
        state = GameState.Fail;
        Time.timeScale = 0f;
        UIManager.Instance.OpenUI<CanvasFail>();
        
        Debug.Log("[GameManager] FAIL!");
    }

    
}
