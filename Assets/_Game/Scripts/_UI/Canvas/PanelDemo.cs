using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelDemo : UICanvas
{
    public void OnClickPlay()
    {
        GameManager.Instance.StartRealGame();
    }
}
