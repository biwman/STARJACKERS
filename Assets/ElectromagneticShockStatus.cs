using UnityEngine;

public sealed class ElectromagneticShockStatus : MonoBehaviour
{
    float activeUntil;
    float speedMultiplier = 1f;
    float fireIntervalMultiplier = 1f;

    public bool IsActive => Time.time < activeUntil;
    public float SpeedMultiplier => IsActive ? speedMultiplier : 1f;
    public float FireIntervalMultiplier => IsActive ? fireIntervalMultiplier : 1f;

    public static void Apply(GameObject target, float duration, float appliedSpeedMultiplier, float appliedFireIntervalMultiplier)
    {
        if (target == null || duration <= 0f)
            return;

        ElectromagneticShockStatus status = target.GetComponent<ElectromagneticShockStatus>();
        if (status == null)
            status = target.AddComponent<ElectromagneticShockStatus>();

        status.activeUntil = Mathf.Max(status.activeUntil, Time.time + duration);
        status.speedMultiplier = Mathf.Min(status.speedMultiplier, Mathf.Clamp(appliedSpeedMultiplier, 0.05f, 1f));
        status.fireIntervalMultiplier = Mathf.Max(status.fireIntervalMultiplier, Mathf.Max(1f, appliedFireIntervalMultiplier));
        ElectromagneticShockVfx.Attach(target, duration);
    }

    public static float GetSpeedMultiplier(GameObject target)
    {
        ElectromagneticShockStatus status = target != null ? target.GetComponent<ElectromagneticShockStatus>() : null;
        return status != null ? status.SpeedMultiplier : 1f;
    }

    public static float GetFireIntervalMultiplier(GameObject target)
    {
        ElectromagneticShockStatus status = target != null ? target.GetComponent<ElectromagneticShockStatus>() : null;
        return status != null ? status.FireIntervalMultiplier : 1f;
    }

    void Update()
    {
        if (Time.time < activeUntil)
            return;

        Destroy(this);
    }
}
