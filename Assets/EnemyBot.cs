using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
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
    RescueShip
}

public sealed class RescueShipBeamVfx : MonoBehaviour
{
    const int BeamPointCount = 18;
    const float EffectZOffset = -0.075f;
    const int SortingOrderOffset = 276;

    static readonly Dictionary<int, RescueShipBeamVfx> ActiveBySourceViewId = new Dictionary<int, RescueShipBeamVfx>();
    static Material sharedMaterial;
    static AudioClip rescueShipBeamClip;

    Transform source;
    Transform target;
    LineRenderer coreLine;
    LineRenderer glowLine;
    AudioSource audioSource;
    int sourceViewId;
    int sortingLayerId;
    int sortingOrder = 2400;

    public static void StartBeam(int sourcePhotonViewId, int targetPhotonViewId)
    {
        StopBeam(sourcePhotonViewId);

        PhotonView sourceView = PhotonView.Find(sourcePhotonViewId);
        PhotonView targetView = PhotonView.Find(targetPhotonViewId);
        if (sourceView == null || targetView == null)
            return;

        GameObject effect = new GameObject("RescueShipBeamVfx_" + sourcePhotonViewId);
        RescueShipBeamVfx vfx = effect.AddComponent<RescueShipBeamVfx>();
        vfx.Initialize(sourceView.transform, targetView.transform, sourcePhotonViewId);
        ActiveBySourceViewId[sourcePhotonViewId] = vfx;
    }

    public static void StopBeam(int sourcePhotonViewId)
    {
        if (!ActiveBySourceViewId.TryGetValue(sourcePhotonViewId, out RescueShipBeamVfx vfx))
            return;

        ActiveBySourceViewId.Remove(sourcePhotonViewId);
        if (vfx != null)
            Destroy(vfx.gameObject);
    }

    void Initialize(Transform sourceTransform, Transform targetTransform, int resolvedSourceViewId)
    {
        source = sourceTransform;
        target = targetTransform;
        sourceViewId = resolvedSourceViewId;

        SpriteRenderer sourceRenderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (sourceRenderer != null)
        {
            sortingLayerId = sourceRenderer.sortingLayerID;
            sortingOrder = sourceRenderer.sortingOrder + SortingOrderOffset;
        }

        if (source != null)
            gameObject.layer = source.gameObject.layer;

        glowLine = CreateLine("RescueBeamGlow", 0.34f, sortingOrder);
        coreLine = CreateLine("RescueBeamCore", 0.12f, sortingOrder + 1);
        CreateAudioSource();
    }

    void Update()
    {
        if (source == null || target == null)
        {
            StopBeam(sourceViewId);
            return;
        }

        UpdateBeam();
        UpdateAudio();
    }

    void OnDestroy()
    {
        if (ActiveBySourceViewId.TryGetValue(sourceViewId, out RescueShipBeamVfx active) && active == this)
            ActiveBySourceViewId.Remove(sourceViewId);

        if (audioSource != null)
            audioSource.Stop();
    }

    void UpdateBeam()
    {
        Vector3 start = GetSourcePoint();
        Vector3 end = GetTargetPoint(start);
        Vector3 delta = end - start;
        Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : source.up;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.forward);
        float distance = Mathf.Max(0.1f, delta.magnitude);
        float pulse = Mathf.Sin(Time.time * 6.5f) * 0.5f + 0.5f;
        float wave = Mathf.Lerp(0.02f, 0.08f, pulse) * Mathf.Clamp01(distance / 8f);

        UpdateLine(glowLine, start, end, perpendicular, wave, pulse, false);
        UpdateLine(coreLine, start, end, perpendicular, wave * 0.38f, pulse, true);
    }

    void UpdateLine(LineRenderer line, Vector3 start, Vector3 end, Vector3 perpendicular, float wave, float pulse, bool core)
    {
        if (line == null)
            return;

        line.enabled = true;
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            float taper = Mathf.Sin(t * Mathf.PI);
            float ripple = Mathf.Sin((t * Mathf.PI * 5f) + Time.time * 9f) * wave * taper;
            float shimmer = Mathf.Sin((t * Mathf.PI * 12f) - Time.time * 5.5f) * wave * 0.22f * taper;
            point += perpendicular * (ripple + shimmer);
            point.z = source.position.z + EffectZOffset;
            line.SetPosition(i, point);
        }

        float alpha = core ? Mathf.Lerp(0.78f, 1f, pulse) : Mathf.Lerp(0.36f, 0.62f, pulse);
        line.colorGradient = core ? BuildCoreGradient(alpha) : BuildGlowGradient(alpha);
        line.widthMultiplier = core
            ? Mathf.Lerp(0.09f, 0.16f, pulse)
            : Mathf.Lerp(0.24f, 0.38f, pulse);
    }

    Vector3 GetSourcePoint()
    {
        SpriteRenderer renderer = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (renderer == null)
            return source != null ? source.position : Vector3.zero;

        Bounds bounds = renderer.bounds;
        float side = Mathf.Sign((target != null ? target.position.x : source.position.x) - source.position.x);
        if (Mathf.Abs(side) < 0.1f)
            side = 1f;

        return new Vector3(
            source.position.x + bounds.extents.x * 0.58f * side,
            source.position.y - bounds.extents.y * 0.12f,
            source.position.z);
    }

    Vector3 GetTargetPoint(Vector3 sourcePoint)
    {
        Collider2D collider = target != null ? target.GetComponent<Collider2D>() : null;
        if (collider != null)
            return collider.ClosestPoint(sourcePoint);

        return target != null ? target.position : sourcePoint;
    }

    LineRenderer CreateLine(string objectName, float width, int order)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        if (source != null)
            lineObject.layer = source.gameObject.layer;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = BeamPointCount;
        line.widthMultiplier = width;
        line.numCapVertices = 14;
        line.numCornerVertices = 10;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.material = GetMaterial();
        line.sortingLayerID = sortingLayerId;
        line.sortingOrder = order;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.22f),
            new Keyframe(0.14f, 0.92f),
            new Keyframe(0.78f, 0.74f),
            new Keyframe(1f, 0.2f));
        return line;
    }

    void CreateAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = GetRescueShipBeamClip();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0.72f;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 4f;
        audioSource.maxDistance = 22f;

        if (audioSource.clip != null)
            audioSource.Play();
    }

    void UpdateAudio()
    {
        transform.position = source != null ? source.position : transform.position;
        if (audioSource != null && audioSource.clip != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    static Gradient BuildCoreGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.92f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.48f, 0.92f, 1f), 0.46f),
                new GradientColorKey(new Color(0.08f, 0.62f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha * 0.86f, 0.55f),
                new GradientAlphaKey(alpha * 0.34f, 1f)
            });
        return gradient;
    }

    static Gradient BuildGlowGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.56f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(0.2f, 0.62f, 1f), 0.5f),
                new GradientColorKey(new Color(0.02f, 0.2f, 0.48f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.78f, 0f),
                new GradientAlphaKey(alpha * 0.54f, 0.52f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    static AudioClip GetRescueShipBeamClip()
    {
        if (rescueShipBeamClip != null)
            return rescueShipBeamClip;

        rescueShipBeamClip = Resources.Load<AudioClip>("Audio/rescue_ship_beam");
        return rescueShipBeamClip;
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = "RescueShipBeamVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3350;
        return sharedMaterial;
    }
}

public static class EnemyTargetingUtility
{
    const float BeaconPriorityRangeMultiplier = 1.9f;

    public static Transform FindClosestTarget(Vector2 origin, PlayerHealth observerHealth, float maxDistance, bool requireNebulaVisibility)
    {
        Transform bestBeaconTarget = null;
        float bestBeaconDistance = float.MaxValue;

        float beaconRange = maxDistance * BeaconPriorityRangeMultiplier;
        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (!IsValidBeaconTarget(beacon, origin, beaconRange))
                continue;

            float distance = Vector2.Distance(origin, beacon.transform.position);
            if (distance >= bestBeaconDistance)
                continue;

            bestBeaconDistance = distance;
            bestBeaconTarget = beacon.transform;
        }

        if (bestBeaconTarget != null)
            return bestBeaconTarget;

        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        PlayerHealth[] players = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (!IsValidPlayerTarget(candidate, observerHealth, origin, maxDistance, requireNebulaVisibility))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }

    public static bool IsTargetValid(Transform target, PlayerHealth observerHealth, Vector2 origin, float maxDistance, bool requireNebulaVisibility)
    {
        if (target == null)
            return false;

        PlayerHealth player = target.GetComponent<PlayerHealth>();
        if (player != null)
        {
            if (IsAnyBeaconAvailable(origin, maxDistance * BeaconPriorityRangeMultiplier))
                return false;

            return IsValidPlayerTarget(player, observerHealth, origin, maxDistance, requireNebulaVisibility);
        }

        LureBeaconDecoy beacon = target.GetComponent<LureBeaconDecoy>();
        return IsValidBeaconTarget(beacon, origin, maxDistance * BeaconPriorityRangeMultiplier);
    }

    public static bool IsAnyTargetInRange(Vector2 origin, PlayerHealth observerHealth, float radius)
    {
        return FindClosestTarget(origin, observerHealth, radius, false) != null;
    }

    static bool IsValidPlayerTarget(PlayerHealth candidate, PlayerHealth observerHealth, Vector2 origin, float maxDistance, bool requireNebulaVisibility)
    {
        if (candidate == null || candidate == observerHealth || candidate.IsWreck || candidate.IsBotControlled || candidate.IsEvacuationAnimating)
            return false;

        if (candidate.GetComponent<LureBeaconDecoy>() != null)
            return false;

        if (Vector2.Distance(origin, candidate.transform.position) > maxDistance)
            return false;

        if (requireNebulaVisibility)
        {
            HideInNebulaTarget candidateNebulaState = candidate.GetComponent<HideInNebulaTarget>();
            HideInNebulaTarget observerNebulaState = observerHealth != null ? observerHealth.GetComponent<HideInNebulaTarget>() : null;
            if (candidateNebulaState != null && candidateNebulaState.IsHiddenFromObserver(observerNebulaState))
                return false;
        }

        return true;
    }

    static bool IsValidBeaconTarget(LureBeaconDecoy beacon, Vector2 origin, float maxDistance)
    {
        if (beacon == null || !beacon.CanBeTargeted)
            return false;

        return Vector2.Distance(origin, beacon.transform.position) <= maxDistance;
    }

    static bool IsAnyBeaconAvailable(Vector2 origin, float maxDistance)
    {
        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (IsValidBeaconTarget(beacon, origin, maxDistance))
                return true;
        }

        return false;
    }
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
    RescueShip
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
    BlueTwin
}

[System.Serializable]
public class EnemyExplosionProfile
{
    Sprite[] cachedFrames;

    public int Damage;
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
    public float BulletScaleMultiplier;
    public Color BulletColor;
    public float BulletSpeed;
    public float MuzzleOffsetDistance;
    public bool InfiniteAmmo;
    public bool RotateTowardAim;
    public float Range;
    public string ShotSoundId;
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
    Sprite cachedSprite;

