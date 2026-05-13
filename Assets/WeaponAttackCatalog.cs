using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ComplexAttackMarkerType
{
    Line,
    Arc
}

[Serializable]
public sealed class WeaponAttackProfile
{
    public string Id;
    public string DisplayName;
    public int MaxAmmo;
    public float RangeMultiplier;
    public float ProjectileSize;
    public float ProjectileSpeed;
    public int HpDamage;
    public int ShieldDamage;
    public int ProjectileCount;
    public float SpreadAngle;
    public float FlightTime;
    public float ProjectileInterval;
    public float AttackCooldown;
    public float AmmoReloadTime;
    public float StartDelay;
    public string HitEffectId;
    public string ShotSoundId;
    public bool Pierces;
    public float AreaDamageRadius;
    public Color ProjectileColor;
    public ComplexAttackMarkerType MarkerType;
    public Color MarkerColor;

    public WeaponAttackProfile Clone()
    {
        return (WeaponAttackProfile)MemberwiseClone();
    }
}

public sealed class WeaponAttackParameterDefinition
{
    public string Key;
    public string Label;
    public string ValueType;
    public float Min;
    public float Max;
}

public static class WeaponAttackCatalog
{
    public const string SimpleGunId = "simple_gun";
    public const string PlasmaGunId = InventoryItemCatalog.PlasmaGunId;
    public const string TripleGunId = InventoryItemCatalog.TripleGunId;
    public const string ArtilleryGunId = InventoryItemCatalog.ArtilleryGunId;
    public const string RailGunId = InventoryItemCatalog.RailGunId;
    public const string DoubleIonizerId = InventoryItemCatalog.DoubleIonizerId;
    public const string AstroCutterId = InventoryItemCatalog.AstroCutterId;

    public const string ParameterTypeInt = "int";
    public const string ParameterTypeFloat = "float";
    public const string ParameterTypeBool = "bool";
    public const string ParameterTypeString = "string";
    public const string ParameterTypeMarkerType = "marker_type";
    public const string ParameterTypeColor = "color";

    static readonly string[] EditableWeaponIds =
    {
        SimpleGunId,
        PlasmaGunId,
        TripleGunId,
        ArtilleryGunId,
        RailGunId,
        DoubleIonizerId,
        AstroCutterId
    };

    static readonly WeaponAttackParameterDefinition[] EditableParameters =
    {
        new WeaponAttackParameterDefinition { Key = "ammo", Label = "AMMO", ValueType = ParameterTypeInt, Min = 1f, Max = 30f },
        new WeaponAttackParameterDefinition { Key = "range", Label = "RANGE", ValueType = ParameterTypeFloat, Min = 1f, Max = 80f },
        new WeaponAttackParameterDefinition { Key = "projectileSize", Label = "PROJECTILE SIZE", ValueType = ParameterTypeFloat, Min = 0.2f, Max = 8f },
        new WeaponAttackParameterDefinition { Key = "projectileSpeed", Label = "PROJECTILE SPEED", ValueType = ParameterTypeFloat, Min = 0.5f, Max = 40f },
        new WeaponAttackParameterDefinition { Key = "hpDamage", Label = "HP DAMAGE", ValueType = ParameterTypeInt, Min = 0f, Max = 300f },
        new WeaponAttackParameterDefinition { Key = "shieldDamage", Label = "SHIELD DAMAGE", ValueType = ParameterTypeInt, Min = 0f, Max = 300f },
        new WeaponAttackParameterDefinition { Key = "projectileCount", Label = "PROJECTILES", ValueType = ParameterTypeInt, Min = 1f, Max = 30f },
        new WeaponAttackParameterDefinition { Key = "spreadAngle", Label = "SPREAD ANGLE", ValueType = ParameterTypeFloat, Min = 0f, Max = 180f },
        new WeaponAttackParameterDefinition { Key = "flightTime", Label = "FLIGHT TIME", ValueType = ParameterTypeFloat, Min = 0.2f, Max = 30f },
        new WeaponAttackParameterDefinition { Key = "startDelay", Label = "START DELAY", ValueType = ParameterTypeFloat, Min = 0f, Max = 5f },
        new WeaponAttackParameterDefinition { Key = "projectileInterval", Label = "BURST GAP", ValueType = ParameterTypeFloat, Min = 0f, Max = 3f },
        new WeaponAttackParameterDefinition { Key = "attackCooldown", Label = "ATTACK COOLDOWN", ValueType = ParameterTypeFloat, Min = 0.03f, Max = 5f },
        new WeaponAttackParameterDefinition { Key = "ammoReloadTime", Label = "AMMO RELOAD", ValueType = ParameterTypeFloat, Min = 0f, Max = 15f },
        new WeaponAttackParameterDefinition { Key = "hitEffectId", Label = "HIT FX", ValueType = ParameterTypeString },
        new WeaponAttackParameterDefinition { Key = "shotSoundId", Label = "SHOT SOUND", ValueType = ParameterTypeString },
        new WeaponAttackParameterDefinition { Key = "pierces", Label = "PIERCES", ValueType = ParameterTypeBool },
        new WeaponAttackParameterDefinition { Key = "areaDamageRadius", Label = "AREA DAMAGE RADIUS", ValueType = ParameterTypeFloat, Min = 0f, Max = 12f },
        new WeaponAttackParameterDefinition { Key = "markerType", Label = "MARKER TYPE", ValueType = ParameterTypeMarkerType },
        new WeaponAttackParameterDefinition { Key = "markerColor", Label = "MARKER COLOR", ValueType = ParameterTypeColor }
    };

