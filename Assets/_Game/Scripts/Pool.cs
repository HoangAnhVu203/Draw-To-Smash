using System.Collections.Generic;
using UnityEngine;

public class Pool : MonoBehaviour
{
    [System.Serializable]
    public class PoolItem
    {
        public string key;          // tên nhóm (ví dụ: "EggFrag")
        public GameObject prefab;   // prefab để tạo
        public int preload = 10;    // số lượng preload
    }

    public List<PoolItem> items = new List<PoolItem>();
    private Dictionary<string, Queue<GameObject>> pools = new();

    void Awake()
    {
        foreach (var item in items)
        {
            if (!item.prefab) continue;

            var queue = new Queue<GameObject>();
            for (int i = 0; i < item.preload; i++)
            {
                var go = Instantiate(item.prefab);
                go.SetActive(false);
                queue.Enqueue(go);
            }
            pools[item.key] = queue;
        }
    }

    public GameObject Get(string key, Vector3 pos, Quaternion rot)
    {
        if (!pools.ContainsKey(key))
        {
            Debug.LogWarning($"[PoolManager] Không tìm thấy key: {key}");
            return null;
        }

        GameObject obj = pools[key].Count > 0 ? pools[key].Dequeue() : null;
        if (obj == null)
        {
            // Nếu pool rỗng → tạo thêm 1 object
            var prefab = items.Find(i => i.key == key)?.prefab;
            if (prefab) obj = Instantiate(prefab);
        }

        if (obj)
        {
            obj.transform.position = pos;
            obj.transform.rotation = rot;
            obj.SetActive(true);
        }

        return obj;
    }

    public void Return(string key, GameObject obj)
    {
        if (!obj) return;
        obj.SetActive(false);
        obj.transform.SetParent(transform);

        if (!pools.ContainsKey(key))
            pools[key] = new Queue<GameObject>();

        pools[key].Enqueue(obj);
    }
}