    public EnemyBotKind Kind;
    public string Id;
    public string DisplayName;
    public string InstantiationMarker;
    public string VisualResourcePath;
    public string EditorAssetPath;
    public float TargetSize;
    public float PhysicsMass;
    public float LinearDamping;
    public float AngularDamping;
    public int DefaultHp;
    public int DefaultShield;
    public float DefaultSpeedMultiplier = 1f;
    public bool DefaultEnabled;
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

public static class EnemyBotCatalog
{
    static readonly EnemyBotDefinition[] Definitions =
    {
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Drone,
            Id = "drone",
            DisplayName = "Drone",
            InstantiationMarker = "enemy_bot",
            VisualResourcePath = "droid1_resource",
            EditorAssetPath = "Assets/droid1.png",
            TargetSize = 1.04f,
            PhysicsMass = 2.8f,
            LinearDamping = 0.08f,
            AngularDamping = 0.22f,
            DefaultHp = 50,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.GuardAndChase,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.1f,
                TurnResponsiveness = 300f,
                DetectionRadius = 10f,
                DisengageRadius = 20f,
                OrbitDistance = 5.5f,
                PreferredDistance = 7.5f,
                ShootDistance = 12f,
                RepathInterval = 0.35f,
                TargetRefreshInterval = 0.45f,
                IdleDriftTurnSpeed = 18f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 6f,
                FireRate = 0.15f,
                Damage = 10,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 10f,
                MuzzleOffsetDistance = 0.5f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 12f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 4.6f,
                LinearDamping = 0.56f,
                AngularDamping = 0.72f,
                DriftSpeed = 0.12f,
                AngularVelocityRange = 4f,
                RewardItemId = InventoryItemCatalog.DroidScrapId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.2f, 0.23f, 0.26f, 0.94f)
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, 0.44f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.02f) },
                MinTrailTime = 0.22f,
                MaxTrailTime = 0.82f,
                MinTrailWidth = 0.03f,
                MaxTrailWidth = 0.16f,
                EmissionThreshold = 0.04f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Corsair,
            Id = "corsair",
            DisplayName = "Corsair",
            InstantiationMarker = "enemy_bot_corsair",
            VisualResourcePath = "statek_duzy_resource",
            EditorAssetPath = "Assets/statek_duzy.png",
            TargetSize = 5.2f,
            PhysicsMass = 24f,
            LinearDamping = 0.16f,
            AngularDamping = 0.38f,
            DefaultHp = 200,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.OrbitMap,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.9f,
                TurnResponsiveness = 150f,
                DetectionRadius = 7f,
                OrbitRadiusFactor = 0.43f,
                OrbitAngularSpeed = 0.32f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 1f,
                Damage = 20,
                BulletScaleMultiplier = 2f,
                BulletColor = new Color(0.15f, 1f, 0.28f, 1f),
                BulletSpeed = 9f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 7f,
                ShotSoundId = "corsair"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 22f,
                LinearDamping = 0.84f,
                AngularDamping = 1.05f,
                DriftSpeed = 0.07f,
                AngularVelocityRange = 1.5f,
                RewardItemId = InventoryItemCatalog.CorsairSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.17f, 0.15f, 0.16f, 0.96f),
                VisualResourcePath = "wrak_corsair_resource",
                EditorAssetPath = "Assets/wrak_corsair.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.62f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.76f, 0.08f),
                    new Vector2(0f, 0.2f),
                    new Vector2(0.76f, 0.08f)
                },
                MinTrailTime = 0.65f,
                MaxTrailTime = 1.55f,
                MinTrailWidth = 0.12f,
                MaxTrailWidth = 0.34f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.RedLarge
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceMine,
            Id = "space_mine",
            DisplayName = "Space Mine",
            InstantiationMarker = "enemy_bot_space_mine",
            VisualResourcePath = "space_mine_resource",
            EditorAssetPath = "Assets/space mine.png",
            TargetSize = 1.08f,
            PhysicsMass = 3.8f,
            LinearDamping = 0.18f,
            AngularDamping = 0.42f,
            DefaultHp = 20,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = true,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.Drift,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 0.18f,
                TurnResponsiveness = 20f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 3.6f,
                LinearDamping = 0.78f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.09f,
                AngularVelocityRange = 2.4f,
                RewardItemId = InventoryItemCatalog.SpaceMineWreckId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.19f, 0.21f, 0.24f, 0.96f),
                VisualResourcePath = "wrak_miny_resource",
                EditorAssetPath = "Assets/wrak_miny.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = Vector2.zero,
                RootRotationZ = 0f,
                TrailOffsetFactors = System.Array.Empty<Vector2>(),
                MinTrailTime = 0f,
                MaxTrailTime = 0f,
                MinTrailWidth = 0f,
                MaxTrailWidth = 0f,
                EmissionThreshold = 1f,
                VisualStyle = EnemyTrailVisualStyle.None
            },
            Explosion = new EnemyExplosionProfile
            {
                Damage = 50,
                TriggerRadius = 2.08f,
                VisualTargetSize = 4.1f,
                VisualDuration = 1.25f,
                VisualStartFrame = 2,
                VisualColumns = 4,
                VisualRows = 6,
                VisualResourcePath = "",
                EditorAssetPath = "",
                SoundId = "space_mine_boom"
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.SpaceTruck,
            Id = "space_truck",
            DisplayName = "Space Truck",
            InstantiationMarker = "enemy_bot_space_truck",
            VisualResourcePath = "space_truck_resource",
            EditorAssetPath = "Assets/space_truck.png",
            TargetSize = 4.2f,
            PhysicsMass = 18f,
            LinearDamping = 0.1f,
            AngularDamping = 0.32f,
            DefaultHp = 100,
            DefaultShield = 50,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RouteExtractionZones,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 1.9f,
                TurnResponsiveness = 170f,
                TargetRefreshInterval = 0.45f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 18f,
                LinearDamping = 0.78f,
                AngularDamping = 0.96f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.8f,
                RewardItemId = InventoryItemCatalog.SpaceTruckWreckId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.18f, 0.22f, 0.2f, 0.98f),
                VisualResourcePath = "space_truck_wrak_resource",
                EditorAssetPath = "Assets/space_truck_wrak.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.48f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.36f, 0.02f),
                    new Vector2(0.36f, 0.02f)
                },
                MinTrailTime = 0.5f,
                MaxTrailTime = 1.25f,
                MinTrailWidth = 0.08f,
                MaxTrailWidth = 0.24f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.GreenTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.NeutralFighter,
            Id = "neutral_fighter",
            DisplayName = "Neutral Fighter",
            InstantiationMarker = "enemy_bot_neutral_fighter",
            VisualResourcePath = "neutral_fighter_resource",
            EditorAssetPath = "Assets/neutral_fighter.png",
            TargetSize = 0.94f,
            PhysicsMass = 5.4f,
            LinearDamping = 0.08f,
            AngularDamping = 0.2f,
            DefaultHp = 20,
            DefaultShield = 20,
            DefaultSpeedMultiplier = 1.5f,
            DefaultEnabled = false,
            DefaultCount = 2,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.NeutralFighter,
                SpawnPattern = EnemySpawnPattern.SafePerimeter,
                MoveSpeed = 1.1f,
                TurnResponsiveness = 320f,
                DetectionRadius = 6f,
                DisengageRadius = 8.5f,
                OrbitDistance = 3.1f,
                PreferredDistance = 4.4f,
                ShootDistance = 7.8f,
                RepathInterval = 0.24f,
                TargetRefreshInterval = 0.28f,
                IdleDriftTurnSpeed = 28f,
                OrbitAngularSpeed = 1.25f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 6,
                ReloadDuration = 4f,
                FireRate = 0.5f,
                Damage = 10,
                BulletScaleMultiplier = 0.58f,
                BulletColor = new Color(1f, 0.08f, 0.04f, 1f),
                BulletSpeed = 11.5f,
                MuzzleOffsetDistance = 0.62f,
                InfiniteAmmo = false,
                RotateTowardAim = true,
                Range = 8f,
                ShotSoundId = "shoot_small"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 3.8f,
                LinearDamping = 0.68f,
                AngularDamping = 0.85f,
                DriftSpeed = 0.1f,
                AngularVelocityRange = 3.2f,
                RewardItemId = InventoryItemCatalog.NeutralFighterSalvageId,
                DestroyWhenEmpty = true,
                BaseColor = new Color(0.42f, 0.44f, 0.46f, 0.98f),
                VisualResourcePath = "neutral_fighter_wreck_resource",
                EditorAssetPath = "Assets/neutral_fighter_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.44f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.04f) },
                MinTrailTime = 0.18f,
                MaxTrailTime = 0.7f,
                MinTrailWidth = 0.025f,
                MaxTrailWidth = 0.14f,
                EmissionThreshold = 0.03f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.RadarShip,
            Id = "radar_ship",
            DisplayName = "Radar Ship",
            InstantiationMarker = "enemy_bot_radar_ship",
            VisualResourcePath = "radar_ship_resource",
            EditorAssetPath = "Assets/radar_ship.png",
            TargetSize = 3.2f,
            PhysicsMass = 16f,
            LinearDamping = 0.11f,
            AngularDamping = 0.28f,
            DefaultHp = 90,
            DefaultShield = 110,
            DefaultSpeedMultiplier = 1.1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RadarShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.88f,
                TurnResponsiveness = 150f,
                DetectionRadius = 8.5f,
                DisengageRadius = 12f,
                OrbitDistance = 5.6f,
                PreferredDistance = 7.8f,
                ShootDistance = 8.5f,
                RepathInterval = 0.26f,
                TargetRefreshInterval = 0.24f,
                IdleDriftTurnSpeed = 15f,
                OrbitAngularSpeed = 0.42f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 9999,
                ReloadDuration = 0f,
                FireRate = 3f,
                Damage = 38,
                BulletScaleMultiplier = 1.8f,
                BulletColor = new Color(1f, 0.55f, 0.18f, 1f),
                BulletSpeed = 18f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 8.5f,
                ShotSoundId = "radar_ship"
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 18f,
                LinearDamping = 0.82f,
                AngularDamping = 1.02f,
                DriftSpeed = 0.08f,
                AngularVelocityRange = 1.3f,
                RewardItemId = InventoryItemCatalog.RadarShipSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.46f, 0.48f, 0.54f, 0.98f),
                VisualResourcePath = "radar_ship_wreck_resource",
                EditorAssetPath = "Assets/radar_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.46f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[] { new Vector2(0f, 0.02f) },
                MinTrailTime = 0.34f,
                MaxTrailTime = 1.12f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.2f,
                EmissionThreshold = 0.02f,
                VisualStyle = EnemyTrailVisualStyle.OrangeSmall
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.RescueShip,
            Id = "rescue_ship",
            DisplayName = "Rescue Ship",
            InstantiationMarker = "enemy_bot_rescue_ship",
            VisualResourcePath = "rescue_ship_resource",
            EditorAssetPath = "Assets/rescue_ship.png",
            TargetSize = 2.18f,
            PhysicsMass = 20f,
            LinearDamping = 0.1f,
            AngularDamping = 0.3f,
            DefaultHp = 85,
            DefaultShield = 95,
            DefaultSpeedMultiplier = 1.9f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.RescueShip,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.96f,
                TurnResponsiveness = 155f,
                DetectionRadius = 18f,
                DisengageRadius = 22f,
                OrbitDistance = 2.6f,
                PreferredDistance = 3.1f,
                ShootDistance = 0f,
                RepathInterval = 0.18f,
                TargetRefreshInterval = 0.2f,
                IdleDriftTurnSpeed = 14f,
                OrbitAngularSpeed = 0.25f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 0,
                ReloadDuration = 0f,
                FireRate = 0f,
                Damage = 0,
                BulletScaleMultiplier = 0f,
                BulletColor = new Color(0.54f, 0.9f, 1f, 1f),
                BulletSpeed = 0f,
                MuzzleOffsetDistance = 0f,
                InfiniteAmmo = true,
                RotateTowardAim = false,
                Range = 0f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 22f,
                LinearDamping = 0.84f,
                AngularDamping = 1.08f,
                DriftSpeed = 0.07f,
                AngularVelocityRange = 1.15f,
                RewardItemId = InventoryItemCatalog.RescueShipSalvageId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.48f, 0.54f, 0.6f, 0.98f),
                VisualResourcePath = "rescue_ship_wreck_resource",
                EditorAssetPath = "Assets/rescue_ship_wreck.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(0f, -0.44f),
                RootRotationZ = 180f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(-0.34f, 0.06f),
                    new Vector2(0.34f, 0.06f)
                },
                MinTrailTime = 0.42f,
                MaxTrailTime = 1.18f,
                MinTrailWidth = 0.05f,
                MaxTrailWidth = 0.18f,
                EmissionThreshold = 0.015f,
                VisualStyle = EnemyTrailVisualStyle.BlueTwin
            }
        },
        new EnemyBotDefinition
        {
            Kind = EnemyBotKind.Mothership,
            Id = "mothership",
            DisplayName = "Mothership",
            InstantiationMarker = "enemy_bot_mothership",
            VisualResourcePath = "mother_ship_resource",
            EditorAssetPath = "Assets/mother_ship.png",
            TargetSize = 7.28f,
            PhysicsMass = 95f,
            LinearDamping = 0.08f,
            AngularDamping = 0.42f,
            DefaultHp = 200,
            DefaultShield = 200,
            DefaultSpeedMultiplier = 1f,
            DefaultEnabled = false,
            DefaultCount = 1,
            DefaultSpawnSecond = 0,
            Movement = new EnemyMovementProfile
            {
                Model = EnemyMovementModel.Mothership,
                SpawnPattern = EnemySpawnPattern.WideCorners,
                MoveSpeed = 0.82f,
                TurnResponsiveness = 28f,
                DetectionRadius = 13.5f,
                DisengageRadius = 22f,
                PreferredDistance = 6.8f,
                RepathInterval = 0.45f,
                TargetRefreshInterval = 0.35f,
                OrbitRadiusFactor = 0.38f,
                OrbitAngularSpeed = 0.18f
            },
            Weapon = new EnemyWeaponProfile
            {
                AmmoCount = 10,
                ReloadDuration = 3f,
                FireRate = 0.28f,
                Damage = 10,
                BulletScaleMultiplier = 1f,
                BulletColor = Color.white,
                BulletSpeed = 10f,
                MuzzleOffsetDistance = 0.38f,
                InfiniteAmmo = false,
                RotateTowardAim = false,
                Range = 18f,
                ShotSoundId = string.Empty
            },
            Wreck = new EnemyWreckProfile
            {
                Mass = 120f,
                LinearDamping = 0.94f,
                AngularDamping = 1.4f,
                DriftSpeed = 0.045f,
                AngularVelocityRange = 0.7f,
                RewardItemId = InventoryItemCatalog.MothershipCoreId,
                DestroyWhenEmpty = false,
                BaseColor = new Color(0.52f, 0.55f, 0.58f, 0.98f),
                VisualResourcePath = "mother_ship_wrak_resource",
                EditorAssetPath = "Assets/mother_ship_wrak.png"
            },
            Trails = new EnemyTrailProfile
            {
                RootOffsetFactors = new Vector2(-0.6f, 0f),
                RootRotationZ = 0f,
                TrailOffsetFactors = new[]
                {
                    new Vector2(0f, -0.54f),
                    new Vector2(0f, -0.27f),
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0.27f),
                    new Vector2(0f, 0.54f)
                },
                MinTrailTime = 3.1f,
                MaxTrailTime = 6.2f,
                MinTrailWidth = 0.56f,
                MaxTrailWidth = 1.35f,
                EmissionThreshold = 0f,
                VisualStyle = EnemyTrailVisualStyle.RedLarge
            }
        }
    };

    static readonly System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> DefinitionsByKind = BuildDefinitionsByKind();
    static readonly System.Collections.Generic.Dictionary<string, EnemyBotDefinition> DefinitionsByMarker = BuildDefinitionsByMarker();

    public static System.Collections.Generic.IReadOnlyList<EnemyBotDefinition> AllDefinitions => Definitions;

    public static EnemyBotDefinition GetDefinition(EnemyBotKind kind)
    {
        DefinitionsByKind.TryGetValue(kind, out EnemyBotDefinition definition);
        return definition;
    }

    public static EnemyBotDefinition GetDefinition(string marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
            return null;

        DefinitionsByMarker.TryGetValue(marker, out EnemyBotDefinition definition);
        return definition;
    }

    static System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> BuildDefinitionsByKind()
    {
        System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<EnemyBotKind, EnemyBotDefinition>();
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].Kind] = Definitions[i];

        return result;
    }

    static System.Collections.Generic.Dictionary<string, EnemyBotDefinition> BuildDefinitionsByMarker()
    {
        System.Collections.Generic.Dictionary<string, EnemyBotDefinition> result = new System.Collections.Generic.Dictionary<string, EnemyBotDefinition>(System.StringComparer.Ordinal);
        for (int i = 0; i < Definitions.Length; i++)
            result[Definitions[i].InstantiationMarker] = Definitions[i];

        return result;
    }
}

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBot : MonoBehaviourPun
{
    const string PlayerPlacedMineMarker = "player_gadget_mine";
    const string SummonedDroneMarker = "space_truck_summoned_drone";

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyBotBehaviorBase behavior;
    SpriteRenderer cachedRenderer;
    EnemyBotKind kind;
    bool hasInitialized;
    bool hasAppliedStats;
    bool hasDetonated;
    bool spawnTeleportVfxPlayed;
    bool isPlayerPlacedMine;
    bool isSummonedDrone;
    bool spaceTruckFirstHitHandled;
    bool spaceTruckHalfHpHandled;
    float forcedSpeedMultiplier;
    int mineOwnerViewId;

    public EnemyBotKind Kind => kind;
    public EnemyBotDefinition Definition => EnemyBotCatalog.GetDefinition(kind);
    public bool IsCorsair => kind == EnemyBotKind.Corsair;
    public bool IsSpaceMine => kind == EnemyBotKind.SpaceMine;
    public bool IsSpaceTruck => kind == EnemyBotKind.SpaceTruck;
    public bool IsRadarShip => kind == EnemyBotKind.RadarShip;
    public bool IsRescueShip => kind == EnemyBotKind.RescueShip;
    public bool IsMothership => kind == EnemyBotKind.Mothership;
    public bool IsPlayerPlacedMine => isPlayerPlacedMine;
    public bool IsSummonedDrone => isSummonedDrone;
    public int MineOwnerViewId => mineOwnerViewId;
    public float VisualTargetSize => Definition != null ? Definition.TargetSize : 1.04f;
    public float EffectiveMoveSpeed => Definition != null && Definition.Movement != null
        ? Definition.Movement.MoveSpeed * EffectiveSpeedMultiplier
        : 1f;
    public float EffectiveSpeedMultiplier => forcedSpeedMultiplier > 0f
        ? forcedSpeedMultiplier
        : RoomSettings.GetEnemySpeedMultiplier(kind);

    public static bool IsBotObject(GameObject target)
    {
        return target != null && target.GetComponent<EnemyBot>() != null;
    }

    public static bool IsBotView(PhotonView targetView)
    {
        return targetView != null && targetView.GetComponent<EnemyBot>() != null;
    }

    public static bool IsBotInstantiationData(object[] data)
    {
        return GetDefinitionFromInstantiationData(data) != null;
    }

    public static EnemyBotKind GetKindFromInstantiationData(object[] data)
    {
        EnemyBotDefinition definition = GetDefinitionFromInstantiationData(data);
        return definition != null ? definition.Kind : EnemyBotKind.Drone;
    }

    static EnemyBotDefinition GetDefinitionFromInstantiationData(object[] data)
    {
        if (data == null ||
            data.Length == 0 ||
            !(data[0] is string marker))
        {
            return null;
        }

        return EnemyBotCatalog.GetDefinition(marker);
    }

    public void InitializeFromPhotonData()
    {
        if (hasInitialized)
            return;

        view = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        kind = GetKindFromInstantiationData(view != null ? view.InstantiationData : null);
        ResolveSpecialMineOwner(view != null ? view.InstantiationData : null);
        ResolveSummonedDrone(view != null ? view.InstantiationData : null);

        DisablePlayerOnlySystems();
        EnsureBehavior();
        ApplyBotVisuals();
        PlaySpawnTeleportVfx();
        ConfigurePhysics();
        ConfigureColliderToVisual();
        ApplyMineOwnerCollisionIgnore();
        if (GetComponent<EnemyBotHealthBarUI>() == null)
        {
            gameObject.AddComponent<EnemyBotHealthBarUI>();
        }
        if (!hasAppliedStats)
        {
            ApplyBotStats();
            hasAppliedStats = true;
        }

        hasInitialized = true;
    }

    void PlaySpawnTeleportVfx()
    {
        if (spawnTeleportVfxPlayed || !IsGameStarted() || isPlayerPlacedMine || kind == EnemyBotKind.RescueShip)
            return;

        spawnTeleportVfxPlayed = true;
        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        float radius = VisualTargetSize * 0.62f;
        if (cachedRenderer != null)
            radius = Mathf.Max(radius, Mathf.Max(cachedRenderer.bounds.size.x, cachedRenderer.bounds.size.y) * 0.58f);

        EnemySpawnTeleportVfx.Spawn(transform.position, cachedRenderer, radius);
    }

    void Awake()
    {
        InitializeFromPhotonData();
    }

    void Start()
    {
        InitializeFromPhotonData();
    }

    void Update()
    {
        EnsureStableVisuals();
        if (isPlayerPlacedMine)
            ApplyMineOwnerCollisionIgnore();

        if (!view.IsMine || !IsGameStarted() || health == null || health.IsWreck)
            return;

        if (behavior == null)
            EnsureBehavior();
    }

    void FixedUpdate()
    {
        if (!view.IsMine || !IsGameStarted() || health == null || health.IsWreck)
            return;

        if (!hasInitialized)
            InitializeFromPhotonData();

        if (behavior == null)
            EnsureBehavior();

        behavior?.TickBehavior();
    }

    void ConfigurePhysics()
    {
        if (rb == null || Definition == null)
            return;

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.mass = Definition.PhysicsMass;
        rb.linearDamping = Definition.LinearDamping;
        rb.angularDamping = Definition.AngularDamping;
    }

    void ConfigureColliderToVisual()
    {
        if (kind != EnemyBotKind.Mothership)
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        Bounds bounds = cachedRenderer.bounds;
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            SetWorldBoxSize(boxCollider, new Vector2(bounds.size.x * 0.82f, bounds.size.y * 0.72f));
        }

        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
            circleCollider.enabled = false;
    }

    void SetWorldBoxSize(BoxCollider2D collider2D, Vector2 worldSize)
    {
        Vector3 scale = collider2D.transform.lossyScale;
        float safeX = Mathf.Abs(scale.x) > 0.0001f ? Mathf.Abs(scale.x) : 1f;
        float safeY = Mathf.Abs(scale.y) > 0.0001f ? Mathf.Abs(scale.y) : 1f;
        collider2D.size = new Vector2(worldSize.x / safeX, worldSize.y / safeY);
        collider2D.offset = Vector2.zero;
    }

    void ApplyBotStats()
    {
        if (health == null || Definition == null)
            return;

        health.ConfigureBaseStats(RoomSettings.GetEnemyHp(kind), RoomSettings.GetEnemyShield(kind));
    }

    void EnsureBehavior()
    {
        behavior = GetComponent<EnemyBotBehaviorBase>();
        if (behavior == null || !IsMatchingBehavior(behavior))
        {
            if (behavior != null)
                Destroy(behavior);

            if (Definition != null && Definition.Movement != null)
            {
                switch (Definition.Movement.Model)
                {
                    case EnemyMovementModel.OrbitMap:
                        behavior = gameObject.AddComponent<EnemyCorsairBehavior>();
                        break;
                    case EnemyMovementModel.Drift:
                        behavior = gameObject.AddComponent<EnemyMineBehavior>();
                        break;
                    case EnemyMovementModel.RouteExtractionZones:
                        behavior = gameObject.AddComponent<EnemySpaceTruckBehavior>();
                        break;
                    case EnemyMovementModel.RadarShip:
                        behavior = gameObject.AddComponent<EnemyRadarShipBehavior>();
                        break;
                    case EnemyMovementModel.RescueShip:
                        behavior = gameObject.AddComponent<EnemyRescueShipBehavior>();
                        break;
                    case EnemyMovementModel.Mothership:
                        behavior = gameObject.AddComponent<EnemyMothershipBehavior>();
                        break;
                    case EnemyMovementModel.NeutralFighter:
                        behavior = gameObject.AddComponent<EnemyNeutralFighterBehavior>();
                        break;
                    default:
                        behavior = gameObject.AddComponent<EnemyDroneBehavior>();
                        break;
                }
            }
            else
            {
                behavior = gameObject.AddComponent<EnemyDroneBehavior>();
            }
        }

        behavior.Initialize(this);
    }

    bool IsMatchingBehavior(EnemyBotBehaviorBase existingBehavior)
    {
        if (existingBehavior == null || Definition == null || Definition.Movement == null)
            return false;

        return Definition.Movement.Model switch
        {
            EnemyMovementModel.OrbitMap => existingBehavior is EnemyCorsairBehavior,
            EnemyMovementModel.Drift => existingBehavior is EnemyMineBehavior,
            EnemyMovementModel.RouteExtractionZones => existingBehavior is EnemySpaceTruckBehavior,
            EnemyMovementModel.RadarShip => existingBehavior is EnemyRadarShipBehavior,
            EnemyMovementModel.RescueShip => existingBehavior is EnemyRescueShipBehavior,
            EnemyMovementModel.Mothership => existingBehavior is EnemyMothershipBehavior,
            EnemyMovementModel.NeutralFighter => existingBehavior is EnemyNeutralFighterBehavior,
            _ => existingBehavior is EnemyDroneBehavior
        };
    }

    void DisablePlayerOnlySystems()
    {
        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        ShipInventoryHudUI cargoHud = GetComponent<ShipInventoryHudUI>();
        if (cargoHud != null)
            cargoHud.enabled = false;

        AmmoUI ammoUi = GetComponent<AmmoUI>();
        if (ammoUi != null)
            ammoUi.enabled = false;

        BoosterBarUI boosterUi = GetComponent<BoosterBarUI>();
        if (boosterUi != null)
            boosterUi.enabled = false;

        ReloadButtonUI reloadUi = GetComponent<ReloadButtonUI>();
        if (reloadUi != null)
            reloadUi.enabled = false;
    }

    void ApplyBotVisuals()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        cachedRenderer = renderer;
        Sprite sprite = GetVisualSprite();
        if (sprite == null)
            return;

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = GameVisualTheme.EnemySortingOrder;
        FitRendererToTargetSize(renderer, VisualTargetSize);
    }

    void EnsureStableVisuals()
    {
        if (health != null && health.IsWreck)
            return;

        if (cachedRenderer == null)
            cachedRenderer = GetComponent<SpriteRenderer>();

        if (cachedRenderer == null)
            return;

        Sprite desiredSprite = GetVisualSprite();
        if (desiredSprite != null && cachedRenderer.sprite != desiredSprite)
            cachedRenderer.sprite = desiredSprite;

        cachedRenderer.color = Color.white;
        cachedRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        cachedRenderer.sortingOrder = GameVisualTheme.EnemySortingOrder;
        FitRendererToTargetSize(cachedRenderer, VisualTargetSize);
    }

    public Sprite GetVisualSprite()
    {
        return Definition != null ? Definition.GetVisualSprite() : null;
    }

    public bool HasCustomDeathExplosion()
    {
        return Definition != null && Definition.Explosion != null;
    }

    public void RequestDetonation()
    {
        if (hasDetonated || !CanRequestDetonation() || Definition == null || Definition.Explosion == null)
            return;

        EnemyExplosionProfile explosion = Definition.Explosion;
        hasDetonated = true;
        Vector3 detonationPosition = GetVisualCenterWorldPosition();
        SpaceObjectMotionSync.BroadcastSpaceMineDetonation(detonationPosition, explosion.TriggerRadius);
        DetonateNearbyTargets(explosion);
        if (PhotonNetwork.CurrentRoom != null && photonView != null)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    bool CanRequestDetonation()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return true;

        if (PhotonNetwork.IsMasterClient)
            return true;

        return isPlayerPlacedMine && view != null && view.IsMine;
    }

    void DetonateNearbyTargets(EnemyExplosionProfile explosion)
    {
        if (explosion == null)
            return;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            PhotonView targetView = candidate.GetComponent<PhotonView>();
            if (targetView != null)
                targetView.RPC(nameof(PlayerHealth.TakeDamage), RpcTarget.MasterClient, RoomSettings.GetEnemyDamage(kind), photonView.ViewID);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            float distance = Vector2.Distance(transform.position, beacon.transform.position);
            if (distance > explosion.TriggerRadius)
                continue;

            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageAt), RpcTarget.MasterClient, RoomSettings.GetEnemyDamage(kind), photonView.ViewID, beacon.transform.position.x, beacon.transform.position.y);
        }
    }

    [PunRPC]
    void PlayMineDetonationEffects(float x, float y, float z)
    {
        SpawnSpaceMineDetonationEffects(new Vector3(x, y, z));
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) &&
            value is bool started)
        {
            return started;
        }

        return false;
    }

    void FitRendererToTargetSize(SpriteRenderer renderer, float targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Bounds spriteBounds = renderer.sprite.bounds;
        float largestDimension = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        if (largestDimension <= 0.0001f)
            return;

        float scale = targetSize / largestDimension;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    Vector3 GetVisualCenterWorldPosition()
    {
        if (cachedRenderer != null)
            return cachedRenderer.bounds.center;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            return renderer.bounds.center;

        return transform.position;
    }

    void ResolveSpecialMineOwner(object[] instantiationData)
    {
        isPlayerPlacedMine = false;
        mineOwnerViewId = 0;

        if (kind != EnemyBotKind.SpaceMine || instantiationData == null || instantiationData.Length < 3)
            return;

        if (!(instantiationData[1] is string marker) ||
            !string.Equals(marker, PlayerPlacedMineMarker, System.StringComparison.Ordinal))
        {
            return;
        }

        if (instantiationData[2] is int ownerViewId && ownerViewId > 0)
        {
            isPlayerPlacedMine = true;
            mineOwnerViewId = ownerViewId;
        }
    }

    void ResolveSummonedDrone(object[] instantiationData)
    {
        isSummonedDrone = false;

        if (kind != EnemyBotKind.Drone || instantiationData == null || instantiationData.Length < 2)
            return;

        isSummonedDrone = instantiationData[1] is string marker &&
                          string.Equals(marker, SummonedDroneMarker, System.StringComparison.Ordinal);
    }

    void ApplyMineOwnerCollisionIgnore()
    {
        if (!isPlayerPlacedMine || mineOwnerViewId <= 0)
            return;

        PhotonView ownerView = PhotonView.Find(mineOwnerViewId);
        if (ownerView == null)
            return;

        Collider2D[] mineColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] ownerColliders = ownerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < mineColliders.Length; i++)
        {
            Collider2D mineCollider = mineColliders[i];
            if (mineCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider2D ownerCollider = ownerColliders[j];
                if (ownerCollider == null)
                    continue;

                Physics2D.IgnoreCollision(mineCollider, ownerCollider, true);
            }
        }
    }

    public bool ShouldIgnoreMineTriggerFor(PlayerHealth candidate)
    {
        if (!isPlayerPlacedMine || candidate == null || mineOwnerViewId <= 0)
            return false;

        PhotonView candidateView = candidate.GetComponent<PhotonView>();
        return candidateView != null && candidateView.ViewID == mineOwnerViewId;
    }

    public void ConvertMothershipTurretsToWreckVisuals()
    {
        if (kind != EnemyBotKind.Mothership)
            return;

        EnemyMothershipBehavior mothershipBehavior = behavior as EnemyMothershipBehavior;
        if (mothershipBehavior == null)
            mothershipBehavior = GetComponent<EnemyMothershipBehavior>();

        mothershipBehavior?.ConvertTurretsToWreckVisuals();
    }

    public void NotifyDamageTaken(int previousHp, int currentHp, int shieldDamage, int hpDamage, int attackerViewID)
    {
        if (!PhotonNetwork.IsMasterClient || health == null || health.IsWreck)
            return;

        bool wasHit = shieldDamage > 0 || hpDamage > 0;
        if (!wasHit)
            return;

        if (kind != EnemyBotKind.SpaceMine && kind != EnemyBotKind.RescueShip)
            EnemyBotManager.NotifyRescueShipSummonTrigger(this);

        if (kind == EnemyBotKind.Mothership && behavior is EnemyMothershipBehavior mothershipBehavior)
            mothershipBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.NeutralFighter && behavior is EnemyNeutralFighterBehavior neutralFighterBehavior)
            neutralFighterBehavior.NotifyDamageSource(attackerViewID);

        if (kind == EnemyBotKind.RadarShip && behavior is EnemyRadarShipBehavior radarShipBehavior)
            radarShipBehavior.NotifyDamageSource(attackerViewID);

        if (kind != EnemyBotKind.SpaceTruck)
            return;

        if (!spaceTruckFirstHitHandled)
        {
            spaceTruckFirstHitHandled = true;
            forcedSpeedMultiplier = 2f;
            TriggerSpaceTruckAlarmAndDrone();
        }

        int halfHp = Mathf.CeilToInt(health.maxHP * 0.5f);
        if (!spaceTruckHalfHpHandled && previousHp > halfHp && currentHp <= halfHp)
        {
            spaceTruckHalfHpHandled = true;
            TriggerSpaceTruckAlarmAndDrone();
        }
    }

    void TriggerSpaceTruckAlarmAndDrone()
    {
        Vector3 position = GetVisualCenterWorldPosition();
        photonView.RPC(nameof(PlaySpaceTruckAlert), RpcTarget.All, position.x, position.y, position.z);
        SpawnSummonedDroneNear(position);
    }

    void SpawnSummonedDroneNear(Vector3 sourcePosition)
    {
        EnemyBotDefinition droneDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.Drone);
        if (droneDefinition == null)
            return;

        Vector2 offset = new Vector2(
            Mathf.Sin(Time.time * 3.7f + photonView.ViewID) > 0f ? 1.8f : -1.8f,
            1.25f);
        Vector3 spawnPosition = sourcePosition + (Vector3)offset;
        GameObject droneObject = PhotonNetwork.Instantiate("Player", spawnPosition, Quaternion.identity, 0, new object[] { droneDefinition.InstantiationMarker, SummonedDroneMarker });
        if (droneObject == null)
            return;

        EnemyBot drone = droneObject.GetComponent<EnemyBot>();
        if (drone == null)
            drone = droneObject.AddComponent<EnemyBot>();

        drone.InitializeFromPhotonData();
    }

    [PunRPC]
    void PlaySpaceTruckAlert(float x, float y, float z)
    {
        AudioManager.Instance.PlaySpaceTruckAlertAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnRadarStrikeMarkerRpc(float x, float y, float warningDuration, float radius)
    {
        RadarStrikeVfx.SpawnMarker(new Vector2(x, y), warningDuration, radius);
    }

    [PunRPC]
    public void PlayRadarShipShootRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRadarShipShootAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void PlayRadarShipIncomingRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRadarShipIncomingAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void SpawnRadarStrikeImpactRpc(float x, float y, float radius)
    {
        RadarStrikeVfx.SpawnImpact(new Vector2(x, y), radius);
    }

    [PunRPC]
    public void PlayRescueShipIncomingRpc(float x, float y, float z)
    {
        AudioManager.Instance.PlayRescueShipIncomingAt(new Vector3(x, y, z));
    }

    [PunRPC]
    public void StartRescueShipBeamRpc(int targetViewId)
    {
        RescueShipBeamVfx.StartBeam(photonView != null ? photonView.ViewID : 0, targetViewId);
    }

    [PunRPC]
    public void StopRescueShipBeamRpc()
    {
        RescueShipBeamVfx.StopBeam(photonView != null ? photonView.ViewID : 0);
    }

    public static Vector3 ResolveSpaceMineDetonationPosition(int sourceViewId, Vector3 fallbackWorldPosition)
    {
        if (sourceViewId > 0)
        {
            PhotonView sourceView = PhotonView.Find(sourceViewId);
            if (sourceView != null)
            {
                EnemyBot bot = sourceView.GetComponent<EnemyBot>();
                if (bot != null)
                    return bot.GetVisualCenterWorldPosition();

                return sourceView.transform.position;
            }
        }

        return fallbackWorldPosition;
    }

    public static void SpawnSpaceMineDetonationEffects(Vector3 worldPosition)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        EnemyExplosionProfile explosion = definition != null ? definition.Explosion : null;
        SpawnSpaceMineDetonationEffects(worldPosition, explosion != null ? explosion.TriggerRadius : 0f);
    }

    public static void SpawnSpaceMineDetonationEffects(Vector3 worldPosition, float radius)
    {
        EnemyBotDefinition definition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        EnemyExplosionProfile explosion = definition != null ? definition.Explosion : null;
        if (explosion == null)
            return;

        float effectRadius = radius > 0.1f ? radius : explosion.TriggerRadius;
        SpaceMineExplosionVfx.Spawn(worldPosition, effectRadius);

        if (explosion.SoundId == "space_mine_boom")
            AudioManager.Instance.PlaySpaceMineBoomAt(worldPosition);
        else
            AudioManager.Instance.PlayExplosionAt(worldPosition);
    }
}

