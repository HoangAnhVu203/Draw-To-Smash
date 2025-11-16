using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasSetting : UICanvas
{
    public void CloseUI()
    {
        UIManager.Instance.CloseUIDirectly<CanvasSetting>();
        DrawManager.Instance.enabled = true;
        //Wheel.Instance.enabled = true;
    }
}
