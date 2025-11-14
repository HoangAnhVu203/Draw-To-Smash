using UnityEngine;
using System.Linq;

[DisallowMultipleComponent]
public class HintSystem : MonoBehaviour
{
    [Header("Config")]
    public int maxHintsPerLevel = 1000;

    [Header("Refs")]
    public GameObject handPrefab;         // HandHand_PF
    public Transform handParentOverride;  // để trống = LevelManager.levelRoot
    public string waypointRootName = "HintWaypoints"; // object chứa các P0,P1,... trong level

    [Header("Hand Settings Override")]
    public float handMoveSpeed = 3f;
    public bool handLoop = true;

    int usedHints = 0;
    GameObject activeHand;

    public bool CanUseHint() => usedHints < maxHintsPerLevel;

    public void TryUseHint()
    {
        if (!CanUseHint())
        {
            Debug.Log("[Hint] Hết lượt gợi ý!");
            return;
        }

        var levelRoot = LevelManager.Instance ? (LevelManager.Instance.levelRoot ? LevelManager.Instance.levelRoot : LevelManager.Instance.transform) : null;
        if (!levelRoot) { Debug.LogWarning("[Hint] Không tìm thấy levelRoot."); return; }

        // tìm cụm waypoint trong level
        var wpRoot = levelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == waypointRootName);
        if (!wpRoot)
        {
            Debug.LogWarning($"[Hint] Không thấy '{waypointRootName}' trong level.");
            return;
        }

        var points = wpRoot.GetComponentsInChildren<Transform>(true)
                           .Where(t => t != wpRoot) // bỏ node gốc
                           .OrderBy(t => t.name)    // P0,P1,... (sắp theo tên)
                           .ToArray();

        if (points.Length < 2)
        {
            Debug.LogWarning("[Hint] Cần ít nhất 2 waypoint để di chuyển.");
            return;
        }

        // nếu đang có hand cũ → xoá
        if (activeHand) Destroy(activeHand);

        // spawn hand
        var parent = handParentOverride ? handParentOverride : levelRoot;
        activeHand = Instantiate(handPrefab, parent);
        activeHand.transform.localScale = handPrefab.transform.localScale;

        // cấu hình di chuyển
        var mover = activeHand.GetComponent<HandHintMover>();
        if (mover)
        {
            mover.moveSpeed = handMoveSpeed;
            mover.loop = handLoop;
            mover.SetPath(points.Select(p => p).ToArray());
        }

        usedHints++;
    }

    public void HideHint()
    {
        if (activeHand)
        {
            Destroy(activeHand);
            activeHand = null;
        }
    }

    public void ResetForNewLevel()
    {
        usedHints = 0;
        HideHint();
    }
}