public abstract class EnemyBotBehaviorBase : MonoBehaviour
{
    protected EnemyBot bot;

    public virtual void Initialize(EnemyBot owner)
    {
        bot = owner;
    }

    public abstract void TickBehavior();
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyDroneBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    float nextTargetRefreshTime;
    float nextRepathTime;
    Vector2 currentMoveDirection = Vector2.up;
    Transform currentTarget;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                Mathf.Max(weapon.AmmoCount, 1),
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + movement.TargetRefreshInterval;
            currentTarget = ResolveTarget();
        }

        if (currentTarget == null)
        {
            ApplyIdleDrift();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + movement.RepathInterval;
            currentMoveDirection = CalculateMoveDirection(currentTarget.position);
        }

        Vector2 desiredVelocity = currentMoveDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.14f);

        Vector2 aimDirection = (Vector2)currentTarget.position - rb.position;
        if (weapon != null && weapon.RotateTowardAim && aimDirection.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg + 90f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootAtTarget(aimDirection);
    }

    void ApplyIdleDrift()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.05f)
        {
            Vector2 fallback = currentMoveDirection.sqrMagnitude > 0.001f ? currentMoveDirection : Vector2.up;
            rb.linearVelocity = fallback.normalized * (bot.EffectiveMoveSpeed * 0.36f);
        }

        float spin = Mathf.Sin(Time.time * 0.45f + view.ViewID * 0.23f) * movement.IdleDriftTurnSpeed;
        rb.MoveRotation(rb.rotation + spin * Time.fixedDeltaTime);
    }

    Transform ResolveTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, movement.DisengageRadius, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    Vector2 CalculateMoveDirection(Vector2 targetPosition)
    {
        Vector2 toTarget = targetPosition - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return currentMoveDirection;

        Vector2 towardTarget = toTarget / distance;
        Vector2 orbitDirection = new Vector2(-towardTarget.y, towardTarget.x);
        if (Mathf.Sin(Time.time * 0.6f + view.ViewID * 0.27f) < 0f)
            orbitDirection *= -1f;

        Vector2 result;
        if (distance > movement.PreferredDistance)
            result = towardTarget * 0.84f + orbitDirection * 0.28f;
        else if (distance < movement.OrbitDistance)
            result = -towardTarget * 0.72f + orbitDirection * 0.52f;
        else
            result = orbitDirection * 0.85f + towardTarget * 0.18f;

        return result.normalized;
    }

    void TryShootAtTarget(Vector2 aimDirection)
    {
        if (shooting == null || weapon == null || aimDirection.sqrMagnitude <= 0.001f)
            return;

        if (aimDirection.magnitude > weapon.Range)
            return;

        if (weapon.RotateTowardAim)
        {
            Vector2 normalizedAim = aimDirection.normalized;
            float facingDot = Vector2.Dot(-transform.up, normalizedAim);
            if (facingDot < 0.9f)
                return;
        }

        shooting.TryFireBot(aimDirection.normalized);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyCorsairBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(6f, Mathf.Min(mapSize.x, mapSize.y) * movement.OrbitRadiusFactor);
        int orbitSeed = view != null ? view.ViewID : 0;
        orbitAngle = Mathf.Abs(orbitSeed * 0.137f) % (Mathf.PI * 2f);
        orbitDirection = (orbitSeed % 2 == 0) ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                9999,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radialDirection = fromCenter.normalized;
        Vector2 tangentDirection = orbitDirection > 0f
            ? new Vector2(-radialDirection.y, radialDirection.x)
            : new Vector2(radialDirection.y, -radialDirection.x);

        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 desiredVelocity = tangentDirection * bot.EffectiveMoveSpeed + radialDirection * (radialError * 1.35f);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.16f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg + 270f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        TryShootNearestTarget();
    }

    void TryShootNearestTarget()
    {
        if (shooting == null || weapon == null)
            return;

        Transform bestTarget = EnemyTargetingUtility.FindClosestTarget(transform.position, health, weapon.Range, true);

        if (bestTarget == null)
            return;

        Vector2 shootDirection = bestTarget.position - transform.position;
        if (shootDirection.sqrMagnitude <= 0.01f)
            return;

        shooting.TryFireBot(shootDirection.normalized);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyNeutralFighterBehavior : EnemyBotBehaviorBase
{
    enum FighterMode
    {
        Patrol,
        Combat,
        Flee
    }

    const float FleeDuration = 5f;
    const float AvoidanceScanRadius = 1.75f;
    const float AvoidanceWeight = 0.62f;
    const float MapEdgeSoftTurnMargin = 5.4f;
    const float MapEdgeHardTurnMargin = 1.15f;
    const float MapEdgeLookAheadSeconds = 1.2f;
    const float MapEdgeMinimumLookAhead = 1.1f;
    const float MapEdgeMaximumLookAhead = 3.4f;
    const float MapEdgeTurnTangentWeight = 0.62f;
    const float FireIntervalJitter = 0.1f;
    const float StuckVelocityThreshold = 0.16f;
    const float StuckDuration = 0.42f;
    const float AvoidanceSuppressionDuration = 0.85f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    FighterMode mode = FighterMode.Patrol;
    Transform currentTarget;
    Vector2 patrolDirection = Vector2.up;
    Vector2 fleeDirection = Vector2.right;
    float nextTargetRefreshTime;
    float nextRepathTime;
    float nextPatrolTurnTime;
    float fleeUntil;
    float orbitDirection = 1f;
    float lowSpeedSince;
    float avoidanceSuppressedUntil;
    float edgeAvoidanceStrength;
    Vector2 edgeInwardNormal;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.211f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        orbitDirection = seed % 2 == 0 ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                weapon.AmmoCount,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range);
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        UpdateMode();

        edgeAvoidanceStrength = 0f;
        edgeInwardNormal = Vector2.zero;

        Vector2 desiredDirection = ResolveDesiredDirection();
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection.normalized;

        float speed = ResolveCurrentSpeed();
        Vector2 desiredVelocity = desiredDirection.normalized * speed;
        float velocityBlend = mode == FighterMode.Combat ? 0.2f : 0.13f;
        if (edgeAvoidanceStrength > 0.001f)
            velocityBlend = Mathf.Max(velocityBlend, Mathf.Lerp(0.22f, 0.54f, edgeAvoidanceStrength));

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, velocityBlend);

        Vector2 aimDirection = ResolveAimDirection(desiredDirection);
        if (edgeAvoidanceStrength > 0.58f &&
            edgeInwardNormal.sqrMagnitude > 0.001f &&
            aimDirection.sqrMagnitude > 0.001f &&
            Vector2.Dot(aimDirection.normalized, edgeInwardNormal.normalized) < -0.12f)
        {
            aimDirection = desiredDirection;
        }

        RotateNoseToward(aimDirection);

        if (mode == FighterMode.Combat)
            TryShootAtTarget();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
            currentTarget = attackerView.transform;

        Vector2 threatPosition = attackerView != null ? (Vector2)attackerView.transform.position : rb != null ? rb.position - Vector2.right : (Vector2)transform.position - Vector2.right;
        Vector2 away = rb != null ? rb.position - threatPosition : (Vector2)transform.position - threatPosition;
        if (away.sqrMagnitude < 0.001f)
            away = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : patrolDirection;

        fleeDirection = away.normalized;
        fleeUntil = Time.time + FleeDuration;
        mode = FighterMode.Flee;
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.12f, movement.TargetRefreshInterval);
        currentTarget = ResolveTarget();
    }

    void UpdateMode()
    {
        if (mode == FighterMode.Flee)
        {
            if (Time.time < fleeUntil)
                return;

            if (currentTarget == null || !IsTargetWithin(currentTarget, movement.DetectionRadius))
                mode = FighterMode.Patrol;
            else
                mode = FighterMode.Combat;
            return;
        }

        if (currentTarget != null && IsTargetWithin(currentTarget, movement.DetectionRadius))
        {
            mode = FighterMode.Combat;
            return;
        }

        if (mode == FighterMode.Combat && (currentTarget == null || !IsTargetWithin(currentTarget, movement.DisengageRadius)))
            mode = FighterMode.Patrol;
    }

    Vector2 ResolveDesiredDirection()
    {
        switch (mode)
        {
            case FighterMode.Combat:
                return ResolveCombatDirection();
            case FighterMode.Flee:
                return fleeDirection.sqrMagnitude > 0.001f ? fleeDirection.normalized : -transform.up;
            default:
                return ResolvePatrolDirection();
        }
    }

    Vector2 ResolvePatrolDirection()
    {
        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(1.2f, 2.4f);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.55f, 0.55f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        Vector2 boundarySteer = ResolveMapBoundarySteering(patrolDirection, out float boundaryStrength, out _);
        if (boundaryStrength > 0.001f)
        {
            float steerBlend = Mathf.Clamp01(0.28f + boundaryStrength * 0.62f);
            patrolDirection = (patrolDirection.normalized * (1f - steerBlend) + boundarySteer * steerBlend).normalized;
        }

        return patrolDirection.normalized;
    }

    Vector2 ResolveCombatDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);

        float slowOrbitWave = Mathf.Sin(Time.time * 1.8f + view.ViewID * 0.17f) * 0.18f;
        if (distance > movement.PreferredDistance + 0.6f)
            return (toward * 0.86f + tangent * (0.26f + slowOrbitWave)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.72f + tangent * 0.68f).normalized;

        return (tangent * 0.88f + toward * 0.16f).normalized;
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        if (Time.time < avoidanceSuppressedUntil)
            return desiredDirection;

        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f
            ? desiredDirection.normalized
            : rb.linearVelocity.sqrMagnitude > 0.001f
                ? rb.linearVelocity.normalized
                : patrolDirection.normalized;
        Vector2 avoidance = Vector2.zero;
        int closeAvoidedObjects = 0;
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, AvoidanceScanRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 toObstacle = closest - rb.position;
            if (toObstacle.sqrMagnitude > 0.0001f && Vector2.Dot(toObstacle.normalized, desired) < -0.2f)
                continue;

            Vector2 away = rb.position - closest;
            float distance = Mathf.Max(0.12f, away.magnitude);
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            if (away.sqrMagnitude > 0.0001f)
            {
                closeAvoidedObjects++;
                avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
            }
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        UpdateStuckSuppression(closeAvoidedObjects);
        if (Time.time < avoidanceSuppressedUntil)
            return desired;

        Vector2 blended = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        if (Vector2.Dot(blended, desired) < 0.2f)
            blended = (desired * 0.82f + avoidance.normalized * 0.18f).normalized;

        return blended;
    }

    void UpdateStuckSuppression(int closeAvoidedObjects)
    {
        if (closeAvoidedObjects < 2 || rb.linearVelocity.magnitude > StuckVelocityThreshold)
        {
            lowSpeedSince = 0f;
            return;
        }

        if (lowSpeedSince <= 0f)
        {
            lowSpeedSince = Time.time;
            return;
        }

        if (Time.time - lowSpeedSince >= StuckDuration)
        {
            avoidanceSuppressedUntil = Time.time + AvoidanceSuppressionDuration;
            lowSpeedSince = 0f;
        }
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        Vector2 edgeSteer = ResolveMapBoundarySteering(desired, out float strength, out Vector2 inwardNormal);
        if (strength <= 0.001f)
            return desiredDirection;

        if (strength > edgeAvoidanceStrength)
        {
            edgeAvoidanceStrength = strength;
            edgeInwardNormal = inwardNormal;
        }

        float inwardDot = inwardNormal.sqrMagnitude > 0.001f ? Vector2.Dot(desired, inwardNormal.normalized) : 0f;
        float blend = Mathf.Clamp01(0.28f + strength * 0.72f);
        if (inwardDot < -0.05f)
            blend = Mathf.Clamp01(blend + 0.22f);

        Vector2 result = (desired * (1f - blend) + edgeSteer * blend).normalized;
        if (strength > 0.72f && inwardNormal.sqrMagnitude > 0.001f && Vector2.Dot(result, inwardNormal.normalized) < 0.18f)
            result = (result * 0.42f + inwardNormal.normalized * 0.92f).normalized;

        return result;
    }

    Vector2 NormalizeMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
            return direction.normalized;

        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.001f)
            return rb.linearVelocity.normalized;

        return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
    }

    Vector2 ResolveMapBoundarySteering(Vector2 desiredDirection, out float strength, out Vector2 inwardNormal)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float bodyRadius = Mathf.Clamp(bot != null ? bot.VisualTargetSize * 0.58f : 0.55f, 0.35f, 1.2f);
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - bodyRadius);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - bodyRadius);
        Vector2 desired = NormalizeMoveDirection(desiredDirection);
        float lookAheadDistance = Mathf.Clamp(
            ResolveCurrentSpeed() * MapEdgeLookAheadSeconds,
            MapEdgeMinimumLookAhead,
            MapEdgeMaximumLookAhead);
        Vector2 predictedPosition = rb.position + desired * lookAheadDistance;
        Vector2 push = Vector2.zero;
        float maxStrength = 0f;

        float rightDistance = halfX - predictedPosition.x;
        if (rightDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(rightDistance);
            push.x -= axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float leftDistance = predictedPosition.x + halfX;
        if (leftDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(leftDistance);
            push.x += axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float topDistance = halfY - predictedPosition.y;
        if (topDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(topDistance);
            push.y -= axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        float bottomDistance = predictedPosition.y + halfY;
        if (bottomDistance < MapEdgeSoftTurnMargin)
        {
            float axisStrength = CalculateMapEdgeStrength(bottomDistance);
            push.y += axisStrength;
            maxStrength = Mathf.Max(maxStrength, axisStrength);
        }

        if (push.sqrMagnitude <= 0.001f)
        {
            strength = 0f;
            inwardNormal = Vector2.zero;
            return Vector2.zero;
        }

        inwardNormal = push.normalized;
        strength = Mathf.Clamp01(maxStrength);

        bool affectsX = Mathf.Abs(push.x) > 0.001f;
        bool affectsY = Mathf.Abs(push.y) > 0.001f;
        Vector2 tangent = new Vector2(-inwardNormal.y, inwardNormal.x);
        if (Vector2.Dot(tangent, desired) < 0f)
            tangent = -tangent;

        float outwardAmount = Mathf.Clamp01(-Vector2.Dot(desired, inwardNormal));
        float tangentWeight = affectsX != affectsY
            ? Mathf.Lerp(MapEdgeTurnTangentWeight, 0.2f, outwardAmount)
            : Mathf.Lerp(0.28f, 0.12f, outwardAmount);

        Vector2 steering = inwardNormal + tangent * tangentWeight;
        return steering.sqrMagnitude > 0.001f ? steering.normalized : inwardNormal;
    }

    float CalculateMapEdgeStrength(float distanceToEdge)
    {
        float softStrength = Mathf.Clamp01((MapEdgeSoftTurnMargin - distanceToEdge) / MapEdgeSoftTurnMargin);
        if (distanceToEdge < MapEdgeHardTurnMargin)
        {
            float hardStrength = Mathf.InverseLerp(MapEdgeHardTurnMargin, -MapEdgeHardTurnMargin, distanceToEdge);
            softStrength = Mathf.Max(softStrength, 0.72f + hardStrength * 0.28f);
        }

        return Mathf.Clamp01(softStrength);
    }

    float ResolveCurrentSpeed()
    {
        float baseSpeed = bot != null ? bot.EffectiveMoveSpeed : 1f;
        return mode == FighterMode.Patrol ? baseSpeed * 0.5f : baseSpeed;
    }

    Vector2 ResolveAimDirection(Vector2 moveDirection)
    {
        if (mode == FighterMode.Combat && currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        return moveDirection.sqrMagnitude > 0.001f ? moveDirection.normalized : transform.up;
    }

    void RotateNoseToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryShootAtTarget()
    {
        if (shooting == null || weapon == null || currentTarget == null)
            return;

        Vector2 aim = (Vector2)currentTarget.position - rb.position;
        float distance = aim.magnitude;
        if (distance <= 0.001f || distance > weapon.Range)
            return;

        Vector2 normalizedAim = aim / distance;
        if (Vector2.Dot(transform.up, normalizedAim) < 0.92f)
            return;

        Vector3 muzzle = transform.position + transform.up * Mathf.Max(0.1f, weapon.MuzzleOffsetDistance);
        float cooldownJitter = Random.Range(-FireIntervalJitter, FireIntervalJitter);
        shooting.TryFireBotFromWorld(normalizedAim, muzzle, cooldownJitter);
    }

    Transform ResolveTarget()
    {
        float allowedRange = mode == FighterMode.Combat ? movement.DisengageRadius : movement.DetectionRadius;
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, allowedRange, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    bool IsTargetWithin(Transform target, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(target, health, transform.position, maxDistance, true);
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyMothershipBehavior : EnemyBotBehaviorBase
{
    sealed class TurretRuntime
    {
        public Transform Root;
        public int Ammo;
        public float NextFireTime;
        public float ReloadFinishTime;
        public bool Reloading;
    }

    const float TurretFullTurnDuration = 4f;
    const float DamageSourceChaseDuration = 5f;
    const float ShieldRegenPerSecond = 2f;
    const float TurretPivotOffsetFromCenterFactor = 1f / 6f;
    const float TurretTargetSizeFactor = 0.285f;
    const float TurretMinimumTargetSize = 1.02f;

    static Sprite cachedTurretSprite;
    static Sprite cachedTurretWreckSprite;

    readonly TurretRuntime[] turrets = new TurretRuntime[6];
    readonly Vector2[] turretOffsetFactors =
    {
        new Vector2(0.28f, -0.01f),
        new Vector2(0.1f, 0.14f),
        new Vector2(0.11f, -0.13f),
        new Vector2(-0.16f, 0.23f),
        new Vector2(-0.25f, -0.01f),
        new Vector2(-0.16f, -0.23f)
    };

    Rigidbody2D rb;
    PhotonView view;
    PlayerShooting shooting;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    SpriteRenderer mothershipRenderer;
    Vector2 orbitCenter;
    float orbitRadius;
    float orbitAngle;
    float orbitDirection = 1f;
    float nextTargetRefreshTime;
    float forcedDirectionUntil;
    float shieldRegenAccumulator;
    Transform currentTarget;
    Vector2 forcedMoveDirection;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        shooting = owner.GetComponent<PlayerShooting>();
        health = owner.GetComponent<PlayerHealth>();
        mothershipRenderer = owner.GetComponent<SpriteRenderer>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        Vector2 mapSize = RoomSettings.GetMapDimensions();
        orbitCenter = Vector2.zero;
        orbitRadius = Mathf.Max(7f, Mathf.Min(mapSize.x, mapSize.y) * (movement != null ? movement.OrbitRadiusFactor : 0.38f));
        int seed = view != null ? view.ViewID : 1;
        orbitAngle = Mathf.Abs(seed * 0.119f) % (Mathf.PI * 2f);
        orbitDirection = seed % 2 == 0 ? 1f : -1f;

        if (shooting != null && weapon != null)
        {
            shooting.ConfigureWeaponProfile(
                weapon.FireRate,
                weapon.AmmoCount,
                weapon.ReloadDuration,
                RoomSettings.GetEnemyDamage(owner.Kind),
                weapon.BulletScaleMultiplier,
                weapon.BulletColor,
                weapon.MuzzleOffsetDistance,
                weapon.InfiniteAmmo,
                weapon.BulletSpeed,
                weapon.ShotSoundId,
                weapon.Range);
        }

        EnsureTurrets();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        EnsureTurrets();
        RegenerateShield();

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + Mathf.Max(0.15f, movement.TargetRefreshInterval);
            currentTarget = ResolveTarget();
        }

        Vector2 desiredDirection = ResolveMoveDirection();
        Vector2 desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.1f);
        RotateHullToward(desiredVelocity);
        TickTurrets();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        Vector2 sourcePosition = rb != null ? rb.position - Vector2.right : (Vector2)transform.position - Vector2.right;
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
            sourcePosition = attackerView.transform.position;

        Vector2 fromShipToSource = sourcePosition - (Vector2)transform.position;
        if (fromShipToSource.sqrMagnitude < 0.001f)
            fromShipToSource = rb != null && rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity : Vector2.right;

        forcedMoveDirection = fromShipToSource.normalized;
        forcedDirectionUntil = Time.time + DamageSourceChaseDuration;
    }

    public void ConvertTurretsToWreckVisuals()
    {
        Sprite wreckSprite = LoadTurretWreckSprite();
        if (wreckSprite == null)
            return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child.name != "TurretVisual")
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.sprite = wreckSprite;
        }
    }

    Vector2 ResolveMoveDirection()
    {
        if (Time.time < forcedDirectionUntil && forcedMoveDirection.sqrMagnitude > 0.001f)
            return forcedMoveDirection.normalized;

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
            if (toTarget.sqrMagnitude > 0.001f)
                return toTarget.normalized;
        }

        orbitAngle += orbitDirection * movement.OrbitAngularSpeed * Time.fixedDeltaTime;
        Vector2 fromCenter = rb.position - orbitCenter;
        if (fromCenter.sqrMagnitude < 0.01f)
            fromCenter = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle));

        Vector2 radial = fromCenter.normalized;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-radial.y, radial.x)
            : new Vector2(radial.y, -radial.x);
        float radialError = orbitRadius - fromCenter.magnitude;
        Vector2 orbitVelocity = tangent + radial * Mathf.Clamp(radialError * 0.12f, -0.5f, 0.5f);
        return orbitVelocity.sqrMagnitude > 0.001f ? orbitVelocity.normalized : Vector2.right;
    }

    void RotateHullToward(Vector2 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    Transform ResolveTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, transform.position, movement.DisengageRadius, true))
            return currentTarget;

        return FindClosestVisibleHumanTarget(movement.DetectionRadius);
    }

    Transform FindClosestVisibleHumanTarget(float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(transform.position, health, maxDistance, true);
    }

    bool IsValidVisibleTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, transform.position, maxDistance, true);
    }

    void RegenerateShield()
    {
        if (health == null || health.HasBrokenShield || health.CurrentShield >= health.MaxShield)
            return;

        shieldRegenAccumulator += ShieldRegenPerSecond * Time.fixedDeltaTime;
        if (shieldRegenAccumulator < 1f)
            return;

        float amount = Mathf.Floor(shieldRegenAccumulator);
        shieldRegenAccumulator -= amount;
        health.TryRestoreShieldAuthority(amount, true);
    }

    void EnsureTurrets()
    {
        Sprite turretSprite = LoadTurretSprite();
        Vector2 shipLocalSize = GetMothershipLocalSize();

        for (int i = 0; i < turrets.Length; i++)
        {
            if (turrets[i] == null)
                turrets[i] = new TurretRuntime { Ammo = weapon != null ? Mathf.Max(1, weapon.AmmoCount) : 10 };

            if (turrets[i].Root == null)
            {
                GameObject turretObject = new GameObject("MothershipTurret_" + i);
                turretObject.transform.SetParent(transform, false);
                turrets[i].Root = turretObject.transform;
            }

            EnsureTurretVisual(turrets[i].Root, turretSprite);
            turrets[i].Root.localScale = Vector3.one;
            turrets[i].Root.localPosition = new Vector3(
                turretOffsetFactors[i].x * shipLocalSize.x,
                turretOffsetFactors[i].y * shipLocalSize.y,
                0f);
            FitTurretVisual(turrets[i].Root);
        }
    }

    Vector2 GetMothershipLocalSize()
    {
        if (mothershipRenderer != null && mothershipRenderer.sprite != null)
            return mothershipRenderer.sprite.bounds.size;

        return new Vector2(7f, 3f);
    }

    void EnsureTurretVisual(Transform turretRoot, Sprite turretSprite)
    {
        if (turretRoot == null)
            return;

        SpriteRenderer rootRenderer = turretRoot.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = false;

        Transform visual = turretRoot.Find("TurretVisual");
        if (visual == null)
        {
            GameObject visualObject = new GameObject("TurretVisual");
            visualObject.transform.SetParent(turretRoot, false);
            visual = visualObject.transform;
        }

        SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
            visualRenderer = visual.gameObject.AddComponent<SpriteRenderer>();

        visualRenderer.sprite = turretSprite;
        visualRenderer.color = Color.white;
        if (mothershipRenderer != null)
        {
            visualRenderer.sortingLayerID = mothershipRenderer.sortingLayerID;
            visualRenderer.sortingOrder = mothershipRenderer.sortingOrder + 1;
        }
    }

    void FitTurretVisual(Transform turretRoot)
    {
        Transform visual = turretRoot != null ? turretRoot.Find("TurretVisual") : null;
        SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null)
            return;

        float largest = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
        if (largest <= 0.001f)
            return;

        float targetSize = Mathf.Max(TurretMinimumTargetSize, GetMothershipLocalSize().x * TurretTargetSizeFactor);
        float scale = targetSize / largest;
        visual.localScale = new Vector3(scale, scale, 1f);
        float pivotOffset = renderer.sprite.bounds.size.x * scale * TurretPivotOffsetFromCenterFactor;
        visual.localPosition = new Vector3(-pivotOffset, 0f, 0f);
        visual.localRotation = Quaternion.identity;
    }

    void TickTurrets()
    {
        if (weapon == null || shooting == null)
            return;

        float turnSpeed = 360f / TurretFullTurnDuration;
        for (int i = 0; i < turrets.Length; i++)
        {
            TurretRuntime turret = turrets[i];
            if (turret == null || turret.Root == null)
                continue;

            UpdateTurretReload(turret);
            Transform target = FindClosestVisibleHumanTargetFrom(turret.Root.position, weapon.Range);
            if (target == null)
                continue;

            Vector2 toTarget = target.position - turret.Root.position;
            if (toTarget.sqrMagnitude <= 0.001f)
                continue;

            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 180f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
            turret.Root.rotation = Quaternion.RotateTowards(turret.Root.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);

            if (Time.time < turret.NextFireTime || turret.Reloading || turret.Ammo <= 0)
                continue;

            Vector2 muzzleDirection = -turret.Root.right;
            Vector3 muzzlePosition = turret.Root.position + (Vector3)(muzzleDirection * GetTurretMuzzleDistance(turret.Root));
            if (shooting.FireBotProjectileFromWorld(muzzleDirection, muzzlePosition))
            {
                turret.Ammo--;
                turret.NextFireTime = Time.time + Mathf.Max(0.05f, weapon.FireRate);
                if (turret.Ammo <= 0)
                {
                    turret.Reloading = true;
                    turret.ReloadFinishTime = Time.time + Mathf.Max(0f, weapon.ReloadDuration);
                }
            }
        }
    }

    float GetTurretMuzzleDistance(Transform turretRoot)
    {
        Transform visual = turretRoot != null ? turretRoot.Find("TurretVisual") : null;
        SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (renderer == null || renderer.sprite == null)
            return Mathf.Max(0.18f, weapon != null ? weapon.MuzzleOffsetDistance : 0.18f);

        float visualWidth = renderer.sprite.bounds.size.x * Mathf.Abs(visual.lossyScale.x);
        return (visualWidth * (0.5f + TurretPivotOffsetFromCenterFactor)) + Mathf.Max(0.08f, weapon != null ? weapon.MuzzleOffsetDistance : 0.18f);
    }

    void UpdateTurretReload(TurretRuntime turret)
    {
        if (!turret.Reloading || weapon == null || Time.time < turret.ReloadFinishTime)
            return;

        turret.Reloading = false;
        turret.Ammo = Mathf.Max(1, weapon.AmmoCount);
    }

    Transform FindClosestVisibleHumanTargetFrom(Vector3 origin, float maxDistance)
    {
        return EnemyTargetingUtility.FindClosestTarget(origin, health, maxDistance, true);
    }

    static Sprite LoadTurretSprite()
    {
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

        cachedTurretSprite = Resources.Load<Sprite>("wieza_mother_ship_resource");
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

#if UNITY_EDITOR
        cachedTurretSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/wieza_mother_ship.png");
        if (cachedTurretSprite != null)
            return cachedTurretSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/wieza_mother_ship.png");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                cachedTurretSprite = sprite;
                return cachedTurretSprite;
            }
        }
