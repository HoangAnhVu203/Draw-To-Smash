using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelMessage : UICanvas
{
    public void CloseBTN()
    {
        UIManager.Instance.CloseUIDirectly<PanelMessage>();
    }
}
