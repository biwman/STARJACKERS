using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum EnemyBotKind
{
    Drone,
    Corsair,
    SpaceMine,
    SpaceTruck,
    Mothership,
    NeutralFighter,
    RadarShip,
    RescueShip,
    PirateFighter,
    PirateFighterElite,
    PirateFighterAce,
    SpaceManta,
    PirateBase,
    GravitySquid,
    HunterLance,
    ContainerShip,
    CosmicWorm,
    RiftWarden,
    MilitaryVan
}

public enum EnemyMovementModel
{
    GuardAndChase,
    OrbitMap,
    Drift,
    RouteExtractionZones,
    Mothership,
    NeutralFighter,
    RadarShip,
    RescueShip,
    PirateFighter,
    PirateBase,
    SpaceManta,
    GravitySquid,
    HunterLance,
    ContainerShip,
    CosmicWorm,
    RiftWarden,
    MilitaryVan
}

public enum EnemySpawnPattern
{
    SafePerimeter,
    WideCorners
}

public enum EnemyTrailVisualStyle
{
    None,
    OrangeSmall,
    RedLarge,
    GreenTwin,
    BlueTwin,
    OrangeRedTwin,
    PurpleLarge
}

[System.Serializable]
public class EnemyExplosionProfile
{
    Sprite[] cachedFrames;

    public int Damage;
    public WeaponDamageType DamageType;
    public WeaponDeliveryMethod DeliveryMethod;
    public WeaponDeliveryFlags DeliveryFlags;
    public float TriggerRadius;
    public float VisualTargetSize;
    public float VisualDuration;
    public int VisualStartFrame;
    public int VisualColumns;
    public int VisualRows;
    public string VisualResourcePath;
    public string EditorAssetPath;
    public string SoundId;

    public Sprite[] GetVisualFrames()
    {
        if (cachedFrames != null && cachedFrames.Length > 0)
            return cachedFrames;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedFrames = Resources.LoadAll<Sprite>(VisualResourcePath);
            if (cachedFrames != null && cachedFrames.Length > 0)
            {
                SortSpritesForAnimation(cachedFrames);
                return cachedFrames;
            }

            Sprite single = Resources.Load<Sprite>(VisualResourcePath);
            if (single != null)
            {
                cachedFrames = new[] { single };
                return cachedFrames;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    sprites.Add(sprite);
            }

            if (sprites.Count > 0)
            {
                sprites.Sort(CompareSpritesForAnimation);
                cachedFrames = sprites.ToArray();
                return cachedFrames;
            }

            Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (single != null)
            {
                cachedFrames = new[] { single };
                return cachedFrames;
            }
        }
#endif

        return System.Array.Empty<Sprite>();
    }

    static void SortSpritesForAnimation(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length <= 1)
            return;

        System.Array.Sort(sprites, CompareSpritesForAnimation);
    }

    static int CompareSpritesForAnimation(Sprite a, Sprite b)
    {
        string nameA = a != null ? a.name : string.Empty;
        string nameB = b != null ? b.name : string.Empty;

        int indexA = ExtractTrailingNumber(nameA);
        int indexB = ExtractTrailingNumber(nameB);
        bool hasIndexA = indexA >= 0;
        bool hasIndexB = indexB >= 0;

        if (hasIndexA && hasIndexB && indexA != indexB)
            return indexA.CompareTo(indexB);

        if (hasIndexA != hasIndexB)
            return hasIndexA ? -1 : 1;

        return string.CompareOrdinal(nameA, nameB);
    }

    static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        int endIndex = value.Length - 1;
        if (!char.IsDigit(value[endIndex]))
            return -1;

        int startIndex = endIndex;
        while (startIndex >= 0 && char.IsDigit(value[startIndex]))
            startIndex--;

        string numberPart = value.Substring(startIndex + 1, endIndex - startIndex);
        return int.TryParse(numberPart, out int number) ? number : -1;
    }
}

[System.Serializable]
public class EnemyTrailProfile
{
    public Vector2 RootOffsetFactors;
    public float RootRotationZ;
    public Vector2[] TrailOffsetFactors;
    public float MinTrailTime;
    public float MaxTrailTime;
    public float MinTrailWidth;
    public float MaxTrailWidth;
    public float EmissionThreshold;
    public EnemyTrailVisualStyle VisualStyle;
}