#endif

        return null;
    }

    static Sprite LoadTurretWreckSprite()
    {
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

        cachedTurretWreckSprite = Resources.Load<Sprite>("wieza_mother_ship_wrak_resource");
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

#if UNITY_EDITOR
        cachedTurretWreckSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/wieza_mother_ship_wrak.png");
        if (cachedTurretWreckSprite != null)
            return cachedTurretWreckSprite;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/wieza_mother_ship_wrak.png");
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                cachedTurretWreckSprite = sprite;
                return cachedTurretWreckSprite;
            }
        }
#endif

        return null;
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyMineBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyExplosionProfile explosion;
    Vector2 driftDirection;
    float nextRetargetTime;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        explosion = owner.Definition != null ? owner.Definition.Explosion : null;

        if (driftDirection.sqrMagnitude <= 0.001f)
        {
            int seed = view != null ? view.ViewID : Random.Range(1, 9999);
            float angle = Mathf.Abs(seed * 0.173f) % (Mathf.PI * 2f);
            driftDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (driftDirection.sqrMagnitude <= 0.001f)
                driftDirection = Vector2.up;
        }
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null || explosion == null)
            return;

        if (health != null && health.IsWreck)
            return;

        Vector2 desiredVelocity = driftDirection.normalized * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.08f);
        rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, 8f, 0.04f);

        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + 0.12f;
            if (IsAnyTargetInRange(explosion.TriggerRadius))
                bot.RequestDetonation();
        }
    }

    bool IsAnyTargetInRange(float radius)
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (bot != null && bot.ShouldIgnoreMineTriggerFor(candidate))
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            if (Vector2.Distance(transform.position, candidate.transform.position) <= radius)
                return true;
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted)
                continue;

            if (Vector2.Distance(transform.position, beacon.transform.position) <= radius)
                return true;
        }

        return false;
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemySpaceTruckBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    ExtractionZone[] extractionZones = System.Array.Empty<ExtractionZone>();
    int targetZoneIndex;
    float nextZoneRefreshTime;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        RefreshExtractionZones(true);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (Time.time >= nextZoneRefreshTime || extractionZones == null || extractionZones.Length == 0)
            RefreshExtractionZones(false);

        Vector2 desiredDirection = ResolveDesiredDirection();
        Vector2 desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.13f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg + 270f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }
    }

    void RefreshExtractionZones(bool chooseNearest)
    {
        nextZoneRefreshTime = Time.time + Mathf.Max(0.3f, movement != null ? movement.TargetRefreshInterval : 0.45f);
        extractionZones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        if (extractionZones == null || extractionZones.Length == 0)
            return;

        if (chooseNearest)
        {
            targetZoneIndex = FindNearestZoneIndex();
            AdvanceTargetZone();
        }
        else
        {
            targetZoneIndex = Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1);
        }
    }

    Vector2 ResolveDesiredDirection()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : Vector2.up;

        ExtractionZone targetZone = extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)];
        if (targetZone == null)
        {
            AdvanceTargetZone();
            return Vector2.up;
        }

        Vector2 toTarget = (Vector2)targetZone.transform.position - rb.position;
        if (toTarget.magnitude <= 1.4f)
        {
            AdvanceTargetZone();
            targetZone = extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)];
            if (targetZone != null)
                toTarget = (Vector2)targetZone.transform.position - rb.position;
        }

        return toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.up;
    }

    int FindNearestZoneIndex()
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < extractionZones.Length; i++)
        {
            ExtractionZone zone = extractionZones[i];
            if (zone == null)
                continue;

            float distance = Vector2.Distance(rb.position, zone.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void AdvanceTargetZone()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return;

        targetZoneIndex = (targetZoneIndex + 1) % extractionZones.Length;
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyRescueShipBehavior : EnemyBotBehaviorBase
{
    const float HealRange = 5.9f;
    const float BeamBreakRange = 7.7f;
    const float HealPerSecond = 10f;
    const float HealStandOffDistance = 4.3f;
    const float HealAnchorTolerance = 0.92f;
    const float MinimumHealLockDuration = 1f;
    const float PatrolTurnIntervalMin = 1.35f;
    const float PatrolTurnIntervalMax = 2.6f;
    const float PatrolSpeedMultiplier = 0.78f;
    const float EntrySpeedMultiplier = 2.45f;
    const float MapEdgeMargin = 2.6f;
    const float MapEdgeSteerWeight = 0.82f;
    const float RecoveryEdgeThreshold = 2.1f;
    const float AvoidanceScanRadius = 2.1f;
    const float AvoidanceWeight = 0.4f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    Collider2D bodyCollider;
    readonly System.Collections.Generic.List<Collider2D> wallColliders = new System.Collections.Generic.List<Collider2D>(4);
    PlayerHealth currentHealTarget;
    Vector2 patrolDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextPatrolTurnTime;
    float healAccumulator;
    float healLockEndTime;
    int activeBeamTargetViewId = -1;
    bool wallCollisionsIgnoredWhileEntering;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        bodyCollider = owner.GetComponent<Collider2D>();
        RefreshWallColliders();

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.193f) % (Mathf.PI * 2f);
        patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
    }

    void OnDisable()
    {
        SetWallCollisionIgnored(false);
        StopBeam();
    }

    void OnDestroy()
    {
        SetWallCollisionIgnored(false);
        StopBeam();
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
        {
            StopBeam();
            return;
        }

        RefreshTargetIfNeeded();

        bool enteringMap = !IsInsidePlayableBounds(rb.position);
        SetWallCollisionIgnored(enteringMap);
        bool hadActiveHealingState = currentHealTarget != null || activeBeamTargetViewId > 0 || healLockEndTime > Time.time;
        bool canHealCurrentTarget = IsHealableTarget(currentHealTarget, bot);
        Vector2 desiredDirection;
        bool withinHealRange = false;

        if (canHealCurrentTarget)
        {
            desiredDirection = ResolveHealDirection(currentHealTarget, out withinHealRange);
        }
        else
        {
            currentHealTarget = null;
            if (hadActiveHealingState)
                EnterPatrolMode(enteringMap);
            desiredDirection = enteringMap ? (-rb.position).normalized : ResolvePatrolDirection();
        }

        desiredDirection = ApplyAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        if (!canHealCurrentTarget && IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
            desiredDirection = Vector2.Lerp(desiredDirection.normalized, (-rb.position).normalized, 0.72f).normalized;
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;

        bool sustainHealLock = canHealCurrentTarget
            && activeBeamTargetViewId > 0
            && Time.time < healLockEndTime
            && Vector2.Distance(rb.position, GetTargetPoint(currentHealTarget)) <= BeamBreakRange;
        bool healingThisFrame = canHealCurrentTarget && (withinHealRange || sustainHealLock);

        float speedMultiplier = enteringMap ? EntrySpeedMultiplier : canHealCurrentTarget ? 1f : PatrolSpeedMultiplier;
        if (healingThisFrame)
            speedMultiplier = 0.16f;

        Vector2 desiredVelocity = desiredDirection.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, healingThisFrame ? 0.24f : 0.14f);

        Vector2 aimDirection = canHealCurrentTarget
            ? (GetTargetPoint(currentHealTarget) - rb.position)
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : desiredDirection;
        RotateHullToward(aimDirection);

        if (healingThisFrame)
        {
            PhotonView targetView = currentHealTarget != null ? currentHealTarget.GetComponent<PhotonView>() : null;
            int targetViewId = targetView != null ? targetView.ViewID : -1;
            bool startedNewBeam = targetViewId > 0 && activeBeamTargetViewId != targetViewId;
            StartBeam(currentHealTarget);
            if (startedNewBeam)
                healLockEndTime = Time.time + MinimumHealLockDuration;
            ApplyHealing(currentHealTarget);
        }
        else
        {
            healAccumulator = 0f;
            healLockEndTime = 0f;
            StopBeam();
        }
    }

    public static bool TryFindNearestDamagedAlly(Vector2 origin, EnemyBot selfBot, out PlayerHealth result)
    {
        result = null;
        float bestDistance = float.MaxValue;
        PlayerHealth[] candidates = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerHealth candidate = candidates[i];
            if (!IsHealableTarget(candidate, selfBot))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            result = candidate;
        }

        return result != null;
    }

    static bool IsHealableTarget(PlayerHealth candidate, EnemyBot selfBot)
    {
        if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || !candidate.IsBotControlled)
            return false;

        EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
        if (candidateBot == null || candidateBot == selfBot)
            return false;

        if (candidateBot.Kind == EnemyBotKind.SpaceMine || candidateBot.Kind == EnemyBotKind.RescueShip)
            return false;

        return candidate.CurrentHP < candidate.maxHP || candidate.CurrentShield < candidate.MaxShield;
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < healLockEndTime && IsHealableTarget(currentHealTarget, bot))
            return;

        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.14f, movement.TargetRefreshInterval);
        TryFindNearestDamagedAlly(rb.position, bot, out currentHealTarget);
    }

    Vector2 ResolveHealDirection(PlayerHealth target, out bool withinRange)
    {
        Vector2 targetPoint = GetTargetPoint(target);
        Vector2 anchorPoint = GetHealAnchorPoint(targetPoint);
        Vector2 toAnchor = anchorPoint - rb.position;
        float anchorDistance = toAnchor.magnitude;
        float targetDistance = Vector2.Distance(rb.position, targetPoint);
        withinRange = targetDistance <= HealRange && anchorDistance <= HealAnchorTolerance;
        if (anchorDistance <= 0.001f)
            return patrolDirection.sqrMagnitude > 0.001f ? patrolDirection : Vector2.up;

        if (targetDistance <= BeamBreakRange)
            return toAnchor / anchorDistance;

        return toAnchor / anchorDistance;
    }

    Vector2 ResolvePatrolDirection()
    {
        if (IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
            return (-rb.position).normalized;

        if (Time.time >= nextPatrolTurnTime)
        {
            nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
            float angle = Mathf.Atan2(patrolDirection.y, patrolDirection.x) + Random.Range(-0.5f, 0.5f);
            patrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return patrolDirection;
    }

    void EnterPatrolMode(bool enteringMap)
    {
        healAccumulator = 0f;
        healLockEndTime = 0f;
        StopBeam();

        Vector2 fallbackDirection;
        if (enteringMap || IsNearMapEdge(rb.position, RecoveryEdgeThreshold))
        {
            fallbackDirection = (-rb.position).sqrMagnitude > 0.001f ? (-rb.position).normalized : Vector2.up;
        }
        else if (rb.linearVelocity.sqrMagnitude > 0.001f)
        {
            fallbackDirection = rb.linearVelocity.normalized;
        }
        else
        {
            fallbackDirection = patrolDirection.sqrMagnitude > 0.001f ? patrolDirection.normalized : Vector2.up;
        }

        patrolDirection = fallbackDirection;
        nextPatrolTurnTime = Time.time + Random.Range(PatrolTurnIntervalMin, PatrolTurnIntervalMax);
        rb.linearVelocity = patrolDirection * (bot.EffectiveMoveSpeed * Mathf.Max(PatrolSpeedMultiplier, 0.92f));
    }

    Vector2 ApplyAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, AvoidanceScanRadius);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.1f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.1f
            ? (desired * 0.76f + avoidance.normalized * 0.24f).normalized
            : result;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        if (hit.GetComponentInParent<DroppedCargoCrate>() != null)
            return true;

        EnemyBot otherBot = hit.GetComponentInParent<EnemyBot>();
        return otherBot != null && otherBot != bot;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.2f, bot.EffectiveMoveSpeed * 1.8f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.8f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.8f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.8f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.8f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * (1f - MapEdgeSteerWeight) + inward.normalized * MapEdgeSteerWeight).normalized;
    }

    bool IsInsidePlayableBounds(Vector2 position)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        return Mathf.Abs(position.x) <= halfX && Mathf.Abs(position.y) <= halfY;
    }

    bool IsNearMapEdge(Vector2 position, float threshold)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        return halfX - Mathf.Abs(position.x) <= threshold || halfY - Mathf.Abs(position.y) <= threshold;
    }

    Vector2 GetHealAnchorPoint(Vector2 targetPoint)
    {
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = mapSize.x * 0.5f;
        float halfY = mapSize.y * 0.5f;
        float leftClearance = targetPoint.x + halfX;
        float rightClearance = halfX - targetPoint.x;
        float bottomClearance = targetPoint.y + halfY;
        float topClearance = halfY - targetPoint.y;

        Vector2 inward = (-targetPoint).sqrMagnitude > 0.001f ? (-targetPoint).normalized : Vector2.up;
        float smallestClearance = Mathf.Min(Mathf.Min(leftClearance, rightClearance), Mathf.Min(bottomClearance, topClearance));
        if (smallestClearance == leftClearance)
            inward = Vector2.right;
        else if (smallestClearance == rightClearance)
            inward = Vector2.left;
        else if (smallestClearance == bottomClearance)
            inward = Vector2.up;
        else if (smallestClearance == topClearance)
            inward = Vector2.down;

        return targetPoint + inward.normalized * HealStandOffDistance;
    }

    void RefreshWallColliders()
    {
        wallColliders.Clear();
        string[] wallNames = { "WallTop", "WallBottom", "WallLeft", "WallRight" };
        for (int i = 0; i < wallNames.Length; i++)
        {
            GameObject wall = GameObject.Find(wallNames[i]);
            if (wall == null)
                continue;

            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            if (wallCollider != null)
                wallColliders.Add(wallCollider);
        }
    }

    void SetWallCollisionIgnored(bool ignored)
    {
        if (bodyCollider == null)
            return;

        if (wallColliders.Count == 0 || wallColliders.TrueForAll(collider => collider == null))
            RefreshWallColliders();

        if (wallCollisionsIgnoredWhileEntering == ignored)
            return;

        wallCollisionsIgnoredWhileEntering = ignored;
        for (int i = 0; i < wallColliders.Count; i++)
        {
            Collider2D wallCollider = wallColliders[i];
            if (wallCollider == null)
                continue;

            Physics2D.IgnoreCollision(bodyCollider, wallCollider, ignored);
        }
    }

    Vector2 GetTargetPoint(PlayerHealth target)
    {
        if (target == null)
            return rb.position;

        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null)
            return collider.ClosestPoint(rb.position);

        return target.transform.position;
    }

    void RotateHullToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void StartBeam(PlayerHealth target)
    {
        PhotonView targetView = target != null ? target.GetComponent<PhotonView>() : null;
        if (targetView == null || bot == null || bot.photonView == null)
            return;

        if (activeBeamTargetViewId == targetView.ViewID)
            return;

        StopBeam();
        activeBeamTargetViewId = targetView.ViewID;
        bot.photonView.RPC(nameof(EnemyBot.StartRescueShipBeamRpc), RpcTarget.All, activeBeamTargetViewId);
    }

    void StopBeam()
    {
        if (activeBeamTargetViewId <= 0 || bot == null || bot.photonView == null)
        {
            activeBeamTargetViewId = -1;
            return;
        }

        bot.photonView.RPC(nameof(EnemyBot.StopRescueShipBeamRpc), RpcTarget.All);
        activeBeamTargetViewId = -1;
    }

    void ApplyHealing(PlayerHealth target)
    {
        if (target == null)
            return;

        healAccumulator += HealPerSecond * Time.fixedDeltaTime;
        int wholePoints = Mathf.FloorToInt(healAccumulator);
        if (wholePoints <= 0)
            return;

        healAccumulator -= wholePoints;
        target.RepairVitalsAuthority(wholePoints);
        if (target.HasFullVitals)
        {
            if (Time.time >= healLockEndTime)
            {
                currentHealTarget = null;
                nextTargetRefreshTime = 0f;
                healLockEndTime = 0f;
                StopBeam();
            }
        }
    }
}