    static readonly Color PlasmaColor = new Color(0.15f, 1f, 0.28f, 1f);
    static readonly Color TripleColor = new Color(0.98f, 0.92f, 0.76f, 1f);
    static readonly Color ArtilleryColor = new Color(1f, 0.54f, 0.12f, 1f);
    static readonly Color RailColor = new Color(1f, 0.22f, 0.04f, 1f);
    static readonly Color IonColor = new Color(0.18f, 0.72f, 1f, 1f);
    static readonly Color AstroCutterColor = new Color(0.86f, 0.62f, 1f, 1f);
    static readonly Color SimpleMarkerColor = new Color(0.58f, 0.9f, 1f, 1f);
    static readonly Color PlasmaMarkerColor = new Color(0.28f, 1f, 0.52f, 1f);
    static readonly Color TripleMarkerColor = new Color(0.88f, 0.96f, 1f, 1f);
    static readonly Color ArtilleryMarkerColor = new Color(1f, 0.66f, 0.2f, 1f);
    static readonly Color RailMarkerColor = new Color(1f, 0.42f, 0.12f, 1f);
    static readonly Color IonMarkerColor = new Color(0.2f, 0.86f, 1f, 1f);
    static readonly Color AstroCutterMarkerColor = new Color(1f, 0.84f, 0.28f, 1f);

    public static IReadOnlyList<string> GetEditableWeaponIds()
    {
        return EditableWeaponIds;
    }

    public static IReadOnlyList<WeaponAttackParameterDefinition> GetEditableParameters()
    {
        return EditableParameters;
    }

    public static WeaponAttackProfile GetNormalAttack(string[] equipmentSlots, int shipSkinIndex)
    {
        return GetNormalAttackByWeaponId(GetPrimaryWeaponId(equipmentSlots, shipSkinIndex));
    }

    public static WeaponAttackProfile GetNormalAttackForEquipmentSlot(string itemId)
    {
        return GetNormalAttackByWeaponId(GetWeaponIdForItem(itemId));
    }

    public static WeaponAttackProfile GetNormalAttackByWeaponId(string weaponId)
    {
        WeaponAttackProfile profile = CreateDefaultProfile(weaponId);

        ApplyRoomOverride(profile);
        ClampProfile(profile);
        return profile;
    }

    public static WeaponAttackProfile GetDefaultNormalAttackByWeaponId(string weaponId)
    {
        WeaponAttackProfile profile = CreateDefaultProfile(weaponId);

        ClampProfile(profile);
        return profile;
    }

