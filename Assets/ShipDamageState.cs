using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public enum ShipDamageType
{
    Engine = 0,
    Ammo = 1,
    SecondaryWeapon = 2,
    Shield = 3,
    Steering = 4,
    Booster = 5,
    Cargo = 6,
    LuckyNoDamage = 7
}

public enum ShipDamageSeverity
{
    None = 0,
    Light = 1,
    Heavy = 2
}

public struct ShipDamageInfo
{
    public ShipDamageType Type;
    public ShipDamageSeverity Severity;
}

[RequireComponent(typeof(PhotonView))]
public sealed class ShipDamageState : MonoBehaviourPun
{
    const int DamageTypeCount = 7;
    const int NoDamageMarkerBit = DamageTypeCount * 2;
    const float NoDamageChance = 0.4f;
    const float LightDamageChance = 0.4f;

    static readonly ShipDamageType[] DamageTypes =
    {
        ShipDamageType.Engine,
        ShipDamageType.Ammo,
        ShipDamageType.SecondaryWeapon,
        ShipDamageType.Shield,
        ShipDamageType.Steering,
        ShipDamageType.Booster,
        ShipDamageType.Cargo
    };

    readonly ShipDamageSeverity[] severities = new ShipDamageSeverity[DamageTypeCount];
    readonly List<ShipDamageType> candidateTypes = new List<ShipDamageType>(DamageTypeCount);
    bool noDamageMarkerActive;
    int version;

    public int Version => version;
    public bool HasAnyDamage
    {
        get
        {
            for (int i = 0; i < severities.Length; i++)
            {
                if (severities[i] != ShipDamageSeverity.None)
                    return true;
            }

            return false;
        }
    }

    public ShipDamageSeverity GetSeverity(ShipDamageType type)
    {
        int index = (int)type;
        return index >= 0 && index < severities.Length ? severities[index] : ShipDamageSeverity.None;
    }

    public bool HasDamage(ShipDamageType type)
    {
        return GetSeverity(type) != ShipDamageSeverity.None;
    }

    public void CopyActiveDamages(List<ShipDamageInfo> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (noDamageMarkerActive)
        {
            results.Add(new ShipDamageInfo
            {
                Type = ShipDamageType.LuckyNoDamage,
                Severity = ShipDamageSeverity.None
            });
        }

        for (int i = 0; i < DamageTypes.Length; i++)
        {
            ShipDamageType type = DamageTypes[i];
            ShipDamageSeverity severity = GetSeverity(type);
            if (severity == ShipDamageSeverity.None)
                continue;

            results.Add(new ShipDamageInfo
            {
                Type = type,
                Severity = severity
            });
        }
    }

    public bool TryRollBelowHalfHpDamageAuthority()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        float roll = UnityEngine.Random.value;
        if (roll < NoDamageChance)
        {
            SetNoDamageMarkerAuthority();
            return false;
        }

        ShipDamageSeverity severity = roll < NoDamageChance + LightDamageChance
            ? ShipDamageSeverity.Light
            : ShipDamageSeverity.Heavy;