[RequireComponent(typeof(EnemyBot))]
public class EnemyRadarShipBehavior : EnemyBotBehaviorBase
{
    const float StrikeWarningDuration = 2f;
    const float StrikeRadius = 1.35f;
    const float AvoidanceScanRadius = 2.2f;
    const float AvoidanceWeight = 0.34f;
    const float PatrolRefreshInterval = 1.15f;
    const float PatrolArrivalDistance = 1.9f;
    const float PatrolFallbackTurnIntervalMin = 1.4f;
    const float PatrolFallbackTurnIntervalMax = 2.5f;
    const float MapEdgeMargin = 2f;
    const float MapEdgeSteerWeight = 0.78f;

    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyWeaponProfile weapon;
    Transform currentTarget;
    Transform patrolCollectibleTarget;
    Vector2 fallbackPatrolDirection = Vector2.up;
    float nextTargetRefreshTime;
    float nextPatrolRefreshTime;
    float nextFallbackTurnTime;
    float nextStrikeTime;
    float orbitDirection = 1f;
    bool strikePending;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        weapon = owner.Definition != null ? owner.Definition.Weapon : null;

        int seed = view != null ? view.ViewID : Random.Range(1, 9999);
        float angle = Mathf.Abs(seed * 0.171f) % (Mathf.PI * 2f);
        fallbackPatrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        orbitDirection = seed % 2 == 0 ? 1f : -1f;
        nextStrikeTime = Time.time + Random.Range(0.5f, 1.2f);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        RefreshTargetIfNeeded();
        RefreshPatrolTargetIfNeeded();

