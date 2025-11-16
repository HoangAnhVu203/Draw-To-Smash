using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasGamePlay : UICanvas
{
    public Text levelText;
    public float showTime = 5f;    
    public float fadeTime = 0.4f;

    Coroutine showRoutine;

    void OnEnable()
    {
        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded += OnLevelLoaded;

        StartShowLevel();
    }

    void OnDisable()
    {
        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded -= OnLevelLoaded;
    }

    void OnLevelLoaded(GameObject go, int index)
    {
        StartShowLevel();
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

        int index = LevelManager.Instance.CurrentIndex + 1;
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


    public void SettingBTN()
    {
        UIManager.Instance.OpenUI<CanvasSetting>();
        DrawManager.Instance.enabled = false;
        //Wheel.Instance.enabled = false;
    }

    
}