        return TryApplyRandomDamageAuthority(severity);
    }

    public bool TryApplyRandomDamageAuthority(ShipDamageSeverity severity)
    {
        if (!PhotonNetwork.IsMasterClient || severity == ShipDamageSeverity.None)
            return false;

        candidateTypes.Clear();
        for (int i = 0; i < DamageTypes.Length; i++)
        {
            ShipDamageType type = DamageTypes[i];
            if (GetSeverity(type) < severity && IsDamageTypeApplicable(type))
                candidateTypes.Add(type);
        }

        if (candidateTypes.Count == 0)
            return false;

        ShipDamageType selectedType = candidateTypes[UnityEngine.Random.Range(0, candidateTypes.Count)];
        return SetDamageAuthority(selectedType, severity);
    }

    public bool RepairWorstDamageAuthority()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        ShipDamageType bestType = ShipDamageType.Engine;
        ShipDamageSeverity bestSeverity = ShipDamageSeverity.None;
        for (int i = 0; i < DamageTypes.Length; i++)
        {
            ShipDamageType type = DamageTypes[i];
            ShipDamageSeverity severity = GetSeverity(type);
            if (severity > bestSeverity)
            {
                bestSeverity = severity;
                bestType = type;
            }
        }

        if (bestSeverity == ShipDamageSeverity.None)
            return false;

        SetSeverityLocal(bestType, ShipDamageSeverity.None);
        SyncAllDamageAuthority();
        return true;
    }

    public float GetEngineSpeedMultiplier()
    {
        ShipDamageSeverity severity = GetSeverity(ShipDamageType.Engine);
        if (severity == ShipDamageSeverity.Heavy)
            return 0.6f;

        return severity == ShipDamageSeverity.Light ? 0.8f : 1f;
    }

    public int GetAdjustedAmmoMax(int baseMaxAmmo)
    {
        int safeBase = Mathf.Max(1, baseMaxAmmo);
        ShipDamageSeverity severity = GetSeverity(ShipDamageType.Ammo);
        if (severity == ShipDamageSeverity.Heavy)
            return Mathf.Max(1, Mathf.FloorToInt(safeBase * 0.5f));

        if (severity == ShipDamageSeverity.Light)
            return Mathf.Max(1, safeBase - 2);

        return safeBase;
    }

    public int GetShieldRepairCap(int maxShield)
    {
        int safeMax = Mathf.Max(0, maxShield);
        ShipDamageSeverity severity = GetSeverity(ShipDamageType.Shield);
        if (severity == ShipDamageSeverity.Heavy)
            return 0;

        if (severity == ShipDamageSeverity.Light)
            return Mathf.FloorToInt(safeMax * 0.5f);

        return safeMax;
    }

    public Vector2 ApplySteeringDamage(Vector2 input)
    {
        ShipDamageSeverity severity = GetSeverity(ShipDamageType.Steering);
        if (severity == ShipDamageSeverity.None || input.sqrMagnitude < 0.0001f)
            return input;

        float strength = severity == ShipDamageSeverity.Heavy ? 1f : 0.45f;
        int actorSeed = photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : Math.Abs(name.GetHashCode());
        float side = actorSeed % 2 == 0 ? 1f : -1f;
        float wobble = Mathf.Sin((Time.time * (1.45f + strength * 0.55f)) + actorSeed * 0.37f);
        float angle = side * Mathf.Lerp(7f, 22f, strength) + wobble * Mathf.Lerp(4f, 13f, strength);
        return ((Vector2)(Quaternion.Euler(0f, 0f, angle) * input)).normalized * input.magnitude;
    }

    public bool IsBoosterDisabled()
    {
        return GetSeverity(ShipDamageType.Booster) == ShipDamageSeverity.Heavy;
    }

    public float GetBoosterChargeLimit()
    {
        ShipDamageSeverity severity = GetSeverity(ShipDamageType.Booster);
        if (severity == ShipDamageSeverity.Heavy)
            return 0f;

        return severity == ShipDamageSeverity.Light ? 0.5f : 1f;
    }

    public int GetCargoSlotPenalty()
    {
        ShipDamageSeverity severity = GetSeverity(ShipDamageType.Cargo);
        if (severity == ShipDamageSeverity.Heavy)
            return 2;

        return severity == ShipDamageSeverity.Light ? 1 : 0;
    }

    public int GetAdjustedCargoCapacity(int baseCapacity)
    {
        return Mathf.Clamp(baseCapacity - GetCargoSlotPenalty(), 0, PlayerInventoryData.ShipSlotCount);
    }

    public string GetAmmoDamageSignature()
    {
        return ((int)GetSeverity(ShipDamageType.Ammo)).ToString();
    }

    public static ShipDamageState GetLocalPlayerState()
    {
        GameObject taggedObject = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.TagObject as GameObject : null;
        ShipDamageState taggedState = taggedObject != null ? taggedObject.GetComponent<ShipDamageState>() : null;
        if (taggedState != null)
            return taggedState;

        ShipDamageState[] states = FindObjectsByType<ShipDamageState>(FindObjectsInactive.Exclude);
        for (int i = 0; i < states.Length; i++)
        {
            ShipDamageState state = states[i];
            if (state != null && state.photonView != null && state.photonView.IsMine)
                return state;
        }

        return null;
    }

    public static int GetLocalCargoAdjustedCapacity(int baseCapacity)
    {
        ShipDamageState state = GetLocalPlayerState();
        return state != null ? state.GetAdjustedCargoCapacity(baseCapacity) : baseCapacity;
    }

    public static void ClearAllRuntimeDamage()
    {
        ShipDamageState[] states = FindObjectsByType<ShipDamageState>(FindObjectsInactive.Include);
        for (int i = 0; i < states.Length; i++)
        {
            ShipDamageState state = states[i];
            if (state == null)
                continue;

            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && state.photonView != null)
                state.photonView.RPC(nameof(ClearAllDamageRpc), RpcTarget.All);
            else
                state.ClearAllLocal();
        }
    }

    public void ClearAllLocalDamageForNewShip()
    {
        ClearAllLocal();
    }

    bool SetDamageAuthority(ShipDamageType type, ShipDamageSeverity severity)
    {
        if (!PhotonNetwork.IsMasterClient || severity == ShipDamageSeverity.None || GetSeverity(type) >= severity || !IsDamageTypeApplicable(type))
            return false;

        noDamageMarkerActive = false;
        SetSeverityLocal(type, severity);
        SyncAllDamageAuthority();
        return true;
    }

    void SetNoDamageMarkerAuthority()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!noDamageMarkerActive)
        {
            noDamageMarkerActive = true;
            version++;
        }

        SyncAllDamageAuthority();
    }

    bool IsDamageTypeApplicable(ShipDamageType type)
    {
        switch (type)
        {
            case ShipDamageType.SecondaryWeapon:
                return HasSecondWeaponOption();
            case ShipDamageType.Shield:
                PlayerHealth health = GetComponent<PlayerHealth>();
                return health == null || health.MaxShield > 0;
            case ShipDamageType.Cargo:
                return GetBaseCargoCapacity() > 0;
            default:
                return true;
        }
    }

    bool HasSecondWeaponOption()
    {
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null && shooting.ComplexWeaponCount > 1)
            return true;

        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        int enabledWeaponSlots = 0;
        for (int slot = 0; slot < 2; slot++)
        {
            if (ShipCatalog.IsEquipmentSlotEnabled(slot, shipSkinIndex))
                enabledWeaponSlots++;
        }

        return enabledWeaponSlots > 1;
    }

    int GetBaseCargoCapacity()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, equipmentSlots);
    }

    void SyncAllDamageAuthority()
    {
        if (photonView != null)
            photonView.RPC(nameof(SyncShipDamageRpc), RpcTarget.All, PackSeverities());
    }

    void SetSeverityLocal(ShipDamageType type, ShipDamageSeverity severity)
    {
        int index = (int)type;
        if (index < 0 || index >= severities.Length)
            return;

        if (severities[index] == severity)
            return;

        severities[index] = severity;
        version++;
    }

    int PackSeverities()
    {
        int packed = 0;
        for (int i = 0; i < severities.Length; i++)
            packed |= Mathf.Clamp((int)severities[i], 0, 3) << (i * 2);

        if (noDamageMarkerActive)
            packed |= 1 << NoDamageMarkerBit;

        return packed;
    }

    void UnpackSeverities(int packed)
    {
        bool changed = false;
        bool packedNoDamageMarker = (packed & (1 << NoDamageMarkerBit)) != 0;
        if (noDamageMarkerActive != packedNoDamageMarker)
        {
            noDamageMarkerActive = packedNoDamageMarker;
            changed = true;
        }

        for (int i = 0; i < severities.Length; i++)
        {
            ShipDamageSeverity severity = (ShipDamageSeverity)((packed >> (i * 2)) & 0x3);
            if (severity > ShipDamageSeverity.Heavy)
                severity = ShipDamageSeverity.None;

            if (severities[i] == severity)
                continue;

            severities[i] = severity;
            changed = true;
        }

        if (changed)
            version++;
    }

    void ClearAllLocal()
    {
        bool changed = noDamageMarkerActive;
        noDamageMarkerActive = false;
        for (int i = 0; i < severities.Length; i++)
        {
            if (severities[i] == ShipDamageSeverity.None)
                continue;

            severities[i] = ShipDamageSeverity.None;
            changed = true;
        }

        if (changed)
            version++;
    }

    [PunRPC]
    void SyncShipDamageRpc(int packed)
    {
        UnpackSeverities(packed);
    }

    [PunRPC]
    void ClearAllDamageRpc()
    {
        ClearAllLocal();
    }
}

