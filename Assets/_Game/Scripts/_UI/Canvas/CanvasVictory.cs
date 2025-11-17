using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasVictory : UICanvas
{
    public void RePlayBTN()
    {
        gameObject.SetActive(false);
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }

    public void NextLVBTN()
    {
        gameObject.SetActive(false);
        UIManager.Instance.OpenUI<CanvasGamePlay>();
    }
}