    public static WeaponAttackProfile GetSuperAttack(string weaponId)
    {
        WeaponAttackProfile profile = GetNormalAttackByWeaponId(weaponId);
        if (string.Equals(profile.Id, PlasmaGunId, StringComparison.Ordinal))
        {
            profile.Id = PlasmaGunId + "_super";
            profile.DisplayName = "PLASMA SUPER";
            profile.ProjectileCount = 4;
            profile.ProjectileInterval = Mathf.Min(0.06f, Mathf.Max(0f, profile.ProjectileInterval));
            profile.ProjectileSize = Mathf.Max(profile.ProjectileSize, 2.15f);
            profile.HpDamage = Mathf.Max(profile.HpDamage, 24);
            profile.ShieldDamage = Mathf.Max(profile.ShieldDamage, 28);
        }
        else if (string.Equals(profile.Id, RailGunId, StringComparison.Ordinal))
        {
            profile.Id = RailGunId + "_super";
            profile.DisplayName = "RAIL SUPER";
            profile.ProjectileCount = 3;
            profile.ProjectileInterval = 0.075f;
            profile.ProjectileSpeed = Mathf.Max(profile.ProjectileSpeed, 34f);
            profile.HpDamage = Mathf.Max(profile.HpDamage, 42);
            profile.ShieldDamage = Mathf.Max(profile.ShieldDamage, 36);
            profile.Pierces = true;
        }
        else if (string.Equals(profile.Id, DoubleIonizerId, StringComparison.Ordinal))
        {
            profile.Id = DoubleIonizerId + "_super";
            profile.DisplayName = "IONIZER SUPER";
            profile.ProjectileCount = 8;
            profile.ProjectileInterval = 0.035f;
            profile.SpreadAngle = Mathf.Max(profile.SpreadAngle, 14f);
            profile.ShieldDamage = Mathf.Max(profile.ShieldDamage, 30);
            profile.HpDamage = Mathf.Max(profile.HpDamage, 10);
            profile.AreaDamageRadius = Mathf.Max(profile.AreaDamageRadius, 0.8f);
        }
        else if (string.Equals(profile.Id, TripleGunId, StringComparison.Ordinal))
        {
            profile.Id = TripleGunId + "_super";
            profile.DisplayName = "TRIPLE SUPER";
            profile.ProjectileCount = 7;
            profile.SpreadAngle = Mathf.Max(profile.SpreadAngle, 16f);
            profile.ProjectileInterval = 0.03f;
            profile.AttackCooldown = Mathf.Max(profile.AttackCooldown, 0.42f);
            profile.HpDamage = Mathf.Max(profile.HpDamage, 7);
            profile.ShieldDamage = Mathf.Max(profile.ShieldDamage, 7);
        }
        else if (string.Equals(profile.Id, ArtilleryGunId, StringComparison.Ordinal))
        {
            profile.Id = ArtilleryGunId + "_super";
            profile.DisplayName = "ARTILLERY SUPER";
            profile.ProjectileCount = 4;
            profile.ProjectileInterval = 0.18f;
            profile.SpreadAngle = Mathf.Max(profile.SpreadAngle, 8f);
            profile.AreaDamageRadius = Mathf.Max(profile.AreaDamageRadius, 0.82f);
            profile.HpDamage = Mathf.Max(profile.HpDamage, 24);
            profile.ShieldDamage = Mathf.Max(profile.ShieldDamage, 24);
        }
        else if (string.Equals(profile.Id, AstroCutterId, StringComparison.Ordinal))
        {
            profile.Id = AstroCutterId + "_super";
            profile.DisplayName = "ASTRO CUTTER SUPER";
            profile.RangeMultiplier = Mathf.Max(profile.RangeMultiplier, 6.25f);
            profile.FlightTime = Mathf.Max(profile.FlightTime, 2.6f);
            profile.HpDamage = Mathf.Max(profile.HpDamage, 5);
            profile.ShieldDamage = Mathf.Max(profile.ShieldDamage, 5);
            profile.AttackCooldown = Mathf.Max(profile.AttackCooldown, 2.4f);
        }
        else
        {
            profile.Id = SimpleGunId + "_super";
            profile.DisplayName = "SIMPLE GUN SUPER";
            profile.ProjectileCount = 10;
            profile.ProjectileInterval = Mathf.Min(0.04f, Mathf.Max(0f, profile.ProjectileInterval));
            profile.AttackCooldown = Mathf.Max(profile.AttackCooldown, 0.4f);
            profile.AmmoReloadTime = 0f;
        }

        ClampProfile(profile);
        return profile;
    }

    public static string GetWeaponDisplayName(string weaponId)
    {
        return GetDefaultNormalAttackByWeaponId(weaponId).DisplayName;
    }

    public static Sprite GetWeaponIcon(string weaponId)
    {
        if (string.Equals(weaponId, SimpleGunId, StringComparison.Ordinal))
            return LoadStandaloneWeaponSprite("simple_gun_resource", "simple_gun.png");

        return InventoryItemCatalog.GetIcon(weaponId);
    }

    public static bool IsEditableWeaponId(string weaponId)
    {
        return string.Equals(weaponId, SimpleGunId, StringComparison.Ordinal) ||
               string.Equals(weaponId, PlasmaGunId, StringComparison.Ordinal) ||
               string.Equals(weaponId, TripleGunId, StringComparison.Ordinal) ||
               string.Equals(weaponId, ArtilleryGunId, StringComparison.Ordinal) ||
               string.Equals(weaponId, RailGunId, StringComparison.Ordinal) ||
               string.Equals(weaponId, DoubleIonizerId, StringComparison.Ordinal) ||
               string.Equals(weaponId, AstroCutterId, StringComparison.Ordinal);
    }

