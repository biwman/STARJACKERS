using UnityEngine;

public sealed class RoundAnnouncementUI : MonoBehaviour
{
    public static void Show(string message, float seconds = 2.6f)
    {
        RoundMessageLayer.ShowTopCenter(message, seconds);
    }

    public static void SetPersistentHint(string ownerKey, string message)
    {
        RoundMessageLayer.SetPersistentObjective(ownerKey, message);
    }

    public static void ClearPersistentHint(string ownerKey)
    {
        RoundMessageLayer.ClearPersistentObjective(ownerKey);
    }
}
