using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasGamePlay : UICanvas
{
    public Text levelText;
    public float showTime = 5f;
    public float fadeTime = 0.4f;

    [Header("Timer sau khi vẽ")]
    public Text timeScale;   // Text hiển thị đếm ngược
    public AudioClip countdownClip;
    public AudioSource countdownSource;

    Coroutine showRoutine;

    void OnEnable()
    {
        if (timeScale)
            timeScale.gameObject.SetActive(false);
        
        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded += OnLevelLoaded;

        // Đăng ký event với GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPostStrokeTimerStart += OnPostStrokeTimerStart;
            GameManager.Instance.OnPostStrokeTimerTick  += OnPostStrokeTimerTick;
            GameManager.Instance.OnPostStrokeTimerEnd   += OnPostStrokeTimerEnd;
        }

        StartShowLevel();
    }

    void OnDisable()
    {
        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded -= OnLevelLoaded;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPostStrokeTimerStart -= OnPostStrokeTimerStart;
            GameManager.Instance.OnPostStrokeTimerTick  -= OnPostStrokeTimerTick;
            GameManager.Instance.OnPostStrokeTimerEnd   -= OnPostStrokeTimerEnd;
        }
    }

    void OnLevelLoaded(GameObject go, int index)
    {
        StartShowLevel();

        if (timeScale)
            timeScale.gameObject.SetActive(false);

        // reset text timer mỗi lần load level mới
        if (timeScale)
        {
            timeScale.gameObject.SetActive(false);
        }
    }

    void StartShowLevel()
    {
        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(CoShowLevel());
    }

    IEnumerator CoShowLevel()
    {
        if (levelText == null) yield break;

        int index = LevelManager.Instance.CurrentIndex;
        levelText.text = $"Level {index}";

        var c = levelText.color;
        c.a = 1f;
        levelText.color = c;
        levelText.gameObject.SetActive(true);

        yield return new WaitForSeconds(showTime);

        float t = 0;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / fadeTime);
            c.a = a;
            levelText.color = c;
            yield return null;
        }

        levelText.gameObject.SetActive(false);
    }

    // ========== HANDLER CHO TIMER ==========

    void OnPostStrokeTimerStart()
    {

        Debug.Log("Sound Start");

        if (timeScale)
        {
            timeScale.gameObject.SetActive(true);
            // hiển thị full thời gian ban đầu
            timeScale.text = GameManager.Instance
                ? GameManager.Instance.tAfterStrokeToJudge.ToString("0")
                : "";
        }

        
        if (countdownSource && countdownClip && AudioManager.IsSoundOn())
    {
        countdownSource.clip = countdownClip;
        countdownSource.loop = true;   // chạy liên tục trong thời gian đếm
        countdownSource.Play();
    }

    }

    void OnPostStrokeTimerTick(float remaining)
    {
        if (timeScale)
        {
            timeScale.text = remaining.ToString("0"); 
        }
    }

    void OnPostStrokeTimerEnd()
    {
        if (timeScale)
        {
            timeScale.gameObject.SetActive(false);
        }
        if (countdownSource)
        countdownSource.Stop();

    }

    // ========== BUTTONS ==========
    public void SettingBTN()
    {
        UIManager.Instance.OpenUI<CanvasSetting>();
    }

    public void NextLevelBTN()
    {
        GameManager.Instance.NextLevel();
    }

    public void ReplayBTN()
    {
        GameManager.Instance.ReplayLevel();
    }
}