[System.Serializable]
public class EnemyMovementProfile
{
    public EnemyMovementModel Model;
    public EnemySpawnPattern SpawnPattern;
    public float MoveSpeed;
    public float TurnResponsiveness;
    public float DetectionRadius;
    public float DisengageRadius;
    public float OrbitDistance;
    public float PreferredDistance;
    public float ShootDistance;
    public float RepathInterval;
    public float TargetRefreshInterval;
    public float IdleDriftTurnSpeed;
    public float OrbitRadiusFactor;
    public float OrbitAngularSpeed;
}

[System.Serializable]
public class EnemyWeaponProfile
{
    public int AmmoCount;
    public float ReloadDuration;
    public float FireRate;
    public int Damage;
    public WeaponDamageType DamageType;
    public WeaponDeliveryMethod DeliveryMethod;
    public WeaponDeliveryFlags DeliveryFlags;
    public float BulletScaleMultiplier;
    public Color BulletColor;
    public float BulletSpeed;
    public float MuzzleOffsetDistance;
    public bool InfiniteAmmo;
    public bool RotateTowardAim;
    public float Range;
    public string ShotSoundId;
    public int MuzzleStreamCount;
}

[System.Serializable]
public class EnemyWreckProfile
{
    Sprite cachedSprite;

    public float Mass;
    public float LinearDamping;
    public float AngularDamping;
    public float DriftSpeed;
    public float AngularVelocityRange;
    public string RewardItemId;
    public bool DestroyWhenEmpty;
    public Color BaseColor;
    public string VisualResourcePath;
    public string EditorAssetPath;

    public Sprite GetVisualSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedSprite = Resources.Load<Sprite>(VisualResourcePath);
            if (cachedSprite != null)
                return cachedSprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(VisualResourcePath);
            cachedSprite = GetLargestSprite(sprites);
            if (cachedSprite != null)
                return cachedSprite;

            Texture2D texture = Resources.Load<Texture2D>(VisualResourcePath);
            if (texture != null)
            {
                float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                return cachedSprite;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (cachedSprite != null)
                return cachedSprite;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    cachedSprite = sprite;
                    return cachedSprite;
                }
            }
        }
#endif

        return null;
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }
}

[System.Serializable]
public class EnemyBotDefinition
{
    static Sprite riftWardenFallbackSprite;

    Sprite cachedSprite;

    public EnemyBotKind Kind;
    public string Id;
    public string DisplayName;
    public string InstantiationMarker;
    public string VisualResourcePath;
    public string AnimationResourcePath;
    public string EditorAssetPath;
    public float AnimationFramesPerSecond;
    public float TargetSize;
    public float PhysicsMass;
    public float LinearDamping;
    public float AngularDamping;
    public int DefaultHp;
    public int DefaultShield;
    public int MaxHp;
    public int MaxShield;
    public float DefaultSpeedMultiplier = 1f;
    public bool DefaultEnabled;
    public bool ShowInEnemySettings = true;
    public int DefaultCount;
    public int DefaultSpawnSecond;
    public EnemyMovementProfile Movement;
    public EnemyWeaponProfile Weapon;
    public EnemyWreckProfile Wreck;
    public EnemyTrailProfile Trails;
    public EnemyExplosionProfile Explosion;

    public string EnabledRoomKey => $"enemy.{Id}.enabled";
    public string CountRoomKey => $"enemy.{Id}.count";
    public string HpRoomKey => $"enemy.{Id}.hp";
    public string ShieldRoomKey => $"enemy.{Id}.shield";
    public string DamageRoomKey => $"enemy.{Id}.damage";
    public string SpeedRoomKey => $"enemy.{Id}.speed";
    public string SpawnSecondRoomKey => $"enemy.{Id}.spawnSecond";
    public string RespawnEnabledRoomKey => $"enemy.{Id}.respawnEnabled";
    public string RespawnIntervalRoomKey => $"enemy.{Id}.respawnInterval";
    public int DefaultDamage => Explosion != null ? Explosion.Damage : Weapon != null ? Weapon.Damage : 0;