        Vector2 desiredDirection = currentTarget != null
            ? ResolveCombatDirection()
            : ResolvePatrolDirection();

        desiredDirection = ApplyCollectibleAvoidance(desiredDirection);
        desiredDirection = ApplyMapEdgeSteering(desiredDirection);
        if (desiredDirection.sqrMagnitude <= 0.001f)
            desiredDirection = fallbackPatrolDirection.sqrMagnitude > 0.001f ? fallbackPatrolDirection : Vector2.up;

        float speedMultiplier = currentTarget != null ? 1f : 0.82f;
        Vector2 desiredVelocity = desiredDirection.normalized * (bot.EffectiveMoveSpeed * speedMultiplier);
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, currentTarget != null ? 0.16f : 0.11f);

        Vector2 aimDirection = currentTarget != null
            ? (Vector2)currentTarget.position - rb.position
            : rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.normalized : desiredDirection;
        RotateHullToward(aimDirection);

        if (currentTarget != null)
            TryCallRadarStrike();
    }

    public void NotifyDamageSource(int attackerViewID)
    {
        PhotonView attackerView = attackerViewID > 0 ? PhotonView.Find(attackerViewID) : null;
        if (attackerView != null)
        {
            currentTarget = attackerView.transform;
            nextTargetRefreshTime = Time.time + 0.4f;
            nextStrikeTime = Mathf.Min(nextStrikeTime, Time.time + 0.25f);
        }
    }

    void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + Mathf.Max(0.18f, movement.TargetRefreshInterval);
        currentTarget = ResolvePlayerTarget();
    }

    void RefreshPatrolTargetIfNeeded()
    {
        if (currentTarget != null)
            return;

        if (patrolCollectibleTarget != null && patrolCollectibleTarget.gameObject.activeInHierarchy)
            return;

        if (Time.time < nextPatrolRefreshTime)
            return;

        nextPatrolRefreshTime = Time.time + PatrolRefreshInterval;
        patrolCollectibleTarget = ResolveBestCollectibleTarget();
    }

    Transform ResolvePlayerTarget()
    {
        if (EnemyTargetingUtility.IsTargetValid(currentTarget, health, rb.position, movement.DisengageRadius, false))
            return currentTarget;

        return EnemyTargetingUtility.FindClosestTarget(rb.position, health, movement.DetectionRadius, false);
    }

    bool IsValidPlayerTarget(PlayerHealth candidate, float maxDistance)
    {
        return EnemyTargetingUtility.IsTargetValid(candidate != null ? candidate.transform : null, health, rb.position, maxDistance, false);
    }

    Transform ResolveBestCollectibleTarget()
    {
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null || treasure.isBeingCollected)
                continue;

            float score = ScoreCollectible(treasure.transform.position, treasure.itemId, 1f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = treasure.transform;
            }
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot || wreck.isBeingCollected)
                continue;

            string itemId = wreck.GetLootItemAt(wreck.GetFirstLootIndex());
            float score = ScoreCollectible(wreck.transform.position, itemId, 1.18f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = wreck.transform;
            }
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot || crate.isBeingCollected)
                continue;

            float score = ScoreCollectible(crate.transform.position, crate.StoredItemId, 1.08f);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = crate.transform;
            }
        }

        return bestTarget;
    }

    float ScoreCollectible(Vector2 position, string itemId, float categoryMultiplier)
    {
        int sellValue = Mathf.Max(1, InventoryItemCatalog.GetSellValueAstrons(itemId));
        float distance = Vector2.Distance(rb.position, position);
        float rarityWeight = (int)InventoryItemCatalog.GetRarity(itemId) * 110f;
        return sellValue * categoryMultiplier + rarityWeight - distance * 38f;
    }

    Vector2 ResolvePatrolDirection()
    {
        if (patrolCollectibleTarget != null && patrolCollectibleTarget.gameObject.activeInHierarchy)
        {
            Vector2 toTarget = (Vector2)patrolCollectibleTarget.position - rb.position;
            if (toTarget.magnitude <= PatrolArrivalDistance)
            {
                patrolCollectibleTarget = null;
                nextPatrolRefreshTime = 0f;
            }
            else
            {
                return toTarget.normalized;
            }
        }

        if (Time.time >= nextFallbackTurnTime)
        {
            nextFallbackTurnTime = Time.time + Random.Range(PatrolFallbackTurnIntervalMin, PatrolFallbackTurnIntervalMax);
            float angle = Mathf.Atan2(fallbackPatrolDirection.y, fallbackPatrolDirection.x) + Random.Range(-0.52f, 0.52f);
            fallbackPatrolDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }

        return fallbackPatrolDirection;
    }

    Vector2 ResolveCombatDirection()
    {
        if (currentTarget == null)
            return ResolvePatrolDirection();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return ResolvePatrolDirection();

        Vector2 toward = toTarget / distance;
        Vector2 tangent = orbitDirection > 0f
            ? new Vector2(-toward.y, toward.x)
            : new Vector2(toward.y, -toward.x);

        float wobble = Mathf.Sin(Time.time * 1.35f + (view != null ? view.ViewID : 1) * 0.13f) * 0.14f;
        if (distance > movement.PreferredDistance + 0.5f)
            return (toward * 0.78f + tangent * (0.32f + wobble)).normalized;

        if (distance < movement.OrbitDistance)
            return (-toward * 0.62f + tangent * 0.78f).normalized;

        return (tangent * 0.92f + toward * 0.12f).normalized;
    }

    Vector2 ApplyCollectibleAvoidance(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, AvoidanceScanRadius);
        Vector2 avoidance = Vector2.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.attachedRigidbody == rb)
                continue;

            if (!IsAvoidedObject(hit))
                continue;

            Vector2 closest = hit.ClosestPoint(rb.position);
            Vector2 away = rb.position - closest;
            if (away.sqrMagnitude <= 0.0001f)
                away = rb.position - (Vector2)hit.transform.position;

            float distance = Mathf.Max(0.12f, away.magnitude);
            if (distance > AvoidanceScanRadius)
                continue;

            avoidance += away.normalized * Mathf.Clamp01((AvoidanceScanRadius - distance) / AvoidanceScanRadius);
        }

        if (avoidance.sqrMagnitude <= 0.001f)
            return desiredDirection;

        Vector2 result = (desired + avoidance.normalized * AvoidanceWeight).normalized;
        return Vector2.Dot(result, desired) < 0.1f
            ? (desired * 0.78f + avoidance.normalized * 0.22f).normalized
            : result;
    }

    bool IsAvoidedObject(Collider2D hit)
    {
        if (hit.GetComponentInParent<ObstacleChunk>() != null)
            return true;

        if (hit.GetComponentInParent<Treasure>() != null)
            return true;

        if (hit.GetComponentInParent<ShipWreck>() != null)
            return true;

        return hit.GetComponentInParent<DroppedCargoCrate>() != null;
    }

    Vector2 ApplyMapEdgeSteering(Vector2 desiredDirection)
    {
        Vector2 desired = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection.normalized : Vector2.up;
        Vector2 mapSize = RoomSettings.GetMapDimensions();
        float halfX = Mathf.Max(3f, mapSize.x * 0.5f - MapEdgeMargin);
        float halfY = Mathf.Max(3f, mapSize.y * 0.5f - MapEdgeMargin);
        Vector2 predicted = rb.position + desired * Mathf.Max(1.4f, bot.EffectiveMoveSpeed * 1.8f);
        Vector2 inward = Vector2.zero;

        if (predicted.x > halfX)
            inward.x -= Mathf.InverseLerp(halfX + 1.4f, halfX, predicted.x);
        else if (predicted.x < -halfX)
            inward.x += Mathf.InverseLerp(-halfX - 1.4f, -halfX, predicted.x);

        if (predicted.y > halfY)
            inward.y -= Mathf.InverseLerp(halfY + 1.4f, halfY, predicted.y);
        else if (predicted.y < -halfY)
            inward.y += Mathf.InverseLerp(-halfY - 1.4f, -halfY, predicted.y);

        if (inward.sqrMagnitude <= 0.001f)
            return desiredDirection;

        return (desired * (1f - MapEdgeSteerWeight) + inward.normalized * MapEdgeSteerWeight).normalized;
    }

    void RotateHullToward(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
            return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
        rb.MoveRotation(nextAngle);
    }

    void TryCallRadarStrike()
    {
        if (weapon == null || currentTarget == null || strikePending || Time.time < nextStrikeTime)
            return;

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        if (toTarget.magnitude > weapon.Range)
            return;

        Vector2 strikePoint = currentTarget.position;
        strikePending = true;
        nextStrikeTime = Time.time + Mathf.Max(0.2f, weapon.FireRate);

        if (bot.photonView != null)
        {
            bot.photonView.RPC(nameof(EnemyBot.PlayRadarShipIncomingRpc), RpcTarget.All, strikePoint.x, strikePoint.y, 0f);
            bot.photonView.RPC(nameof(EnemyBot.SpawnRadarStrikeMarkerRpc), RpcTarget.All, strikePoint.x, strikePoint.y, StrikeWarningDuration, StrikeRadius);
        }

        StartCoroutine(ExecuteStrikeAfterDelay(strikePoint));
    }

    IEnumerator ExecuteStrikeAfterDelay(Vector2 strikePoint)
    {
        yield return new WaitForSeconds(StrikeWarningDuration);

        if (bot == null || bot.photonView == null)
        {
            strikePending = false;
            yield break;
        }

        bot.photonView.RPC(nameof(EnemyBot.PlayRadarShipShootRpc), RpcTarget.All, strikePoint.x, strikePoint.y, 0f);
        ApplyStrikeDamage(strikePoint);
        bot.photonView.RPC(nameof(EnemyBot.SpawnRadarStrikeImpactRpc), RpcTarget.All, strikePoint.x, strikePoint.y, StrikeRadius);
        strikePending = false;
    }

    void ApplyStrikeDamage(Vector2 strikePoint)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(strikePoint, StrikeRadius);
        HashSet<int> processedViewIds = new HashSet<int>();
        int attackerViewId = bot.photonView != null ? bot.photonView.ViewID : 0;
        int baseDamage = RoomSettings.GetEnemyDamage(bot.Kind);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth candidate = hits[i] != null ? hits[i].GetComponentInParent<PlayerHealth>() : null;
            PhotonView targetView = candidate != null ? candidate.GetComponent<PhotonView>() : null;
            if (candidate == null || targetView == null || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating)
                continue;

            if (candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (!processedViewIds.Add(targetView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, candidate.transform.position);
            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            targetView.RPC(nameof(PlayerHealth.TakeDamageProfileAt), RpcTarget.MasterClient, damage, damage, attackerViewId, strikePoint.x, strikePoint.y);
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted || beacon.photonView == null)
                continue;

            if (!processedViewIds.Add(beacon.photonView.ViewID))
                continue;

            float distance = Vector2.Distance(strikePoint, beacon.transform.position);
            if (distance > StrikeRadius)
                continue;

            float falloff = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(distance / StrikeRadius));
            int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * falloff));
            beacon.photonView.RPC(nameof(LureBeaconDecoy.TakeBeaconDamageProfileAt), RpcTarget.MasterClient, damage, damage, attackerViewId, strikePoint.x, strikePoint.y);
        }
    }
}