    public static string SerializeProfile(WeaponAttackProfile profile)
    {
        if (profile == null)
            return string.Empty;

        ClampProfile(profile);
        StringBuilder builder = new StringBuilder();
        Append(builder, "ammo", profile.MaxAmmo.ToString(CultureInfo.InvariantCulture));
        Append(builder, "range", FormatFloat(profile.RangeMultiplier));
        Append(builder, "projectileSize", FormatFloat(profile.ProjectileSize));
        Append(builder, "projectileSpeed", FormatFloat(profile.ProjectileSpeed));
        Append(builder, "hpDamage", profile.HpDamage.ToString(CultureInfo.InvariantCulture));
        Append(builder, "shieldDamage", profile.ShieldDamage.ToString(CultureInfo.InvariantCulture));
        Append(builder, "projectileCount", profile.ProjectileCount.ToString(CultureInfo.InvariantCulture));
        Append(builder, "spreadAngle", FormatFloat(profile.SpreadAngle));
        Append(builder, "flightTime", FormatFloat(profile.FlightTime));
        Append(builder, "startDelay", FormatFloat(profile.StartDelay));
        Append(builder, "projectileInterval", FormatFloat(profile.ProjectileInterval));
        Append(builder, "attackCooldown", FormatFloat(profile.AttackCooldown));
        Append(builder, "ammoReloadTime", FormatFloat(profile.AmmoReloadTime));
        Append(builder, "hitEffectId", Escape(profile.HitEffectId ?? string.Empty));
        Append(builder, "shotSoundId", Escape(profile.ShotSoundId ?? string.Empty));
        Append(builder, "pierces", profile.Pierces ? "1" : "0");
        Append(builder, "areaDamageRadius", FormatFloat(profile.AreaDamageRadius));
        Append(builder, "markerType", profile.MarkerType.ToString().ToLowerInvariant());
        Append(builder, "markerColor", ColorToHex(profile.MarkerColor));
        return builder.ToString();
    }

    public static bool TrySetProfileValue(WeaponAttackProfile profile, WeaponAttackParameterDefinition parameter, string rawValue)
    {
        if (profile == null || parameter == null)
            return false;

        rawValue ??= string.Empty;
        switch (parameter.Key)
        {
            case "ammo":
                if (!TryParseInt(rawValue, out int ammo)) return false;
                profile.MaxAmmo = Mathf.Clamp(ammo, Mathf.RoundToInt(parameter.Min), Mathf.RoundToInt(parameter.Max));
                return true;
            case "range":
                if (!TryParseFloat(rawValue, out float range)) return false;
                profile.RangeMultiplier = Mathf.Clamp(range, parameter.Min, parameter.Max);
                return true;
            case "projectileSize":
                if (!TryParseFloat(rawValue, out float projectileSize)) return false;
                profile.ProjectileSize = Mathf.Clamp(projectileSize, parameter.Min, parameter.Max);
                return true;
            case "projectileSpeed":
                if (!TryParseFloat(rawValue, out float projectileSpeed)) return false;
                profile.ProjectileSpeed = Mathf.Clamp(projectileSpeed, parameter.Min, parameter.Max);
                return true;
            case "hpDamage":
                if (!TryParseInt(rawValue, out int hpDamage)) return false;
                profile.HpDamage = Mathf.Clamp(hpDamage, Mathf.RoundToInt(parameter.Min), Mathf.RoundToInt(parameter.Max));
                return true;
            case "shieldDamage":
                if (!TryParseInt(rawValue, out int shieldDamage)) return false;
                profile.ShieldDamage = Mathf.Clamp(shieldDamage, Mathf.RoundToInt(parameter.Min), Mathf.RoundToInt(parameter.Max));
                return true;
            case "projectileCount":
                if (!TryParseInt(rawValue, out int projectileCount)) return false;
                profile.ProjectileCount = Mathf.Clamp(projectileCount, Mathf.RoundToInt(parameter.Min), Mathf.RoundToInt(parameter.Max));
                return true;
            case "spreadAngle":
                if (!TryParseFloat(rawValue, out float spreadAngle)) return false;
                profile.SpreadAngle = Mathf.Clamp(spreadAngle, parameter.Min, parameter.Max);
                return true;
            case "flightTime":
                if (!TryParseFloat(rawValue, out float flightTime)) return false;
                profile.FlightTime = Mathf.Clamp(flightTime, parameter.Min, parameter.Max);
                return true;
            case "startDelay":
                if (!TryParseFloat(rawValue, out float startDelay)) return false;
                profile.StartDelay = Mathf.Clamp(startDelay, parameter.Min, parameter.Max);
                return true;
            case "projectileInterval":
                if (!TryParseFloat(rawValue, out float projectileInterval)) return false;
                profile.ProjectileInterval = Mathf.Clamp(projectileInterval, parameter.Min, parameter.Max);
                return true;
            case "attackCooldown":
                if (!TryParseFloat(rawValue, out float attackCooldown)) return false;
                profile.AttackCooldown = Mathf.Clamp(attackCooldown, parameter.Min, parameter.Max);
                return true;
            case "ammoReloadTime":
                if (!TryParseFloat(rawValue, out float ammoReloadTime)) return false;
                profile.AmmoReloadTime = Mathf.Clamp(ammoReloadTime, parameter.Min, parameter.Max);
                return true;
            case "hitEffectId":
                profile.HitEffectId = rawValue.Trim();
                return true;
            case "shotSoundId":
                profile.ShotSoundId = rawValue.Trim();
                return true;
            case "pierces":
                profile.Pierces = ParseBool(rawValue);
                return true;
            case "areaDamageRadius":
                if (!TryParseFloat(rawValue, out float areaDamageRadius)) return false;
                profile.AreaDamageRadius = Mathf.Clamp(areaDamageRadius, parameter.Min, parameter.Max);
                return true;
            case "markerType":
                profile.MarkerType = ParseMarkerType(rawValue);
                return true;
            case "markerColor":
                if (!TryParseColor(rawValue, out Color markerColor)) return false;
                profile.MarkerColor = markerColor;
                return true;
            default:
                return false;
        }
    }

