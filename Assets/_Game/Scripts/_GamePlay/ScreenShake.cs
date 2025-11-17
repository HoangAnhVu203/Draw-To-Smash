using UnityEngine;
using System.Collections;

public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance;

    public float shakeDuration = 0.1f;
    public float shakeStrength = 0.2f;

    Vector3 originalPos;

    void Awake()
    {
        Instance = this;
        originalPos = transform.localPosition;
    }

    public void Shake()
    {
        StopAllCoroutines();
        StartCoroutine(DoShake());
    }

    IEnumerator DoShake()
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            transform.localPosition = originalPos + (Vector3)Random.insideUnitCircle * shakeStrength;
            yield return null;
        }
        transform.localPosition = originalPos;
    }
}