public sealed class RadarStrikeVfx : MonoBehaviour
{
    enum VfxMode
    {
        Marker,
        Impact
    }

    const float MarkerZ = -0.34f;
    const float ImpactZ = -0.35f;
    const int MarkerSortingOrder = 7100;
    const int ImpactSortingOrder = 7150;

    static Material lineMaterial;

    VfxMode mode;
    Vector2 worldPosition;
    float duration;
    float radius;
    float startedAt;
    LineRenderer outerGlow;
    LineRenderer outerCore;
    LineRenderer innerCore;
    LineRenderer crossA;
    LineRenderer crossB;
    LineRenderer impactFlash;
    LineRenderer impactRing;
    LineRenderer impactRingOuter;
    LineRenderer impactBeam;
    LineRenderer impactCoreGlow;
    LineRenderer[] impactSparks;

    public static void SpawnMarker(Vector2 position, float warningDuration, float radius)
    {
        GameObject effect = new GameObject("RadarStrikeMarkerVfx");
        RadarStrikeVfx vfx = effect.AddComponent<RadarStrikeVfx>();
        vfx.InitializeMarker(position, warningDuration, radius);
    }

    public static void SpawnImpact(Vector2 position, float radius)
    {
        GameObject effect = new GameObject("RadarStrikeImpactVfx");
        RadarStrikeVfx vfx = effect.AddComponent<RadarStrikeVfx>();
        vfx.InitializeImpact(position, radius);
    }

