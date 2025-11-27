using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelEndGame : UICanvas
{
    public void RollBackBTN()
    {
        UIManager.Instance.CloseUIDirectly<PanelEndGame>();

        int lv1 = LevelManager.Instance.defaultStartIndex;
        LevelManager.Instance.LoadLevel(lv1);

        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }
}
