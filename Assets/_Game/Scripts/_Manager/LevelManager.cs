using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelManager : MonoBehaviour
{
    [Serializable]
    public class LevelEntry
    {
        public string id;
        public GameObject prefab;   // Prefab level
    }

    public static LevelManager Instance { get; private set; }

    [Header("Danh sách level (kéo prefab vào)")]
    public List<LevelEntry> levels = new List<LevelEntry>();

    [Header("Nơi spawn level (trống = dùng this.transform)")]
    public Transform levelRoot;

    [Header("Thiết lập khởi động / lưu tiến trình")]
    [Tooltip("Có lưu tiến trình level hay không")]
    public bool saveProgress = true;

    [Tooltip("Qua level cuối có quay lại không")]
    public bool loopAtEnd = false;      // true = quay lại LV đầu

    [Tooltip("Level thật đầu tiên (bỏ qua demo). Ví dụ: 2 nếu LV0,1 là demo")]
    public int defaultStartIndex = 1;

    // KEY dùng chung với GameManager (DEMO)
    public const string PP_LEVEL_INDEX = "LV_INDEX";

    public int CurrentIndex { get; private set; } = -1;
    public GameObject CurrentLevelGO { get; private set; }

    public event Action<GameObject, int> OnLevelLoaded;   // (levelGO, index)
    public event Action<int> OnLevelUnloaded;             // (index cũ)

    [Header("Nơi chứa đối tượng runtime để dọn khi đổi level")]
    public Transform runtimeRoot;

    int eggFragLayer = -1;

    public Transform RuntimeRoot => runtimeRoot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!levelRoot) levelRoot = transform;

        if (!runtimeRoot)
        {
            var rt = new GameObject("__RuntimeRoot");
            runtimeRoot = rt.transform;
            runtimeRoot.SetParent(transform, false);
        }

        // Layer mảnh vỡ trứng (EggFrag)
        eggFragLayer = LayerMask.NameToLayer("EggFrag");
    }

    // ❌ KHÔNG auto LoadLevel trong Start nữa.
    // GameManager DEMO sẽ quyết định:
    // - Lần đầu → demo (LV0,1,...)
    // - Sau khi xem demo → firstRealLevelIndex
    void Start()
    {
        // Để trống, không làm gì
    }

    // ==========================
    //       PUBLIC API 
    // ==========================

    public void Replay()
    {
        if (CurrentIndex < 0) return;
        LoadLevel(CurrentIndex);
    }

    public void NextLevel()
    {
        if (levels.Count == 0) return;

        int next = CurrentIndex + 1;
        if (next >= levels.Count)
        {
            if (loopAtEnd) next = defaultStartIndex;   // quay lại level thật đầu tiên
            else next = levels.Count - 1;             // đứng ở level cuối
        }
        LoadLevel(next);
    }

    public void PrevLevel()
    {
        if (levels.Count == 0) return;

        int prev = CurrentIndex - 1;
        if (prev < 0)
        {
            if (loopAtEnd) prev = levels.Count - 1;
            else prev = 0;
        }
        LoadLevel(prev);
    }

    public void LoadLevelById(string id)
    {
        int i = levels.FindIndex(l => l.id == id);
        if (i >= 0) LoadLevel(i);
        else Debug.LogWarning($"[LevelManager] Không tìm thấy level id='{id}'");
    }

    // ==========================
    //         CORE LOAD
    // ==========================

    public void LoadLevel(int index)
    {
        if (levels.Count == 0)
        {
            Debug.LogError("[LevelManager] Chưa cấu hình levels!");
            return;
        }

        index = Mathf.Clamp(index, 0, levels.Count - 1);

        // 1) Dọn runtime TRƯỚC KHI HỦY/LOAD (đảm bảo sạch)
        CleanRuntime();

        // 2) Hủy level cũ
        if (CurrentLevelGO)
        {
            OnLevelUnloaded?.Invoke(CurrentIndex);
            Destroy(CurrentLevelGO);
            CurrentLevelGO = null;
        }

        // 3) Spawn level mới
        var entry = levels[index];
        if (!entry.prefab)
        {
            Debug.LogError($"[LevelManager] Prefab rỗng tại index {index}");
            return;
        }

        CurrentLevelGO = Instantiate(entry.prefab, levelRoot);
        CurrentLevelGO.name = string.IsNullOrEmpty(entry.id)
            ? $"Level_{index}"
            : entry.id;

        CurrentIndex = index;

        HintSystem.Instance?.HideHint();

        // 4) Save progress
        // Chỉ save khi:
        //  - bật saveProgress
        //  - và đây là level thật (>= defaultStartIndex để bỏ qua level demo 0,1,...)
        if (saveProgress && CurrentIndex >= defaultStartIndex)
        {
            PlayerPrefs.SetInt(PP_LEVEL_INDEX, CurrentIndex);
            PlayerPrefs.Save();
        }

        // 5) Thông báo GameManager reset gameplay (timer, state,...)
        GameManager.Instance?.ResetForNewLevel();

        // 6) Callback / log
        OnLevelLoaded?.Invoke(CurrentLevelGO, CurrentIndex);
        Debug.Log($"[LevelManager] Loaded {CurrentLevelGO.name} (index {CurrentIndex})");

        // 7) Hiện "Level X" cho level thật
        //if (CurrentIndex >= defaultStartIndex)
        //{
        //    UIManager.Instance
        //        .GetUI<CanvasGamePlay>()?
        //        .StartShowLevel(CurrentIndex);
        //}
    }

    // ==========================
    //         CLEAR RUNTIME
    // ==========================

    public void CleanRuntime()
    {
        // 1) Clear các object runtime nằm trong runtimeRoot
        if (runtimeRoot)
        {
            for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
                Destroy(runtimeRoot.GetChild(i).gameObject);
        }

        // 2) Xoá các object mang layer EggFrag (mảnh vỡ trứng)
        if (eggFragLayer >= 0)
        {
            var all = FindObjectsOfType<GameObject>();
            foreach (var go in all)
            {
                if (!go || !go.activeInHierarchy) continue;
                if (go.layer == eggFragLayer)
                    Destroy(go);
            }
        }

        // 3) Clear toàn bộ các nét vẽ (stroke) trong DrawManager
        DrawManager.Instance?.ClearAllStrokes();

        // 4) Nếu dùng Pool (object pooling) thì despawn tất cả
        foreach (var p in FindObjectsOfType<Pool>(true))
            p.DespawnAll();
    }
}