    void InitializeMarker(Vector2 position, float warningDuration, float configuredRadius)
    {
        mode = VfxMode.Marker;
        worldPosition = position;
        duration = Mathf.Max(0.2f, warningDuration);
        radius = Mathf.Max(0.45f, configuredRadius);
        startedAt = Time.time;
        EnsureMarkerLines();
        transform.position = new Vector3(position.x, position.y, MarkerZ);
    }

    void InitializeImpact(Vector2 position, float configuredRadius)
    {
        mode = VfxMode.Impact;
        worldPosition = position;
        duration = 0.92f;
        radius = Mathf.Max(0.45f, configuredRadius);
        startedAt = Time.time;
        EnsureImpactLines();
        transform.position = new Vector3(position.x, position.y, ImpactZ);
    }

    void Update()
    {
        float elapsed = Time.time - startedAt;
        float progress = duration > 0.001f ? Mathf.Clamp01(elapsed / duration) : 1f;

        if (mode == VfxMode.Marker)
        {
            UpdateMarker(progress);
            if (progress >= 1f)
                Destroy(gameObject);
            return;
        }

        UpdateImpact(progress);
        if (progress >= 1f)
            Destroy(gameObject);
    }

    void EnsureMarkerLines()
    {
        outerGlow = CreateLine("OuterGlow", MarkerSortingOrder, 0.22f);
        outerCore = CreateLine("OuterCore", MarkerSortingOrder + 1, 0.08f);
        innerCore = CreateLine("InnerCore", MarkerSortingOrder + 2, 0.05f);
        crossA = CreateLine("CrossA", MarkerSortingOrder + 3, 0.07f);
        crossB = CreateLine("CrossB", MarkerSortingOrder + 3, 0.07f);

        outerGlow.loop = true;
        outerCore.loop = true;
        innerCore.loop = true;
    }

    void EnsureImpactLines()
    {
        impactBeam = CreateLine("ImpactBeam", ImpactSortingOrder, 0.24f);
        impactCoreGlow = CreateLine("ImpactCoreGlow", ImpactSortingOrder + 1, 0.34f);
        impactFlash = CreateLine("ImpactFlash", ImpactSortingOrder + 2, 0.16f);
        impactRing = CreateLine("ImpactRing", ImpactSortingOrder + 3, 0.12f);
        impactRingOuter = CreateLine("ImpactRingOuter", ImpactSortingOrder + 2, 0.22f);
        impactSparks = new LineRenderer[7];
        for (int i = 0; i < impactSparks.Length; i++)
            impactSparks[i] = CreateLine("ImpactSpark" + i, ImpactSortingOrder + 4 + i, 0.06f + i * 0.004f);

        impactCoreGlow.loop = true;
        impactRing.loop = true;
        impactRingOuter.loop = true;
    }

    void UpdateMarker(float progress)
    {
        float pulse = Mathf.Sin(Time.time * 9.4f) * 0.5f + 0.5f;
        float urgency = Mathf.SmoothStep(0f, 1f, progress);
        Color warning = Color.Lerp(new Color(1f, 0.68f, 0.2f, 0.95f), new Color(1f, 0.12f, 0.08f, 1f), urgency);
        Color glow = new Color(warning.r, warning.g * 0.9f, warning.b * 0.9f, Mathf.Lerp(0.24f, 0.52f, pulse));
        float outerRadius = radius * Mathf.Lerp(0.86f, 1.08f, pulse);
        float innerRadius = radius * 0.58f;
        float crossExtent = radius * 0.7f;

        UpdateRing(outerGlow, outerRadius, glow, MarkerSortingOrder, 36, 0.06f);
        UpdateRing(outerCore, radius, new Color(1f, 0.98f, 0.9f, 0.96f), MarkerSortingOrder + 1, 32, 0.03f);
        UpdateRing(innerCore, innerRadius, new Color(warning.r, warning.g, warning.b, 0.92f), MarkerSortingOrder + 2, 28, 0.04f);

        Vector3 center = new Vector3(worldPosition.x, worldPosition.y, MarkerZ);
        UpdateSimpleLine(crossA, center + new Vector3(-crossExtent, 0f, 0f), center + new Vector3(crossExtent, 0f, 0f), warning, MarkerSortingOrder + 3);
        UpdateSimpleLine(crossB, center + new Vector3(0f, -crossExtent, 0f), center + new Vector3(0f, crossExtent, 0f), warning, MarkerSortingOrder + 3);
    }

    void UpdateImpact(float progress)
    {
        float inverse = 1f - progress;
        float blast = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 1.25f));
        Color hot = Color.Lerp(new Color(1f, 0.98f, 0.9f, 1f), new Color(1f, 0.36f, 0.08f, 0f), blast);
        Color glow = Color.Lerp(new Color(1f, 0.86f, 0.52f, 0.92f), new Color(0.46f, 0.16f, 0.06f, 0f), progress);
        Color ring = Color.Lerp(new Color(1f, 0.62f, 0.18f, 0.96f), new Color(0.82f, 0.1f, 0.04f, 0f), progress);
        Vector3 center = new Vector3(worldPosition.x, worldPosition.y, ImpactZ);

        float beamLength = Mathf.Lerp(radius * 3.1f, radius * 0.45f, blast);
        UpdateSimpleLine(
            impactBeam,
            center + new Vector3(radius * 0.08f, beamLength, 0f),
            center,
            new Color(1f, 0.88f, 0.58f, inverse * 0.86f),
            ImpactSortingOrder);

        float coreRadius = Mathf.Lerp(radius * 0.32f, radius * 0.92f, blast);
        float flashRadius = Mathf.Lerp(radius * 0.22f, radius * 1.14f, Mathf.SmoothStep(0f, 1f, progress));
        UpdateRing(impactCoreGlow, coreRadius, glow, ImpactSortingOrder + 1, 24, 0.16f);
        UpdateRing(impactFlash, flashRadius, hot, ImpactSortingOrder + 2, 22, 0.12f);
        UpdateRing(impactRing, Mathf.Lerp(radius * 0.18f, radius * 1.0f, progress), ring, ImpactSortingOrder + 3, 28, 0.18f);
        UpdateRing(impactRingOuter, Mathf.Lerp(radius * 0.4f, radius * 1.45f, progress), new Color(ring.r, ring.g, ring.b, inverse * 0.26f), ImpactSortingOrder + 2, 30, 0.24f);
        UpdateImpactSparks(progress, center);
    }

    void UpdateImpactSparks(float progress, Vector3 center)
    {
        if (impactSparks == null)
            return;

        float inverse = 1f - progress;
        for (int i = 0; i < impactSparks.Length; i++)
        {
            LineRenderer spark = impactSparks[i];
            if (spark == null)
                continue;

            float angle = ((360f / impactSparks.Length) * i + 18f * Mathf.Sin(i * 3.17f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.72f + 0.28f).normalized;
            float startDistance = Mathf.Lerp(radius * 0.08f, radius * 0.42f, progress);
            float length = Mathf.Lerp(radius * (0.9f + i * 0.08f), radius * 0.18f, progress);
            Vector3 start = center + (Vector3)(dir * startDistance);
            Vector3 end = start + (Vector3)(dir * length);
            Color sparkColor = Color.Lerp(new Color(1f, 0.92f, 0.68f, inverse * 0.95f), new Color(0.82f, 0.2f, 0.05f, 0f), progress);
            UpdateSimpleLine(spark, start, end, sparkColor, ImpactSortingOrder + 4 + i);
        }
    }

    LineRenderer CreateLine(string lineName, int sortingOrder, float width)
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader)
            {
                name = "RadarStrikeVfxMaterial",
                color = Color.white
            };
            lineMaterial.renderQueue = 5000;
        }

        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.useWorldSpace = true;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 10;
        line.numCornerVertices = 8;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        line.sortingOrder = sortingOrder;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = 0;
        return line;
    }

    void UpdateRing(LineRenderer line, float ringRadius, Color color, int sortingOrder, int segments, float wobble = 0f)
    {
        if (line == null)
            return;

        line.enabled = color.a > 0.001f;
        line.sortingOrder = sortingOrder;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = segments;

        Vector3 center = new Vector3(worldPosition.x, worldPosition.y, mode == VfxMode.Marker ? MarkerZ : ImpactZ);
        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments * Mathf.PI * 2f;
            float localRadius = ringRadius;
            if (wobble > 0.0001f)
                localRadius *= 1f + Mathf.Sin(t * 3f + startedAt * 1.7f + Time.time * 9.5f + i * 0.19f) * wobble;

            line.SetPosition(i, center + new Vector3(Mathf.Cos(t) * localRadius, Mathf.Sin(t) * localRadius, 0f));
        }
    }

    void UpdateSimpleLine(LineRenderer line, Vector3 start, Vector3 end, Color color, int sortingOrder)
    {
        if (line == null)
            return;

        line.enabled = color.a > 0.001f;
        line.sortingOrder = sortingOrder;
        line.startColor = color;
        line.endColor = color;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }
}
