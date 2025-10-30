using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasVictory : UICanvas
{
    public void RePlayBTN()
    {
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }

    public void NextLVBTN()
    {
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }
}
