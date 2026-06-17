using UnityEngine;

public sealed class ToxicBorderDeathZoneWarningUI : MonoBehaviour
{
    const string WarningMessage = "LEAVE THE DEATH ZONE";

    public static void ShowForFrame()
    {
        RoundMessageLayer.ShowWarning(WarningMessage, RoundMessagePriority.Danger, 0.24f);
    }

    public static void HideImmediate()
    {
        RoundMessageLayer.ClearWarning(RoundMessagePriority.Danger);
    }
}
