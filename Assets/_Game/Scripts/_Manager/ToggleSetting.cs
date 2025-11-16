using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleSetting : MonoBehaviour
{
    public Toggle toggle;
    public RectTransform checkmark;
    public Image background;


    public Vector2 leftPos = new Vector2(-55, 0);
    public Vector2 rightPos = new Vector2(55, 0);


    public Color onColor = Color.green;
    public Color offColor = Color.gray;
    void Start()
    {
        toggle.onValueChanged.AddListener(OnToggleChanged);


        checkmark.anchoredPosition = toggle.isOn ? rightPos : leftPos;
        background.color = toggle.isOn ? onColor : offColor;
    }

    void OnToggleChanged(bool isOn)
    {
        checkmark.anchoredPosition = isOn ? rightPos : leftPos;
        background.color = isOn ? onColor : offColor;
    }
}