public static class ShipDamageIconFactory
{
    const int Size = 64;
    static readonly Dictionary<ShipDamageType, Sprite> Sprites = new Dictionary<ShipDamageType, Sprite>();

    public static Sprite GetIcon(ShipDamageType type)
    {
        if (Sprites.TryGetValue(type, out Sprite sprite) && sprite != null)
            return sprite;

        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = "ShipDamageIcon_" + type,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Clear(texture);
        DrawIcon(texture, type);
        texture.Apply(false, true);

        sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = "ShipDamageIconSprite_" + type;
        Sprites[type] = sprite;
        return sprite;
    }

    static void DrawIcon(Texture2D texture, ShipDamageType type)
    {
        switch (type)
        {
            case ShipDamageType.Engine:
                FillPolygon(texture, new Vector2[]
                {
                    new Vector2(18f, 19f), new Vector2(46f, 19f),
                    new Vector2(42f, 40f), new Vector2(22f, 40f)
                }, Color.white);
                DrawLine(texture, 24, 45, 40, 45, 5);
                DrawLine(texture, 27, 51, 37, 51, 5);
                FillPolygon(texture, new Vector2[] { new Vector2(20f, 17f), new Vector2(26f, 8f), new Vector2(30f, 17f) }, Color.white);
                FillPolygon(texture, new Vector2[] { new Vector2(34f, 17f), new Vector2(38f, 8f), new Vector2(44f, 17f) }, Color.white);
                break;
            case ShipDamageType.Ammo:
                FillPolygon(texture, new Vector2[]
                {
                    new Vector2(32f, 7f), new Vector2(43f, 23f),
                    new Vector2(43f, 50f), new Vector2(21f, 50f),
                    new Vector2(21f, 23f)
                }, Color.white);
                DrawLine(texture, 22, 24, 42, 24, 3, ClearColor());
                DrawLine(texture, 22, 43, 42, 43, 3, ClearColor());
                break;
            case ShipDamageType.SecondaryWeapon:
                DrawLine(texture, 14, 45, 45, 18, 10);
                DrawLine(texture, 26, 52, 52, 29, 9);
                FillRect(texture, 42, 14, 56, 22, Color.white);
                FillRect(texture, 47, 22, 56, 34, Color.white);
                DrawLine(texture, 11, 13, 53, 55, 8);
                break;
            case ShipDamageType.Shield:
                FillPolygon(texture, new Vector2[]
                {
                    new Vector2(32f, 7f), new Vector2(51f, 16f),
                    new Vector2(47f, 40f), new Vector2(32f, 56f),
                    new Vector2(17f, 40f), new Vector2(13f, 16f)
                }, Color.white);
                DrawLine(texture, 33, 13, 27, 29, 4, ClearColor());
                DrawLine(texture, 27, 29, 36, 33, 4, ClearColor());
                DrawLine(texture, 36, 33, 29, 50, 4, ClearColor());
                break;
            case ShipDamageType.Steering:
                FillCircle(texture, 23, 47, 9, Color.white);
                DrawLine(texture, 24, 44, 38, 27, 8);
                FillCircle(texture, 41, 22, 8, Color.white);
                DrawLine(texture, 36, 45, 53, 45, 5);
                DrawLine(texture, 48, 39, 55, 45, 5);
                DrawLine(texture, 48, 51, 55, 45, 5);
                break;
            case ShipDamageType.Booster:
                FillPolygon(texture, new Vector2[]
                {
                    new Vector2(32f, 5f), new Vector2(45f, 27f),
                    new Vector2(39f, 46f), new Vector2(25f, 46f),
                    new Vector2(19f, 27f)
                }, Color.white);
                DrawLine(texture, 32, 17, 32, 34, 5, ClearColor());
                FillPolygon(texture, new Vector2[] { new Vector2(25f, 48f), new Vector2(18f, 60f), new Vector2(30f, 52f) }, Color.white);
                FillPolygon(texture, new Vector2[] { new Vector2(34f, 52f), new Vector2(46f, 60f), new Vector2(39f, 48f) }, Color.white);
                DrawLine(texture, 32, 50, 32, 61, 6);
                break;
            case ShipDamageType.Cargo:
                FillPolygon(texture, new Vector2[]
                {
                    new Vector2(15f, 21f), new Vector2(32f, 12f),
                    new Vector2(49f, 21f), new Vector2(49f, 44f),
                    new Vector2(32f, 54f), new Vector2(15f, 44f)
                }, Color.white);
                DrawLine(texture, 32, 13, 32, 35, 4, ClearColor());
                DrawLine(texture, 16, 22, 32, 32, 4, ClearColor());
                DrawLine(texture, 48, 22, 32, 32, 4, ClearColor());
                DrawLine(texture, 39, 30, 32, 40, 5, ClearColor());
                DrawLine(texture, 32, 40, 39, 49, 5, ClearColor());
                break;
            case ShipDamageType.LuckyNoDamage:
                FillCircle(texture, 32, 32, 25, Color.white);
                FillCircle(texture, 32, 32, 20, ClearColor());
                FillCircle(texture, 24, 36, 4, Color.white);
                FillCircle(texture, 40, 36, 4, Color.white);
                DrawLine(texture, 21, 25, 27, 20, 4);
                DrawLine(texture, 27, 20, 37, 20, 4);
                DrawLine(texture, 37, 20, 43, 25, 4);
                break;
        }
    }