    public static string GetProfileValueText(WeaponAttackProfile profile, WeaponAttackParameterDefinition parameter)
    {
        if (profile == null || parameter == null)
            return string.Empty;

        switch (parameter.Key)
        {
            case "ammo": return profile.MaxAmmo.ToString(CultureInfo.InvariantCulture);
            case "range": return FormatFloat(profile.RangeMultiplier);
            case "projectileSize": return FormatFloat(profile.ProjectileSize);
            case "projectileSpeed": return FormatFloat(profile.ProjectileSpeed);
            case "hpDamage": return profile.HpDamage.ToString(CultureInfo.InvariantCulture);
            case "shieldDamage": return profile.ShieldDamage.ToString(CultureInfo.InvariantCulture);
            case "projectileCount": return profile.ProjectileCount.ToString(CultureInfo.InvariantCulture);
            case "spreadAngle": return FormatFloat(profile.SpreadAngle);
            case "flightTime": return FormatFloat(profile.FlightTime);
            case "startDelay": return FormatFloat(profile.StartDelay);
            case "projectileInterval": return FormatFloat(profile.ProjectileInterval);
            case "attackCooldown": return FormatFloat(profile.AttackCooldown);
            case "ammoReloadTime": return FormatFloat(profile.AmmoReloadTime);
            case "hitEffectId": return profile.HitEffectId ?? string.Empty;
            case "shotSoundId": return profile.ShotSoundId ?? string.Empty;
            case "pierces": return profile.Pierces ? "YES" : "NO";
            case "areaDamageRadius": return FormatFloat(profile.AreaDamageRadius);
            case "markerType": return profile.MarkerType.ToString().ToUpperInvariant();
            case "markerColor": return ColorToHex(profile.MarkerColor);
            default: return string.Empty;
        }
    }