    public Sprite GetVisualSprite()
    {
        if (cachedSprite != null)
        {
            if (Kind == EnemyBotKind.RiftWarden && cachedSprite.name.Contains("fallback"))
                cachedSprite = null;
            else
                return cachedSprite;
        }

        if (!string.IsNullOrWhiteSpace(VisualResourcePath))
        {
            cachedSprite = Resources.Load<Sprite>(VisualResourcePath);
            if (cachedSprite != null)
                return cachedSprite;

            Sprite[] sprites = Resources.LoadAll<Sprite>(VisualResourcePath);
            cachedSprite = GetLargestSprite(sprites);
            if (cachedSprite != null)
                return cachedSprite;

            Texture2D texture = Resources.Load<Texture2D>(VisualResourcePath);
            if (texture != null)
            {
                float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
                cachedSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit);
                return cachedSprite;
            }
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(EditorAssetPath))
        {
            cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorAssetPath);
            if (cachedSprite != null)
                return cachedSprite;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(EditorAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    cachedSprite = sprite;
                    return cachedSprite;
                }
            }
        }
#endif

        if (Kind == EnemyBotKind.RiftWarden)
        {
            cachedSprite = CreateRiftWardenFallbackSprite();
            return cachedSprite;
        }

        return null;
    }

    static Sprite CreateRiftWardenFallbackSprite()
    {
        if (riftWardenFallbackSprite != null)
            return riftWardenFallbackSprite;

        const int size = 192;
        const float center = (size - 1) * 0.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "rift_warden_runtime_fallback"
        };

        Color32[] pixels = new Color32[size * size];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 armor = new Color32(32, 48, 43, 238);
        Color32 armorEdge = new Color32(185, 214, 202, 230);
        Color32 glow = new Color32(22, 255, 220, 180);
        Color32 core = new Color32(82, 255, 228, 245);
        Color32 whiteCore = new Color32(225, 255, 250, 255);

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                int index = y * size + x;

                if (radius < 84f)
                {
                    float alpha = Mathf.Clamp01(1f - radius / 84f) * 0.42f;
                    pixels[index] = Blend(pixels[index], new Color32(24, 240, 216, (byte)(alpha * 255f)));
                }

                if (radius > 56f && radius < 64f)
                    pixels[index] = Blend(pixels[index], glow);

                if (radius > 36f && radius < 42f)
                    pixels[index] = Blend(pixels[index], armorEdge);

                float sector = Mathf.Abs(Mathf.Sin(angle * 4f));
                if (sector < 0.18f && radius > 34f && radius < 82f)
                    pixels[index] = Blend(pixels[index], armor);

                if ((Mathf.Abs(dx) < 2.2f || Mathf.Abs(dy) < 2.2f) && radius > 24f && radius < 76f)
                    pixels[index] = Blend(pixels[index], glow);

                if (radius < 31f)
                    pixels[index] = Blend(pixels[index], core);

                if (radius < 15f)
                    pixels[index] = Blend(pixels[index], whiteCore);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        riftWardenFallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        riftWardenFallbackSprite.name = "rift_warden_runtime_fallback_sprite";
        return riftWardenFallbackSprite;
    }

    static Color32 Blend(Color32 bottom, Color32 top)
    {
        float topAlpha = top.a / 255f;
        float bottomAlpha = bottom.a / 255f;
        float outAlpha = topAlpha + bottomAlpha * (1f - topAlpha);
        if (outAlpha <= 0.0001f)
            return new Color32(0, 0, 0, 0);

        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(((top.r * topAlpha) + (bottom.r * bottomAlpha * (1f - topAlpha))) / outAlpha), 0, 255);
        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(((top.g * topAlpha) + (bottom.g * bottomAlpha * (1f - topAlpha))) / outAlpha), 0, 255);
        byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(((top.b * topAlpha) + (bottom.b * bottomAlpha * (1f - topAlpha))) / outAlpha), 0, 255);
        return new Color32(r, g, b, (byte)Mathf.Clamp(Mathf.RoundToInt(outAlpha * 255f), 0, 255));
    }

    static Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }
}