    static void Clear(Texture2D texture)
    {
        Color clear = ClearColor();
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
                texture.SetPixel(x, y, clear);
        }
    }

    static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int thickness)
    {
        DrawLine(texture, x0, y0, x1, y1, thickness, Color.white);
    }

    static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawBrush(texture, x0, y0, thickness, color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    static void DrawBrush(Texture2D texture, int centerX, int centerY, int thickness)
    {
        DrawBrush(texture, centerX, centerY, thickness, Color.white);
    }

    static void DrawBrush(Texture2D texture, int centerX, int centerY, int thickness, Color color)
    {
        int radius = Mathf.Max(1, thickness / 2);
        int radiusSq = radius * radius;
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            if (y < 0 || y >= Size)
                continue;

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || x >= Size)
                    continue;

                int ox = x - centerX;
                int oy = y - centerY;
                if (ox * ox + oy * oy <= radiusSq)
                    texture.SetPixel(x, y, color);
            }
        }
    }

    static void FillRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
    {
        for (int y = Mathf.Max(0, minY); y <= Mathf.Min(Size - 1, maxY); y++)
        {
            for (int x = Mathf.Max(0, minX); x <= Mathf.Min(Size - 1, maxX); x++)
                texture.SetPixel(x, y, color);
        }
    }

    static void FillCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
    {
        int radiusSq = radius * radius;
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            if (y < 0 || y >= Size)
                continue;

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || x >= Size)
                    continue;

                int ox = x - centerX;
                int oy = y - centerY;
                if (ox * ox + oy * oy <= radiusSq)
                    texture.SetPixel(x, y, color);
            }
        }
    }

    static void FillPolygon(Texture2D texture, Vector2[] points, Color color)
    {
        if (points == null || points.Length < 3)
            return;

        float minX = points[0].x;
        float maxX = points[0].x;
        float minY = points[0].y;
        float maxY = points[0].y;
        for (int i = 1; i < points.Length; i++)
        {
            minX = Mathf.Min(minX, points[i].x);
            maxX = Mathf.Max(maxX, points[i].x);
            minY = Mathf.Min(minY, points[i].y);
            maxY = Mathf.Max(maxY, points[i].y);
        }

        int startX = Mathf.Clamp(Mathf.FloorToInt(minX), 0, Size - 1);
        int endX = Mathf.Clamp(Mathf.CeilToInt(maxX), 0, Size - 1);
        int startY = Mathf.Clamp(Mathf.FloorToInt(minY), 0, Size - 1);
        int endY = Mathf.Clamp(Mathf.CeilToInt(maxY), 0, Size - 1);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), points))
                    texture.SetPixel(x, y, color);
            }
        }
    }

    static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;
        for (int i = 0; i < polygon.Length; i++)
        {
            bool intersects = (polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                              point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                              (polygon[j].y - polygon[i].y) + polygon[i].x;
            if (intersects)
                inside = !inside;

            j = i;
        }

        return inside;
    }

    static Color ClearColor()
    {
        return new Color(1f, 1f, 1f, 0f);
    }
}
