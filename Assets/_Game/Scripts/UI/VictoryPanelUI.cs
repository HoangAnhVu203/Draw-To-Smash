using UnityEngine;
using UnityEngine.UI;

public class VictoryPanelUI : MonoBehaviour
{
    [Header("Optional - để trống thì tự tìm theo tên")]
    public Button replayBtn;          // Button - RePlay
    public Button nextBtn;            // Button - NextLV
    public Button closeBtn;           // (nếu có)

    void Awake()
    {
        // Tự tìm nếu chưa gán trong Inspector (tên con gợi ý)
        if (!replayBtn) replayBtn = transform.Find("Button - RePlay")?.GetComponent<Button>();
        if (!nextBtn) nextBtn = transform.Find("Button - NextLV")?.GetComponent<Button>();
        if (!closeBtn) closeBtn = transform.Find("Button - Close")?.GetComponent<Button>();

        // Gắn sự kiện
        if (replayBtn) replayBtn.onClick.AddListener(OnReplay);
        if (nextBtn) nextBtn.onClick.AddListener(OnNext);
        if (closeBtn) closeBtn.onClick.AddListener(() => gameObject.SetActive(false));
    }

    public void OnReplay()
    {
        Time.timeScale = 1f;                           // thoát pause
        LevelManager.Instance?.Replay();              // nạp lại level hiện tại
        gameObject.SetActive(false);                  // ẩn panel
    }

    public void OnNext()
    {
        Time.timeScale = 1f;
        LevelManager.Instance?.NextLevel();          // qua màn kế tiếp
        gameObject.SetActive(false);
    }
}
