using UnityEngine;

public sealed class AtlasSuppressionStatus : MonoBehaviour
{
    float activeUntil;
    float speedMultiplier = 1f;
    float fireIntervalMultiplier = 1f;
    float reloadMultiplier = 1f;
    float projectileSpeedMultiplier = 1f;

    public bool IsActive => Time.time < activeUntil;
    public float SpeedMultiplier => IsActive ? speedMultiplier : 1f;
    public float FireIntervalMultiplier => IsActive ? fireIntervalMultiplier : 1f;
    public float ReloadMultiplier => IsActive ? reloadMultiplier : 1f;
    public float ProjectileSpeedMultiplier => IsActive ? projectileSpeedMultiplier : 1f;

    public static void Apply(
        GameObject target,
        float duration,
        float appliedSpeedMultiplier,
        float appliedFireIntervalMultiplier,
        float appliedReloadMultiplier,
        float appliedProjectileSpeedMultiplier)
    {
        if (target == null || duration <= 0f)
            return;

        AtlasSuppressionStatus status = target.GetComponent<AtlasSuppressionStatus>();
        if (status == null)
            status = target.AddComponent<AtlasSuppressionStatus>();

        status.activeUntil = Mathf.Max(status.activeUntil, Time.time + duration);
        status.speedMultiplier = Mathf.Min(status.speedMultiplier, Mathf.Clamp(appliedSpeedMultiplier, 0.05f, 1f));
        status.fireIntervalMultiplier = Mathf.Max(status.fireIntervalMultiplier, Mathf.Max(1f, appliedFireIntervalMultiplier));
        status.reloadMultiplier = Mathf.Max(status.reloadMultiplier, Mathf.Max(1f, appliedReloadMultiplier));
        status.projectileSpeedMultiplier = Mathf.Min(status.projectileSpeedMultiplier, Mathf.Clamp(appliedProjectileSpeedMultiplier, 0.05f, 1f));
        ElectromagneticShockVfx.Attach(target, duration);
    }

    public static float GetSpeedMultiplier(GameObject target)
    {
        AtlasSuppressionStatus status = target != null ? target.GetComponent<AtlasSuppressionStatus>() : null;
        return status != null ? status.SpeedMultiplier : 1f;
    }

    public static float GetFireIntervalMultiplier(GameObject target)
    {
        AtlasSuppressionStatus status = target != null ? target.GetComponent<AtlasSuppressionStatus>() : null;
        return status != null ? status.FireIntervalMultiplier : 1f;
    }

    public static float GetReloadMultiplier(GameObject target)
    {
        AtlasSuppressionStatus status = target != null ? target.GetComponent<AtlasSuppressionStatus>() : null;
        return status != null ? status.ReloadMultiplier : 1f;
    }

    public static float GetProjectileSpeedMultiplier(GameObject target)
    {
        AtlasSuppressionStatus status = target != null ? target.GetComponent<AtlasSuppressionStatus>() : null;
        return status != null ? status.ProjectileSpeedMultiplier : 1f;
    }

    void Update()
    {
        if (Time.time < activeUntil)
            return;

        Destroy(this);
    }
}
