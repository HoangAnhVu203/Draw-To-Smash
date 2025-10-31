using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Pool : MonoBehaviour
{
    [System.Serializable]
    public class PoolItem
    {
        public string key;            // ví dụ "EggFrag1"
        public GameObject prefab;
        public int preload = 10;
    }

    // gắn lên mỗi object để biết nó thuộc key nào, owner nào
    class PooledObject : MonoBehaviour
    {
        public string key;
        public Pool owner;
    }

    public List<PoolItem> items = new List<PoolItem>();

    // kho: key -> queue các object rảnh
    private readonly Dictionary<string, Queue<GameObject>> _store = new();
    // đang active
    private readonly List<GameObject> _active = new();

    void Awake()
    {
        // tạo trước
        foreach (var it in items)
        {
            if (!it.prefab) continue;

            var q = new Queue<GameObject>();
            for (int i = 0; i < it.preload; i++)
            {
                var go = Instantiate(it.prefab, transform);
                go.SetActive(false);

                // gắn dấu
                var tag = go.GetComponent<PooledObject>() ?? go.AddComponent<PooledObject>();
                tag.key = it.key;
                tag.owner = this;

                q.Enqueue(go);
            }
            _store[it.key] = q;
        }
    }

    // === API ===

    // Get kèm parent (nên dùng cái này)
    public GameObject Get(string key, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (!_store.ContainsKey(key))
        {
            Debug.LogWarning($"[Pool] Key '{key}' chưa có, sẽ tạo queue mới.");
            _store[key] = new Queue<GameObject>();
        }

        GameObject obj = null;
        if (_store[key].Count > 0)
        {
            obj = _store[key].Dequeue();
        }
        else
        {
            // hết hàng -> instantiate thêm từ cấu hình items
            var prefab = items.Find(i => i.key == key)?.prefab;
            if (prefab)
            {
                obj = Instantiate(prefab, transform);
                var tag = obj.GetComponent<PooledObject>() ?? obj.AddComponent<PooledObject>();
                tag.key = key;
                tag.owner = this;
                obj.SetActive(false);
            }
        }

        if (!obj) return null;

        if (parent) obj.transform.SetParent(parent, true);
        obj.transform.SetPositionAndRotation(pos, rot);
        obj.SetActive(true);

        if (!_active.Contains(obj))
            _active.Add(obj);

        return obj;
    }

    // Get không parent (vẫn giữ cho tương thích)
    public GameObject Get(string key, Vector3 pos, Quaternion rot)
        => Get(key, pos, rot, null);

    // Return theo key (giữ lại để tương thích)
    public void Return(string key, GameObject obj)
    {
        if (!obj) return;
        var tag = obj.GetComponent<PooledObject>();
        if (tag == null) // fallback nếu thiếu tag
        {
            tag = obj.AddComponent<PooledObject>();
            tag.key = key;
            tag.owner = this;
        }
        InternalReturn(obj, tag);
    }

    // Return không cần key (ƯU TIÊN dùng cái này)
    public void Return(GameObject obj)
    {
        if (!obj) return;
        var tag = obj.GetComponent<PooledObject>();
        if (tag == null || tag.owner != this)
        {
            obj.SetActive(false);
            obj.transform.SetParent(transform, false);
            return;
        }
        InternalReturn(obj, tag);
    }

    // Dọn toàn bộ đang active (dùng khi Replay/Next level)
    public void DespawnAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var go = _active[i];
            if (!go) { _active.RemoveAt(i); continue; }
            Return(go); // sẽ tự đẩy về đúng key
        }
    }

    // === internal ===
    void InternalReturn(GameObject obj, PooledObject tag)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform, false);

        if (!_store.ContainsKey(tag.key))
            _store[tag.key] = new Queue<GameObject>();

        _store[tag.key].Enqueue(obj);
        _active.Remove(obj);
    }
}