    public static string GetRoomSetupSignature()
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < EditableWeaponIds.Length; i++)
        {
            if (i > 0)
                builder.Append(';');

            string weaponId = EditableWeaponIds[i];
            builder.Append(weaponId);
            builder.Append('=');
            builder.Append(RoomSettings.GetGunSetupOverride(weaponId));
        }

        return builder.ToString();
    }

    public static string GetPrimaryWeaponId(string[] equipmentSlots, int shipSkinIndex)
    {
        if (equipmentSlots == null)
            return SimpleGunId;

        for (int slot = 0; slot < 2; slot++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(slot, shipSkinIndex))
                continue;

            string weaponId = GetWeaponIdForItem(slot < equipmentSlots.Length ? equipmentSlots[slot] : null);
            if (!string.Equals(weaponId, SimpleGunId, StringComparison.Ordinal))
                return weaponId;
        }

        return SimpleGunId;
    }

    public static string GetWeaponIdForItem(string itemId)
    {
        if (string.Equals(itemId, PlasmaGunId, StringComparison.Ordinal))
            return PlasmaGunId;

        if (string.Equals(itemId, TripleGunId, StringComparison.Ordinal))
            return TripleGunId;

        if (string.Equals(itemId, ArtilleryGunId, StringComparison.Ordinal))
            return ArtilleryGunId;

        if (string.Equals(itemId, RailGunId, StringComparison.Ordinal))
            return RailGunId;

        if (string.Equals(itemId, DoubleIonizerId, StringComparison.Ordinal))
            return DoubleIonizerId;

        if (string.Equals(itemId, AstroCutterId, StringComparison.Ordinal))
            return AstroCutterId;

        return SimpleGunId;
    }

    public static bool IsWeaponItemId(string itemId)
    {
        return !string.Equals(GetWeaponIdForItem(itemId), SimpleGunId, StringComparison.Ordinal);
    }

    static WeaponAttackProfile CreateDefaultProfile(string weaponId)
    {
        if (string.Equals(weaponId, PlasmaGunId, StringComparison.Ordinal))
            return CreatePlasmaGunNormal();

        if (string.Equals(weaponId, TripleGunId, StringComparison.Ordinal))
            return CreateTripleGunNormal();

        if (string.Equals(weaponId, ArtilleryGunId, StringComparison.Ordinal))
            return CreateArtilleryGunNormal();

        if (string.Equals(weaponId, RailGunId, StringComparison.Ordinal))
            return CreateRailGunNormal();

        if (string.Equals(weaponId, DoubleIonizerId, StringComparison.Ordinal))
            return CreateDoubleIonizerNormal();

        if (string.Equals(weaponId, AstroCutterId, StringComparison.Ordinal))
            return CreateAstroCutterNormal();

        return CreateSimpleGunNormal();
    }

    static WeaponAttackProfile CreateSimpleGunNormal()
    {
        return new WeaponAttackProfile
        {
            Id = SimpleGunId,
            DisplayName = "SIMPLE GUN",
            MaxAmmo = 4,
            RangeMultiplier = 15f,
            ProjectileSize = 1f,
            ProjectileSpeed = 10f,
            HpDamage = 6,
            ShieldDamage = 6,
            ProjectileCount = 1,
            SpreadAngle = 1f,
            FlightTime = 10f,
            ProjectileInterval = 0.065f,
            AttackCooldown = 0.28f,
            AmmoReloadTime = 2.3f,
            StartDelay = 0f,
            HitEffectId = "default",
            ShotSoundId = "laser",
            Pierces = false,
            AreaDamageRadius = 0f,
            ProjectileColor = Color.white,
            MarkerType = ComplexAttackMarkerType.Line,
            MarkerColor = SimpleMarkerColor
        };
    }

    static WeaponAttackProfile CreatePlasmaGunNormal()
    {
        return new WeaponAttackProfile
        {
            Id = PlasmaGunId,
            DisplayName = "PLASMA GUN",
            MaxAmmo = 6,
            RangeMultiplier = 26f,
            ProjectileSize = 3f,
            ProjectileSpeed = 11.5f,
            HpDamage = 20,
            ShieldDamage = 20,
            ProjectileCount = 1,
            SpreadAngle = 0f,
            FlightTime = 10f,
            ProjectileInterval = 0f,
            AttackCooldown = 0.34f,
            AmmoReloadTime = 1.8f,
            StartDelay = 0f,
            HitEffectId = "plasma",
            ShotSoundId = "corsair",
            Pierces = false,
            AreaDamageRadius = 0f,
            ProjectileColor = PlasmaColor,
            MarkerType = ComplexAttackMarkerType.Line,
            MarkerColor = PlasmaMarkerColor
        };
    }

    static WeaponAttackProfile CreateTripleGunNormal()
    {
        return new WeaponAttackProfile
        {
            Id = TripleGunId,
            DisplayName = "TRIPLE GUN",
            MaxAmmo = 4,
            RangeMultiplier = 15f,
            ProjectileSize = 0.92f,
            ProjectileSpeed = 10f,
            HpDamage = 5,
            ShieldDamage = 5,
            ProjectileCount = 3,
            SpreadAngle = 8f,
            FlightTime = 10f,
            ProjectileInterval = 0.03f,
            AttackCooldown = 0.34f,
            AmmoReloadTime = 2.3f,
            StartDelay = 0f,
            HitEffectId = "default",
            ShotSoundId = "laser",
            Pierces = false,
            AreaDamageRadius = 0f,
            ProjectileColor = TripleColor,
            MarkerType = ComplexAttackMarkerType.Line,
            MarkerColor = TripleMarkerColor
        };
    }

    static WeaponAttackProfile CreateArtilleryGunNormal()
    {
        return new WeaponAttackProfile
        {
            Id = ArtilleryGunId,
            DisplayName = "ARTILLERY GUN",
            MaxAmmo = 3,
            RangeMultiplier = 5f,
            ProjectileSize = 1.65f,
            ProjectileSpeed = 1f,
            HpDamage = 18,
            ShieldDamage = 18,
            ProjectileCount = 1,
            SpreadAngle = 0f,
            FlightTime = 1.15f,
            ProjectileInterval = 0f,
            AttackCooldown = 0.92f,
            AmmoReloadTime = 3.8f,
            StartDelay = 0.05f,
            HitEffectId = "artillery",
            ShotSoundId = "artillery",
            Pierces = false,
            AreaDamageRadius = 1.0f,
            ProjectileColor = ArtilleryColor,
            MarkerType = ComplexAttackMarkerType.Arc,
            MarkerColor = ArtilleryMarkerColor
        };
    }

    static WeaponAttackProfile CreateRailGunNormal()
    {
        return new WeaponAttackProfile
        {
            Id = RailGunId,
            DisplayName = "RAIL GUN",
            MaxAmmo = 3,
            RangeMultiplier = 42f,
            ProjectileSize = 0.65f,
            ProjectileSpeed = 32f,
            HpDamage = 44,
            ShieldDamage = 18,
            ProjectileCount = 1,
            SpreadAngle = 0f,
            FlightTime = 10f,
            ProjectileInterval = 0f,
            AttackCooldown = 0.85f,
            AmmoReloadTime = 4.8f,
            StartDelay = 0.08f,
            HitEffectId = "rail",
            ShotSoundId = "lazer1",
            Pierces = true,
            AreaDamageRadius = 0f,
            ProjectileColor = RailColor,
            MarkerType = ComplexAttackMarkerType.Line,
            MarkerColor = RailMarkerColor
        };
    }

    static WeaponAttackProfile CreateDoubleIonizerNormal()
    {
        return new WeaponAttackProfile
        {
            Id = DoubleIonizerId,
            DisplayName = "DOUBLE IONIZER",
            MaxAmmo = 8,
            RangeMultiplier = 18f,
            ProjectileSize = 1.15f,
            ProjectileSpeed = 13f,
            HpDamage = 8,
            ShieldDamage = 30,
            ProjectileCount = 2,
            SpreadAngle = 8f,
            FlightTime = 10f,
            ProjectileInterval = 0.02f,
            AttackCooldown = 0.3f,
            AmmoReloadTime = 1.44f,
            StartDelay = 0f,
            HitEffectId = "ion",
            ShotSoundId = "lazer2",
            Pierces = false,
            AreaDamageRadius = 0.65f,
            ProjectileColor = IonColor,
            MarkerType = ComplexAttackMarkerType.Line,
            MarkerColor = IonMarkerColor
        };
    }

    static WeaponAttackProfile CreateAstroCutterNormal()
    {
        return new WeaponAttackProfile
        {
            Id = AstroCutterId,
            DisplayName = "ASTRO CUTTER",
            MaxAmmo = 3,
            RangeMultiplier = 5.25f,
            ProjectileSize = 0.92f,
            ProjectileSpeed = 0f,
            HpDamage = 3,
            ShieldDamage = 3,
            ProjectileCount = 1,
            SpreadAngle = 0f,
            FlightTime = 2f,
            ProjectileInterval = 0f,
            AttackCooldown = 2.15f,
            AmmoReloadTime = 9.6f,
            StartDelay = 0f,
            HitEffectId = "astro_cutter",
            ShotSoundId = "astro_cutter",
            Pierces = true,
            AreaDamageRadius = 0f,
            ProjectileColor = AstroCutterColor,
            MarkerType = ComplexAttackMarkerType.Line,
            MarkerColor = AstroCutterMarkerColor
        };
    }

    static void ApplyRoomOverride(WeaponAttackProfile profile)
    {
        if (profile == null)
            return;

        string raw = RoomSettings.GetGunSetupOverride(profile.Id);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        Dictionary<string, string> values = ParseSerializedProfile(raw);
        IReadOnlyList<WeaponAttackParameterDefinition> parameters = GetEditableParameters();
        for (int i = 0; i < parameters.Count; i++)
        {
            WeaponAttackParameterDefinition parameter = parameters[i];
            if (parameter == null || !values.TryGetValue(parameter.Key, out string value))
                continue;

            TrySetProfileValue(profile, parameter, value);
        }
    }

    static Dictionary<string, string> ParseSerializedProfile(string raw)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
            return values;

        string[] parts = raw.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (string.IsNullOrWhiteSpace(part))
                continue;

            int separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            string key = part.Substring(0, separatorIndex);
            string value = part.Substring(separatorIndex + 1);
            values[key] = Unescape(value);
        }

        return values;
    }

    static void ClampProfile(WeaponAttackProfile profile)
    {
        if (profile == null)
            return;

        profile.MaxAmmo = Mathf.Clamp(profile.MaxAmmo, 1, 30);
        profile.RangeMultiplier = Mathf.Clamp(profile.RangeMultiplier, 1f, 80f);
        profile.ProjectileSize = Mathf.Clamp(profile.ProjectileSize, 0.2f, 8f);
        profile.ProjectileSpeed = Mathf.Clamp(profile.ProjectileSpeed, 0.5f, 40f);
        profile.HpDamage = Mathf.Clamp(profile.HpDamage, 0, 300);
        profile.ShieldDamage = Mathf.Clamp(profile.ShieldDamage, 0, 300);
        profile.ProjectileCount = Mathf.Clamp(profile.ProjectileCount, 1, 30);
        profile.SpreadAngle = Mathf.Clamp(profile.SpreadAngle, 0f, 180f);
        profile.FlightTime = Mathf.Clamp(profile.FlightTime, 0.2f, 30f);
        profile.ProjectileInterval = Mathf.Clamp(profile.ProjectileInterval, 0f, 3f);
        profile.AttackCooldown = Mathf.Clamp(profile.AttackCooldown, 0.03f, 5f);
        profile.AmmoReloadTime = Mathf.Clamp(profile.AmmoReloadTime, 0f, 15f);
        profile.StartDelay = Mathf.Clamp(profile.StartDelay, 0f, 5f);
        profile.AreaDamageRadius = Mathf.Clamp(profile.AreaDamageRadius, 0f, 12f);
        if (!Enum.IsDefined(typeof(ComplexAttackMarkerType), profile.MarkerType))
            profile.MarkerType = ComplexAttackMarkerType.Line;
        profile.MarkerColor.a = 1f;
    }

    static void Append(StringBuilder builder, string key, string value)
    {
        if (builder.Length > 0)
            builder.Append('|');

        builder.Append(key);
        builder.Append('=');
        builder.Append(value ?? string.Empty);
    }

    static string Escape(string value)
    {
        return (value ?? string.Empty)
            .Replace("%", "%25")
            .Replace("|", "%7C")
            .Replace("=", "%3D");
    }

    static string Unescape(string value)
    {
        return (value ?? string.Empty)
            .Replace("%3D", "=")
            .Replace("%7C", "|")
            .Replace("%25", "%");
    }

    static bool TryParseInt(string raw, out int value)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ||
               int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
    }

    static bool TryParseFloat(string raw, out float value)
    {
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    static bool ParseBool(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string normalized = raw.Trim().ToLowerInvariant();
        return normalized == "1" ||
               normalized == "true" ||
               normalized == "yes" ||
               normalized == "on";
    }

    static ComplexAttackMarkerType ParseMarkerType(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return ComplexAttackMarkerType.Line;

        return string.Equals(rawValue.Trim(), "arc", StringComparison.OrdinalIgnoreCase)
            ? ComplexAttackMarkerType.Arc
            : ComplexAttackMarkerType.Line;
    }

    static string FormatFloat(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string ColorToHex(Color color)
    {
        Color32 c = color;
        return "#" + c.r.ToString("X2", CultureInfo.InvariantCulture) +
               c.g.ToString("X2", CultureInfo.InvariantCulture) +
               c.b.ToString("X2", CultureInfo.InvariantCulture);
    }

    public static bool TryParseColor(string raw, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string hex = raw.Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal))
            hex = hex.Substring(1);

        if (hex.Length != 6 ||
            !byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        color = new Color32(r, g, b, 255);
        return true;
    }

    static Sprite LoadStandaloneWeaponSprite(string resourcesPath, string projectFileName)
    {
        if (!string.IsNullOrWhiteSpace(resourcesPath))
        {
            Sprite resourceSprite = Resources.Load<Sprite>(resourcesPath);
            if (resourceSprite != null)
                return resourceSprite;

            Texture2D resourceTexture = Resources.Load<Texture2D>(resourcesPath);
            if (resourceTexture != null)
            {
                return Sprite.Create(
                    resourceTexture,
                    new Rect(0f, 0f, resourceTexture.width, resourceTexture.height),
                    new Vector2(0.5f, 0.5f),
                    Mathf.Max(100f, Mathf.Max(resourceTexture.width, resourceTexture.height)));
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(projectFileName))
        {
            string assetPath = "Assets/" + projectFileName;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
                return sprite;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    Mathf.Max(100f, Mathf.Max(texture.width, texture.height)));
            }
        }
#endif

        return null;
    }
}
