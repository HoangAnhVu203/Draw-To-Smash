using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider2D))]
public class Wheel : MonoBehaviour, IPointerClickHandler
{
    [Header("Hiển thị")]
    public SpriteRenderer spriteRenderer;
    [Range(0f, 1f)] public float maxAlpha = 1f;
    public float fadeSpeed = 10f;

    [Header("Vật lý")]
    public float gravityScale = 1f;
    public bool addRigidbodyIfMissing = true;

    Rigidbody2D rb;
    bool clicked = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Fallback 1: hoạt động không cần EventSystem
    void OnMouseDown()
    {
        TryActivate();
    }

    // Fallback 2: hoạt động với EventSystem + Physics2DRaycaster
    public void OnPointerClick(PointerEventData eventData)
    {
        TryActivate();
    }

    void TryActivate()
    {
        if (clicked) return;
        clicked = true;

        if (spriteRenderer)
            StartCoroutine(FadeToAlpha(spriteRenderer.color.a, maxAlpha));

        if (!rb && addRigidbodyIfMissing)
            rb = gameObject.AddComponent<Rigidbody2D>();

        if (rb)
        {
            rb.gravityScale = gravityScale;
            rb.simulated = true;
        }
    }

    IEnumerator FadeToAlpha(float from, float to)
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * fadeSpeed;
            float a = Mathf.Lerp(from, to, t);
            if (spriteRenderer)
            {
                var c = spriteRenderer.color;
                c.a = a;
                spriteRenderer.color = c;
            }
            yield return null;
        }
    }
}
