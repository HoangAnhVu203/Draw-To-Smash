using UnityEngine;


public static class VibrationManager
{
    public static void Vibrate()
    {
        if (AudioManager.IsVibrationOn())
            Handheld.Vibrate();
    }

    
}
