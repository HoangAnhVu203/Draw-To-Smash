using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelManager : MonoBehaviour
{
    [Serializable]
    public class LevelEntry
    {
        public string id;           
        public GameObject prefab;   // prefab level
    }

    public static LevelManager Instance { get; private set; }

    [Header("Danh sách level (kéo Prefab vào)")]
    public List<LevelEntry> levels = new List<LevelEntry>();

    [Header("Nơi spawn level (trống = dùng this.transform)")]
    public Transform levelRoot;

    [Header("Thiết lập khởi động")]
    public bool saveProgress = true;
    public bool loopAtEnd = false;     // true = qua cuối sẽ quay lại LV1
    public int defaultStartIndex = 0;    // dùng khi chưa có save

    public const string PP_LEVEL_INDEX = "LV_INDEX";

    public int CurrentIndex { get; private set; } = -1;
    public GameObject CurrentLevelGO { get; private set; }

    public event Action<GameObject, int> OnLevelLoaded;   // (levelGO, index)
    public event Action<int> OnLevelUnloaded;            // (index cũ)

    [Header("Nơi chứa đối tượng runtime để dọn khi đổi level")]
    public Transform runtimeRoot;               // NEW

    int eggFragLayer = -1;   

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!levelRoot) levelRoot = transform;

        if (!runtimeRoot)
        {
            var rt = new GameObject("__RuntimeRoot");
            runtimeRoot = rt.transform;
            runtimeRoot.SetParent(transform, false);
        }

        eggFragLayer = LayerMask.NameToLayer("EggFrag");
    }

    void Start()
    {
        int start = saveProgress && PlayerPrefs.HasKey(PP_LEVEL_INDEX)
            ? PlayerPrefs.GetInt(PP_LEVEL_INDEX, defaultStartIndex)
            : defaultStartIndex;

        start = Mathf.Clamp(start, 0, Mathf.Max(0, levels.Count - 1));
        LoadLevel(start);
    }

    //================= PUBLIC API =================//

    public Transform RuntimeRoot => runtimeRoot;

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
            if (loopAtEnd) next = 0;
            else next = levels.Count - 1; // đứng ở level cuối
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

    //================= CORE =================//

    public void LoadLevel(int index)
    {
        if (levels.Count == 0) { Debug.LogError("[LevelManager] Chưa cấu hình levels!"); return; }
        index = Mathf.Clamp(index, 0, levels.Count - 1);

        // 1) DỌN runtime TRƯỚC KHI HỦY/LOAD (đảm bảo sạch)
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
        if (!entry.prefab) { Debug.LogError($"[LevelManager] Prefab rỗng tại index {index}"); return; }

        CurrentLevelGO = Instantiate(entry.prefab, levelRoot);
        CurrentLevelGO.name = string.IsNullOrEmpty(entry.id) ? $"Level_{index}" : entry.id;
        CurrentIndex = index;

        if (saveProgress) { PlayerPrefs.SetInt(PP_LEVEL_INDEX, CurrentIndex); PlayerPrefs.Save(); }

        OnLevelLoaded?.Invoke(CurrentLevelGO, CurrentIndex);
        GameManager.Instance?.ResetForNewLevel();
        Debug.Log($"[LevelManager] Loaded {CurrentLevelGO.name} (index {CurrentIndex})");
    }

    public void CleanRuntime()
    {
        if (runtimeRoot)
        {
            for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
                Destroy(runtimeRoot.GetChild(i).gameObject);
        }

        if (eggFragLayer >= 0)
        {
            var all = FindObjectsOfType<GameObject>();
            foreach (var go in all)
            {
                if (!go || !go.activeInHierarchy) continue;
                if (go.layer == eggFragLayer) Destroy(go);
            }
        }

        DrawManager.Instance?.ClearAllStrokes();

        foreach (var p in FindObjectsOfType<Pool>(true))
            p.DespawnAll(); 
    }
}
