using UnityEngine;

public struct WeaponDamageMultipliers
{
    public readonly float ShieldMultiplier;
    public readonly float HpMultiplier;

    public bool IsNeutral
    {
        get
        {
            return Mathf.Approximately(ShieldMultiplier, 1f) &&
                   Mathf.Approximately(HpMultiplier, 1f);
        }
    }

    public static WeaponDamageMultipliers Neutral
    {
        get { return new WeaponDamageMultipliers(1f, 1f); }
    }

    public WeaponDamageMultipliers(float shieldMultiplier, float hpMultiplier)
    {
        ShieldMultiplier = Mathf.Max(0f, shieldMultiplier);
        HpMultiplier = Mathf.Max(0f, hpMultiplier);
    }
}

public static class WeaponDamageInteractionCatalog
{
    public static bool DebugDamageContexts;

    public static WeaponDamageMultipliers ResolveMultipliers(WeaponHitContext hitContext)
    {
        return new WeaponDamageMultipliers(
            ResolveShieldMultiplier(hitContext),
            ResolveHpMultiplier(hitContext));
    }

    public static int ApplyMultiplier(int damage, float multiplier)
    {
        if (damage <= 0)
            return 0;

        if (Mathf.Approximately(multiplier, 1f))
            return damage;

        if (multiplier <= 0f)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }

    public static string BuildDebugSummary(WeaponHitContext hitContext, float shieldMultiplier, float hpMultiplier)
    {
        string classification = WeaponAttackCatalog.BuildClassificationSummary(hitContext.DamageType, hitContext.DeliveryMethod, hitContext.DeliveryFlags);
        if (string.IsNullOrWhiteSpace(classification))
            classification = "Damage type: None";

        return classification +
               "\nShield x" + FormatMultiplier(shieldMultiplier) +
               " / HP x" + FormatMultiplier(hpMultiplier);
    }

    public static void LogDamageContext(Object contextObject, string phase, WeaponHitContext hitContext, WeaponDamageMultipliers multipliers, int attackerViewId, int shieldDamage, int hpDamage)
    {
        if (!DebugDamageContexts)
            return;

        Debug.Log(
            "[WeaponDamage] " + (phase ?? "damage") +
            " attacker=" + attackerViewId +
            " shield=" + shieldDamage +
            " hp=" + hpDamage +
            " type=" + WeaponAttackCatalog.GetDamageTypeLabel(hitContext.DamageType) +
            " delivery=" + WeaponAttackCatalog.GetDeliveryMethodLabel(hitContext.DeliveryMethod) +
            " flags=" + WeaponAttackCatalog.GetDeliveryFlagsLabel(hitContext.DeliveryFlags) +
            " source=" + (hitContext.DamageSource ?? string.Empty) +
            " shieldMultiplier=" + FormatMultiplier(multipliers.ShieldMultiplier) +
            " hpMultiplier=" + FormatMultiplier(multipliers.HpMultiplier),
            contextObject);
    }

    static float ResolveShieldMultiplier(WeaponHitContext hitContext)
    {
        switch (hitContext.DamageType)
        {
            case WeaponDamageType.Laser:
            case WeaponDamageType.Plasma:
            case WeaponDamageType.Kinetic:
            case WeaponDamageType.Explosive:
            case WeaponDamageType.Ion:
            case WeaponDamageType.Gravitic:
            case WeaponDamageType.Environmental:
            case WeaponDamageType.None:
            default:
                return 1f;
        }
    }

    static float ResolveHpMultiplier(WeaponHitContext hitContext)
    {
        switch (hitContext.DamageType)
        {
            case WeaponDamageType.Laser:
            case WeaponDamageType.Plasma:
            case WeaponDamageType.Kinetic:
            case WeaponDamageType.Explosive:
            case WeaponDamageType.Ion:
            case WeaponDamageType.Gravitic:
            case WeaponDamageType.Environmental:
            case WeaponDamageType.None:
            default:
                return 1f;
        }
    }

    static string FormatMultiplier(float value)
    {
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
