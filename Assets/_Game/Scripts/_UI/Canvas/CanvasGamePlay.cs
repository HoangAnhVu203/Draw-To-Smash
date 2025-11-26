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

    public RectTransform nextLvButton;
    public float nextWaitTime = 15f;
    public float nextScaleUp = 1.2f;
    public float nextScaleDuration = 0.3f;

    [Header("Hint Button Hint")]
    public RectTransform hintButton;
    public float hintWaitTime = 7f;
    public float hintScaleUp = 1.2f;
    public float hintScaleDuration = 0.3f;

    Vector3 nextOriginalScale;
    Vector3 hintOriginalScale;

    Coroutine nextScaleRoutine;
    Coroutine hintScaleRoutine;

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

        if (nextLvButton != null)
        {
            nextOriginalScale = nextLvButton.localScale;
            nextLvButton.localScale = nextOriginalScale;
        }
        if (hintButton != null)
        {
            hintOriginalScale = hintButton.localScale;
            hintButton.localScale = hintOriginalScale;
        }

        StartNextScaleHint();
        StartHintButtonScaleHint();
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

        StopNextScaleHint();
        StopHintButtonScaleHint();
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

    void StartNextScaleHint()
    {
        if (nextLvButton == null) return;

        if (nextScaleRoutine != null)
            StopCoroutine(nextScaleRoutine);

        nextScaleRoutine = StartCoroutine(NextButtonScaleLoop());
    }

    void StopNextScaleHint()
    {
        if (nextScaleRoutine != null)
            StopCoroutine(nextScaleRoutine);

        if (nextLvButton != null)
            nextLvButton.localScale = nextOriginalScale;

        nextScaleRoutine = null;
    }

    IEnumerator NextButtonScaleLoop()
    {
        yield return new WaitForSeconds(nextWaitTime);

        while (true)
        {
            // scale up
            float t = 0f;
            while (t < nextScaleDuration)
            {
                t += Time.deltaTime;
                float k = t / nextScaleDuration;
                nextLvButton.localScale =
                    Vector3.Lerp(nextOriginalScale, nextOriginalScale * nextScaleUp, k);
                yield return null;
            }

            // scale down
            t = 0f;
            while (t < nextScaleDuration)
            {
                t += Time.deltaTime;
                float k = t / nextScaleDuration;
                nextLvButton.localScale =
                    Vector3.Lerp(nextOriginalScale * nextScaleUp, nextOriginalScale, k);
                yield return null;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    // ================== SCALE HINT BTN ==================

    void StartHintButtonScaleHint()
    {
        if (hintButton == null) return;

        if (hintScaleRoutine != null)
            StopCoroutine(hintScaleRoutine);

        hintScaleRoutine = StartCoroutine(HintButtonScaleLoop());
    }

    void StopHintButtonScaleHint()
    {
        if (hintScaleRoutine != null)
            StopCoroutine(hintScaleRoutine);

        if (hintButton != null)
            hintButton.localScale = hintOriginalScale;

        hintScaleRoutine = null;
    }

    IEnumerator HintButtonScaleLoop()
    {
        yield return new WaitForSeconds(hintWaitTime);

        while (true)
        {
            // scale up
            float t = 0f;
            while (t < hintScaleDuration)
            {
                t += Time.deltaTime;
                float k = t / hintScaleDuration;
                hintButton.localScale =
                    Vector3.Lerp(hintOriginalScale, hintOriginalScale * hintScaleUp, k);
                yield return null;
            }

            // scale down
            t = 0f;
            while (t < hintScaleDuration)
            {
                t += Time.deltaTime;
                float k = t / hintScaleDuration;
                hintButton.localScale =
                    Vector3.Lerp(hintOriginalScale * hintScaleUp, hintOriginalScale, k);
                yield return null;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    public void ResetHintTimers()
    {
        // dừng toàn bộ hiệu ứng cũ
        StopNextScaleHint();
        StopHintButtonScaleHint();

        // trả scale về gốc (phòng trường hợp không chạy OnEnable)
        if (nextLvButton != null)
            nextLvButton.localScale = nextOriginalScale;

        if (hintButton != null)
            hintButton.localScale = hintOriginalScale;

        // bắt đầu đếm lại từ đầu
        StartNextScaleHint();
        StartHintButtonScaleHint();
    }
}
