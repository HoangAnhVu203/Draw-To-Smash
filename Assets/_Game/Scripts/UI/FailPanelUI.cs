using UnityEngine;
using UnityEngine.UI;

public class FailPanelUI : MonoBehaviour
{
    public Button replayBtn;

    void Awake()
    {
        if (!replayBtn) replayBtn = transform.Find("Button - RePlay")?.GetComponent<Button>();
        if (replayBtn) replayBtn.onClick.AddListener(OnReplay);
    }

    public void OnReplay()
    {
        Time.timeScale = 1f;
        LevelManager.Instance?.Replay();
        gameObject.SetActive(false);
    }
}
