using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShortScannerRevealStatus : MonoBehaviour
{
    static ShortScannerRevealStatus activeOwner;

    float activeUntil;
    bool wasActive;

    public static bool IsActive => activeOwner != null && Time.time < activeOwner.activeUntil;

    public static ShortScannerRevealStatus EnsureFor(GameObject target)
    {
        if (target == null)
            return null;

        ShortScannerRevealStatus status = target.GetComponent<ShortScannerRevealStatus>();
        if (status == null)
            status = target.AddComponent<ShortScannerRevealStatus>();

        activeOwner = status;
        return status;
    }

    public void ActivateReveal(float duration)
    {
        activeOwner = this;
        activeUntil = Mathf.Max(activeUntil, Time.time + Mathf.Max(0f, duration));
        wasActive = true;
        HideInNebulaTarget.RefreshAllTargetVisibility();
    }

    void Update()
    {
        if (!wasActive || Time.time < activeUntil)
            return;

        ClearActiveOwner();
    }

    void OnDisable()
    {
        ClearActiveOwner();
    }

    void OnDestroy()
    {
        ClearActiveOwner();
    }

    void ClearActiveOwner()
    {
        if (activeOwner != this)
            return;

        activeOwner = null;
        activeUntil = 0f;
        wasActive = false;
        HideInNebulaTarget.RefreshAllTargetVisibility();
    }
}
