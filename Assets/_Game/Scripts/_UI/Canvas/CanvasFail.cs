using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasFail : UICanvas
{
    public void RePlayBTN()
    {
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }
}
