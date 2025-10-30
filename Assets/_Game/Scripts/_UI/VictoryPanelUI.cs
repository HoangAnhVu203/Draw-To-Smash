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
        // Tự tìm nếu chưa gán
        if (!replayBtn) replayBtn = transform.Find("Button - RePlay")?.GetComponent<Button>();
        if (!nextBtn) nextBtn = transform.Find("Button - NextLV")?.GetComponent<Button>();
        if (!closeBtn) closeBtn = transform.Find("Button - Close")?.GetComponent<Button>();

        // Gắn đúng 1 listener cho mỗi nút
        if (replayBtn) replayBtn.onClick.AddListener(OnReplay);
        if (nextBtn) nextBtn.onClick.AddListener(OnNext);
        if (closeBtn) closeBtn.onClick.AddListener(OnClose);
    }

    public void OnReplay()
    {
        Time.timeScale = 1f;
        LevelManager.Instance?.Replay();
        gameObject.SetActive(false);
    }

    public void OnNext()
    {
        Time.timeScale = 1f;
        LevelManager.Instance?.NextLevel();
        gameObject.SetActive(false);
    }

    public void OnClose()
    {
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }
}
