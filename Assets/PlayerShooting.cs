using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;

public class PlayerShooting : MonoBehaviourPun
{
    const float AutoAimRange = 13f;
    const float ManualAimThreshold = 0.35f;
    const float DefaultBulletRangeMultiplier = 15f;
    const float GadgetMinePlacementCooldown = 0.9f;
    const int GadgetMineDefaultCharges = 4;
    const int BatteryDefaultCharges = 3;
    static readonly Color PlasmaBulletColor = new Color(0.15f, 1f, 0.28f, 1f);

    sealed class GadgetRuntimeState
    {
        public string ItemId;
        public int MaxCharges;
        public int RemainingCharges;
        public float Cooldown;
        public float NextUseTime;
    }

    public Joystick shootJoystick;
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;
    public float fireRate = 0.3f;
    public int maxAmmo = 10;
    public float reloadDuration = 4f;
    public int bulletDamage = 10;
    public float bulletScaleMultiplier = 1f;
    public Color bulletColor = Color.white;
    public float muzzleOffsetDistance = 0.5f;
    public float bulletRangeMultiplier = DefaultBulletRangeMultiplier;
    public bool infiniteAmmo;
    public string shotSoundId = string.Empty;
    public static bool gameStarted = false;

    float nextFireTime = 0f;
    int currentAmmo;
    bool isReloading;
    float reloadFinishTime;
    bool customAmmoProfileActive;
    bool baseWeaponProfileCaptured;
    float baseBulletSpeed;
    float baseFireRate;
    float baseReloadDuration;
    int baseBulletDamage;
    float baseBulletScaleMultiplier;
    Color baseBulletColor;
    float baseMuzzleOffsetDistance;
    float baseBulletRangeMultiplier;
    bool baseInfiniteAmmo;
    string baseShotSoundId = string.Empty;
    int multiShotCount = 1;
    string lastAppliedWeaponSignature = string.Empty;
    string lastAppliedGadgetSignature = string.Empty;
    readonly List<string> activeGadgetItemIds = new List<string>();
    readonly Dictionary<string, GadgetRuntimeState> gadgetStates = new Dictionary<string, GadgetRuntimeState>(StringComparer.Ordinal);
    readonly Dictionary<string, int> authoritativeGadgetCharges = new Dictionary<string, int>(StringComparer.Ordinal);
    string lastAuthoritativeGadgetChargeStateRaw = null;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsReloading => isReloading;
    public bool CanManualReload => photonView.IsMine && IsGameStarted() && !isReloading && currentAmmo > 0 && currentAmmo < maxAmmo;
    public IReadOnlyList<string> ActiveGadgetItemIds => activeGadgetItemIds;
    public string CurrentGadgetItemId => activeGadgetItemIds.Count > 0 ? activeGadgetItemIds[0] : null;
    public Sprite CurrentGadgetIcon => !string.IsNullOrWhiteSpace(CurrentGadgetItemId) ? InventoryItemCatalog.GetIcon(CurrentGadgetItemId) : null;
    public int RemainingGadgetCharges => GetRemainingGadgetCharges(CurrentGadgetItemId);
    public int MaxGadgetCharges => GetMaxGadgetCharges(CurrentGadgetItemId);
    public float ReloadProgress
    {
        get
        {
            if (!isReloading || reloadDuration <= 0f)
                return 0f;

            float remaining = Mathf.Max(0f, reloadFinishTime - Time.time);
            return 1f - Mathf.Clamp01(remaining / reloadDuration);
        }
    }

    void Start()
    {
        EnsureBotBootstrap();
        CaptureBaseWeaponProfile();
        maxAmmo = GetConfiguredMaxAmmo();
        currentAmmo = maxAmmo;

        if (AstronautSurvivor.IsAstronautInstantiationData(photonView.InstantiationData))
            return;

        if (GetComponent<EnemyBot>() != null)
            return;

        if (!photonView.IsMine)
            return;

        if (GetComponent<AmmoUI>() == null)
        {
            gameObject.AddComponent<AmmoUI>();
        }

        if (GetComponent<ReloadButtonUI>() == null)
        {
            gameObject.AddComponent<ReloadButtonUI>();
        }

        if (GetComponent<GadgetButtonUI>() == null)
        {
            gameObject.AddComponent<GadgetButtonUI>();
        }

        Debug.Log("PlayerShooting START");
    }

    void Update()
    {
        EnsureBotBootstrap();

        if (!IsGameStarted())
            return;

        if (GetComponent<EnemyBot>() != null)
        {
            UpdateReload();
            return;
        }

        if (!photonView.IsMine)
            return;

        SyncEquippedWeaponProfile();
        SyncEquippedGadgetProfile();
        RefreshAuthoritativeGadgetRuntimeStates();
        SyncAmmoSetting();

        UpdateReload();

        if (shootJoystick == null)
        {
            GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
            if (shootJoystickObject != null)
            {
                shootJoystick = shootJoystickObject.GetComponent<Joystick>();
                Debug.Log("ShootJoystick found");
            }
        }

        if (shootJoystick == null)
            return;

        if (isReloading || currentAmmo <= 0)
            return;

        if (!shootJoystick.IsPressed)
            return;

        Vector2 direction = ResolveManualAimDirection();

        if (Time.time >= nextFireTime)
        {
            Shoot(direction.normalized);
            ConsumeAmmo();
            nextFireTime = Time.time + fireRate;
        }
    }

    Vector2 ResolveManualAimDirection()
    {
        Vector2 rawDirection = shootJoystick != null ? shootJoystick.inputVector : Vector2.zero;
        if (rawDirection.magnitude >= ManualAimThreshold)
            return rawDirection.normalized;

        return FindAutoAimDirection();
    }

    Vector2 FindAutoAimDirection()
    {
        PlayerHealth[] targets = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Transform bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            PlayerHealth target = targets[i];
            if (target == null || target.IsWreck || target.photonView == null || target.photonView.ViewID == photonView.ViewID)
                continue;

            HideInNebulaTarget nebulaState = target.GetComponent<HideInNebulaTarget>();
            if (nebulaState != null && nebulaState.IsHiddenFromLocalPlayer())
                continue;

            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance > AutoAimRange || distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = target.transform;
        }

        if (bestTarget != null)
        {
            Vector2 aim = (bestTarget.position - transform.position);
            if (aim.sqrMagnitude > 0.001f)
                return aim.normalized;
        }

        return transform.up;
    }

    void Shoot(Vector2 direction)
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("bulletPrefab NULL");
            return;
        }

        PhotonView playerView = GetComponent<PhotonView>();
        int ownerId = playerView != null ? playerView.ViewID : 0;
        bool spawned = false;

        if (ShouldFireFromDualWingMuzzles())
        {
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(-1f), ownerId);
            spawned |= SpawnBullet(direction, GetWingMuzzlePosition(1f), ownerId);
        }
        else
        {
            Vector3 spawnPos = transform.position + (transform.up * muzzleOffsetDistance);
            spawned |= SpawnBullet(direction, spawnPos, ownerId);
        }

        if (spawned)
            photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
    }

    public bool TryFireBot(Vector2 direction)
    {
        if (GetComponent<EnemyBot>() == null)
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        Shoot(direction.normalized);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + fireRate;
        return true;
    }

    public bool TryFireBotFromWorld(Vector2 direction, Vector3 spawnPosition, float cooldownOffset = 0f)
    {
        if (GetComponent<EnemyBot>() == null)
            return false;

        if (!photonView.IsMine || !IsGameStarted())
            return false;

        UpdateReload();

        if (Time.time < nextFireTime || direction.sqrMagnitude < 0.04f)
            return false;

        if (!infiniteAmmo && (isReloading || currentAmmo <= 0))
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = SpawnBullet(direction.normalized, spawnPosition, ownerId);
        if (!spawned)
            return false;

        photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);
        if (!infiniteAmmo)
            ConsumeAmmo();
        nextFireTime = Time.time + fireRate + Mathf.Max(0f, cooldownOffset);
        return true;
    }

    public bool FireBotProjectileFromWorld(Vector2 direction, Vector3 spawnPosition)
    {
        if (GetComponent<EnemyBot>() == null)
            return false;

        if (!photonView.IsMine || !IsGameStarted() || direction.sqrMagnitude < 0.04f)
            return false;

        int ownerId = photonView != null ? photonView.ViewID : 0;
        bool spawned = SpawnBullet(direction.normalized, spawnPosition, ownerId);
        if (spawned)
            photonView.RPC(nameof(PlayLaserSfx), RpcTarget.All);

        return spawned;
    }

    void ConsumeAmmo()
    {
        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        if (currentAmmo <= 0)
        {
            StartReload(false);
        }
    }

    void StartReload(bool playSound)
    {
        if (isReloading)
            return;

        isReloading = true;
        reloadFinishTime = Time.time + reloadDuration;

        if (playSound)
        {
            photonView.RPC(nameof(PlayReloadSfx), RpcTarget.All);
        }
    }

    void UpdateReload()
    {
        if (!isReloading)
            return;

        if (Time.time < reloadFinishTime)
            return;

        isReloading = false;
        currentAmmo = maxAmmo;
    }

    void SyncAmmoSetting()
    {
        if (customAmmoProfileActive && GetComponent<EnemyBot>() != null)
            return;

        int configuredAmmo = GetConfiguredMaxAmmo();
        if (configuredAmmo == maxAmmo)
            return;

        int previousMaxAmmo = maxAmmo;
        maxAmmo = configuredAmmo;

        if (isReloading)
            return;

        if (currentAmmo == previousMaxAmmo)
        {
            currentAmmo = maxAmmo;
        }
        else
        {
            currentAmmo = Mathf.Min(currentAmmo, maxAmmo);
        }
    }

    void CaptureBaseWeaponProfile()
    {
        if (baseWeaponProfileCaptured || GetComponent<EnemyBot>() != null)
            return;

        baseBulletSpeed = bulletSpeed;
        baseFireRate = fireRate;
        baseReloadDuration = reloadDuration;
        baseBulletDamage = bulletDamage;
        baseBulletScaleMultiplier = bulletScaleMultiplier;
        baseBulletColor = bulletColor;
        baseMuzzleOffsetDistance = muzzleOffsetDistance;
        baseBulletRangeMultiplier = bulletRangeMultiplier;
        baseInfiniteAmmo = infiniteAmmo;
        baseShotSoundId = shotSoundId ?? string.Empty;
        baseWeaponProfileCaptured = true;
    }

    void SyncEquippedWeaponProfile()
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null)
            return;

        CaptureBaseWeaponProfile();
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        int plasmaGunCount = CountEquippedPlasmaGuns(equipmentSlots, shipSkinIndex);
        string signature = shipSkinIndex + ":" + plasmaGunCount;
        if (signature == lastAppliedWeaponSignature)
            return;

        ApplyPlayerWeaponProfile(plasmaGunCount);
        lastAppliedWeaponSignature = signature;
    }

    int CountEquippedPlasmaGuns(string[] equipmentSlots, int shipSkinIndex)
    {
        if (equipmentSlots == null)
            return 0;

        int count = 0;
        if (ShipCatalog.IsEquipmentSlotEnabled(0, shipSkinIndex) &&
            string.Equals(GetEquipmentItem(equipmentSlots, 0), InventoryItemCatalog.PlasmaGunId, StringComparison.Ordinal))
        {
            count++;
        }

        if (ShipCatalog.IsEquipmentSlotEnabled(1, shipSkinIndex) &&
            string.Equals(GetEquipmentItem(equipmentSlots, 1), InventoryItemCatalog.PlasmaGunId, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    static string GetEquipmentItem(string[] equipmentSlots, int index)
    {
        return equipmentSlots != null && index >= 0 && index < equipmentSlots.Length
            ? equipmentSlots[index]
            : null;
    }

    void ApplyPlayerWeaponProfile(int plasmaGunCount)
    {
        if (!baseWeaponProfileCaptured)
            return;

        if (plasmaGunCount > 0)
        {
            fireRate = Mathf.Max(0.05f, baseFireRate * 2f);
            reloadDuration = baseReloadDuration;
            bulletDamage = Mathf.Max(1, baseBulletDamage * 2);
            bulletScaleMultiplier = Mathf.Max(baseBulletScaleMultiplier, 2f);
            bulletColor = PlasmaBulletColor;
            muzzleOffsetDistance = baseMuzzleOffsetDistance;
            bulletRangeMultiplier = Mathf.Max(0.25f, baseBulletRangeMultiplier * 2f);
            infiniteAmmo = baseInfiniteAmmo;
            bulletSpeed = baseBulletSpeed;
            shotSoundId = "corsair";
            multiShotCount = plasmaGunCount >= 2 ? 2 : 1;
            return;
        }

        fireRate = baseFireRate;
        reloadDuration = baseReloadDuration;
        bulletDamage = baseBulletDamage;
        bulletScaleMultiplier = baseBulletScaleMultiplier;
        bulletColor = baseBulletColor;
        muzzleOffsetDistance = baseMuzzleOffsetDistance;
        bulletRangeMultiplier = baseBulletRangeMultiplier;
        infiniteAmmo = baseInfiniteAmmo;
        bulletSpeed = baseBulletSpeed;
        shotSoundId = baseShotSoundId;
        multiShotCount = 1;
    }

    void SyncEquippedGadgetProfile()
    {
        if (!photonView.IsMine || GetComponent<EnemyBot>() != null)
            return;

        RefreshAuthoritativeGadgetChargeCache();
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        string signature = BuildGadgetSignature(shipSkinIndex, orderedItems, gadgetCounts);
        if (signature == lastAppliedGadgetSignature)
            return;

        Dictionary<string, int> previousCharges = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, GadgetRuntimeState> pair in gadgetStates)
        {
            if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Key))
                continue;

            previousCharges[pair.Key] = pair.Value.RemainingCharges;
        }

        activeGadgetItemIds.Clear();
        gadgetStates.Clear();
        for (int i = 0; i < orderedItems.Count; i++)
        {
            string itemId = orderedItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
            int maxCharges = ResolveGadgetMaxCharges(itemId, equippedCount);
            if (maxCharges <= 0)
                continue;

            int remainingCharges = authoritativeGadgetCharges.TryGetValue(itemId, out int authoritativeRemaining)
                ? Mathf.Clamp(authoritativeRemaining, 0, maxCharges)
                : previousCharges.TryGetValue(itemId, out int previousRemaining)
                    ? Mathf.Clamp(previousRemaining, 0, maxCharges)
                    : maxCharges;

            GadgetRuntimeState state = new GadgetRuntimeState
            {
                ItemId = itemId,
                MaxCharges = maxCharges,
                RemainingCharges = remainingCharges,
                Cooldown = ResolveGadgetCooldown(itemId),
                NextUseTime = 0f
            };

            activeGadgetItemIds.Add(itemId);
            gadgetStates[itemId] = state;
        }

        lastAppliedGadgetSignature = signature;
    }

    void RefreshAuthoritativeGadgetRuntimeStates()
    {
        if (!photonView.IsMine || gadgetStates.Count == 0)
            return;

        RefreshAuthoritativeGadgetChargeCache();
        foreach (KeyValuePair<string, GadgetRuntimeState> pair in gadgetStates)
        {
            GadgetRuntimeState state = pair.Value;
            if (state == null)
                continue;

            if (authoritativeGadgetCharges.TryGetValue(pair.Key, out int remainingCharges))
                state.RemainingCharges = Mathf.Clamp(remainingCharges, 0, Mathf.Max(0, state.MaxCharges));
            else
                state.RemainingCharges = Mathf.Clamp(state.RemainingCharges, 0, Mathf.Max(0, state.MaxCharges));
        }
    }

    void RefreshAuthoritativeGadgetChargeCache()
    {
        string rawState = GetAuthoritativeGadgetChargeStateRaw();
        if (string.Equals(rawState, lastAuthoritativeGadgetChargeStateRaw, StringComparison.Ordinal))
            return;

        lastAuthoritativeGadgetChargeStateRaw = rawState;
        authoritativeGadgetCharges.Clear();
        ParseAuthoritativeGadgetChargesForActor(
            rawState,
            photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : (PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1),
            authoritativeGadgetCharges);
    }

    string GetAuthoritativeGadgetChargeStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.GadgetChargesStateKey, out object value) &&
            value is string serializedState)
        {
            return serializedState;
        }

        return string.Empty;
    }

    Dictionary<string, int> CollectEquippedGadgetCounts(string[] equipmentSlots, int shipSkinIndex, List<string> orderedItems)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (equipmentSlots == null)
            return counts;

        for (int i = 6; i <= 7; i++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(i, shipSkinIndex))
                continue;

            string itemId = GetEquipmentItem(equipmentSlots, i);
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (!counts.ContainsKey(itemId))
            {
                counts[itemId] = 0;
                orderedItems?.Add(itemId);
            }

            counts[itemId]++;
        }

        return counts;
    }

    static string BuildGadgetSignature(int shipSkinIndex, List<string> orderedItems, Dictionary<string, int> counts)
    {
        if (orderedItems == null || orderedItems.Count == 0)
            return shipSkinIndex + ":none";

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append(shipSkinIndex);
        builder.Append(':');
        for (int i = 0; i < orderedItems.Count; i++)
        {
            string itemId = orderedItems[i];
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            if (builder[builder.Length - 1] != ':')
                builder.Append('|');

            builder.Append(itemId);
            builder.Append('x');
            builder.Append(counts != null && counts.TryGetValue(itemId, out int count) ? count : 0);
        }

        return builder.ToString();
    }

    bool ShouldFireFromDualWingMuzzles()
    {
        return multiShotCount >= 2;
    }

    Vector3 GetWingMuzzlePosition(float side)
    {
        float lateralOffset = GetWingMuzzleOffset();
        float forwardOffset = Mathf.Max(0.18f, muzzleOffsetDistance * 0.7f);
        return transform.position + (transform.up * forwardOffset) + (transform.right * (lateralOffset * side));
    }

    float GetWingMuzzleOffset()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            float halfWidth = spriteRenderer.sprite.bounds.extents.x * Mathf.Abs(spriteRenderer.transform.lossyScale.x);
            return Mathf.Max(0.35f, halfWidth * 0.82f);
        }

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(0.35f, collider2D.bounds.extents.x * 0.72f);

        return 0.55f;
    }

    bool SpawnBullet(Vector2 direction, Vector3 spawnPos, int ownerId)
    {
        GameObject bullet = PhotonNetwork.Instantiate(
            bulletPrefab.name,
            spawnPos,
            Quaternion.identity,
            0,
            new object[]
            {
                ownerId,
                bulletDamage,
                bulletScaleMultiplier,
                bulletColor.r,
                bulletColor.g,
                bulletColor.b,
                bulletColor.a,
                bulletRangeMultiplier
            }
        );

        if (bullet == null)
        {
            Debug.LogError("Bullet failed to spawn");
            return false;
        }

        Bullet bulletComponent = bullet.GetComponent<Bullet>();
        if (bulletComponent != null)
            bulletComponent.ownerViewID = ownerId;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * bulletSpeed;
        }
        else
        {
            Debug.LogError("Bullet is missing Rigidbody2D");
        }

        Collider2D playerCollider = GetComponent<Collider2D>();
        Collider2D bulletCollider = bullet.GetComponent<Collider2D>();
        if (bulletCollider != null && playerCollider != null)
            Physics2D.IgnoreCollision(bulletCollider, playerCollider);

        return true;
    }

    bool IsGameStarted()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value))
        {
            return (bool)value;
        }

        return false;
    }

    int GetConfiguredMaxAmmo()
    {
        if (customAmmoProfileActive && GetComponent<EnemyBot>() != null)
            return maxAmmo;

        return RoomSettings.GetAmmoCount();
    }

    public void TriggerManualReload()
    {
        if (!CanManualReload)
            return;

        StartReload(true);
    }

    public void TriggerGadgetUse()
    {
        TriggerGadgetUse(CurrentGadgetItemId);
    }

    public void TriggerGadgetUse(string itemId)
    {
        if (!CanUseGadget(itemId))
            return;

        if (string.IsNullOrWhiteSpace(itemId))
            return;

        if (gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) && state != null && state.Cooldown > 0f)
            state.NextUseTime = Time.time + state.Cooldown;

        photonView.RPC(nameof(RequestAuthoritativeGadgetUse), RpcTarget.MasterClient, itemId);
    }

    public void ConfigureWeaponProfile(float configuredFireRate, int configuredMaxAmmo, float configuredReloadDuration, int configuredBulletDamage, float configuredBulletScaleMultiplier, Color configuredBulletColor, float configuredMuzzleOffsetDistance, bool configuredInfiniteAmmo, float configuredBulletSpeed = -1f, string configuredShotSoundId = "", float configuredRangeMultiplier = -1f)
    {
        customAmmoProfileActive = true;
        fireRate = Mathf.Max(0.05f, configuredFireRate);
        maxAmmo = Mathf.Max(1, configuredMaxAmmo);
        reloadDuration = Mathf.Max(0f, configuredReloadDuration);
        bulletDamage = Mathf.Max(0, configuredBulletDamage);
        bulletScaleMultiplier = Mathf.Max(0.25f, configuredBulletScaleMultiplier);
        bulletColor = configuredBulletColor;
        muzzleOffsetDistance = Mathf.Max(0f, configuredMuzzleOffsetDistance);
        infiniteAmmo = configuredInfiniteAmmo;
        shotSoundId = configuredShotSoundId ?? string.Empty;

        if (configuredBulletSpeed > 0f)
            bulletSpeed = configuredBulletSpeed;

        if (configuredRangeMultiplier > 0f)
            bulletRangeMultiplier = configuredRangeMultiplier;

        isReloading = false;
        reloadFinishTime = 0f;
        currentAmmo = maxAmmo;
    }

    [PunRPC]
    void PlayLaserSfx()
    {
        if (shotSoundId == "corsair")
        {
            AudioManager.Instance.PlayCorsairLaserAt(transform.position);
            return;
        }

        AudioManager.Instance.PlayLaserAt(transform.position);
    }

    [PunRPC]
    void PlayReloadSfx()
    {
        AudioManager.Instance.PlayReloadAt(transform.position);
    }

    bool TryDeployGadgetMine()
    {
        EnemyBotDefinition mineDefinition = EnemyBotCatalog.GetDefinition(EnemyBotKind.SpaceMine);
        if (mineDefinition == null)
            return false;

        Vector3 spawnPosition = ResolveGadgetMineSpawnPosition();
        GameObject mineObject = PhotonNetwork.Instantiate(
            "Player",
            spawnPosition,
            transform.rotation,
            0,
            new object[]
            {
                mineDefinition.InstantiationMarker,
                "player_gadget_mine",
                photonView != null ? photonView.ViewID : 0
            });

        if (mineObject != null)
        {
            EnemyBot bot = mineObject.GetComponent<EnemyBot>();
            if (bot == null)
                bot = mineObject.AddComponent<EnemyBot>();

            bot.InitializeFromPhotonData();
        }

        return true;
    }

    Vector3 ResolveGadgetMineSpawnPosition()
    {
        float baseDistance = GetGadgetMinePlacementDistance();
        Vector2 forward = transform.up.sqrMagnitude > 0.001f ? (Vector2)transform.up.normalized : Vector2.up;
        Vector2 right = transform.right.sqrMagnitude > 0.001f ? (Vector2)transform.right.normalized : Vector2.right;
        Vector2 origin = transform.position;
        Vector2[] directions =
        {
            -forward,
            right,
            -right,
            (-forward + right).normalized,
            (-forward - right).normalized,
            forward
        };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector2 candidate = origin + (directions[i] * baseDistance);
            if (IsMineSpawnPositionFree(candidate))
                return new Vector3(candidate.x, candidate.y, 0f);
        }

        Vector2 fallback = origin - (forward * baseDistance);
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    float GetGadgetMinePlacementDistance()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            float shipExtent = Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y);
            return Mathf.Max(0.9f, shipExtent + 0.55f);
        }

        Collider2D collider2D = GetComponentInChildren<Collider2D>();
        if (collider2D != null)
            return Mathf.Max(0.9f, Mathf.Max(collider2D.bounds.extents.x, collider2D.bounds.extents.y) + 0.55f);

        return 1.15f;
    }

    bool IsMineSpawnPositionFree(Vector2 candidate)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, 0.38f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    bool TryActivateBatteryCharge()
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        return health != null && health.TryBeginBatteryShieldChargeAuthority();
    }

    public bool CanUseGadget(string itemId)
    {
        if (!photonView.IsMine || !IsGameStarted() || string.IsNullOrWhiteSpace(itemId))
            return false;

        if (!gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return false;

        if (state.RemainingCharges <= 0 || Time.time < state.NextUseTime)
            return false;

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            return health != null && health.CanActivateBatteryChargeLocally();
        }

        return true;
    }

    public int GetRemainingGadgetCharges(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return 0;

        return Mathf.Max(0, state.RemainingCharges);
    }

    public int GetMaxGadgetCharges(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) || state == null)
            return 0;

        return Mathf.Max(0, state.MaxCharges);
    }

    public Sprite GetGadgetIcon(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId) ? null : InventoryItemCatalog.GetIcon(itemId);
    }

    public string GetGadgetButtonLabel(string itemId)
    {
        return InventoryItemCatalog.GetShortLabel(itemId);
    }

    public Color GetGadgetButtonColor(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return new Color(0.14f, 0.5f, 0.28f, 0.96f);

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return new Color(0.16f, 0.46f, 0.78f, 0.96f);

        return new Color(0.22f, 0.3f, 0.4f, 0.94f);
    }

    float ResolveGadgetCooldown(string gadgetItemId)
    {
        if (string.Equals(gadgetItemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return GadgetMinePlacementCooldown;

        return 0f;
    }

    int ResolveGadgetMaxCharges(string gadgetItemId, int equippedCount)
    {
        equippedCount = Mathf.Max(0, equippedCount);
        if (equippedCount <= 0)
            return 0;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return GadgetMineDefaultCharges * equippedCount;

        if (string.Equals(gadgetItemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return BatteryDefaultCharges * equippedCount;

        return 0;
    }

    [PunRPC]
    void RequestAuthoritativeGadgetUse(string itemId, PhotonMessageInfo messageInfo)
    {
        if (!PhotonNetwork.IsMasterClient || !IsGameStarted() || string.IsNullOrWhiteSpace(itemId))
            return;

        if (photonView == null || photonView.Owner == null || messageInfo.Sender == null || messageInfo.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        Photon.Realtime.Player owner = photonView.Owner;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipmentSlots = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        List<string> orderedItems = new List<string>();
        Dictionary<string, int> gadgetCounts = CollectEquippedGadgetCounts(equipmentSlots, shipSkinIndex, orderedItems);
        int equippedCount = gadgetCounts.TryGetValue(itemId, out int count) ? count : 0;
        int maxCharges = ResolveGadgetMaxCharges(itemId, equippedCount);
        if (maxCharges <= 0)
            return;

        int remainingCharges = GetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, maxCharges);
        if (remainingCharges <= 0)
            return;

        if (!TryExecuteAuthoritativeGadgetUse(itemId))
            return;

        SetAuthoritativeRemainingChargesOnMaster(owner.ActorNumber, itemId, remainingCharges - 1, maxCharges);
    }

    bool TryExecuteAuthoritativeGadgetUse(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.GadgetMineId, StringComparison.Ordinal))
            return TryDeployGadgetMine();

        if (string.Equals(itemId, InventoryItemCatalog.BatteryId, StringComparison.Ordinal))
            return TryActivateBatteryCharge();

        return false;
    }

    int GetAuthoritativeRemainingChargesOnMaster(int actorNumber, string itemId, int maxCharges)
    {
        if (actorNumber <= 0 || string.IsNullOrWhiteSpace(itemId))
            return 0;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(GetAuthoritativeGadgetChargeStateRaw());
        if (chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) &&
            actorCharges != null &&
            actorCharges.TryGetValue(itemId, out int remainingCharges))
        {
            return Mathf.Clamp(remainingCharges, 0, maxCharges);
        }

        return maxCharges;
    }

    void SetAuthoritativeRemainingChargesOnMaster(int actorNumber, string itemId, int remainingCharges, int maxCharges)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || actorNumber <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(GetAuthoritativeGadgetChargeStateRaw());
        if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null)
        {
            actorCharges = new Dictionary<string, int>(StringComparer.Ordinal);
            chargesByActor[actorNumber] = actorCharges;
        }

        if (remainingCharges >= maxCharges)
            actorCharges.Remove(itemId);
        else
            actorCharges[itemId] = Mathf.Max(0, remainingCharges);

        if (actorCharges.Count == 0)
            chargesByActor.Remove(actorNumber);

        string serializedState = SerializeAuthoritativeGadgetChargeState(chargesByActor);
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.GadgetChargesStateKey] = serializedState
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        lastAuthoritativeGadgetChargeStateRaw = null;
    }

    static void ParseAuthoritativeGadgetChargesForActor(string serializedState, int actorNumber, Dictionary<string, int> destination)
    {
        if (destination == null)
            return;

        destination.Clear();
        if (string.IsNullOrWhiteSpace(serializedState) || actorNumber <= 0)
            return;

        Dictionary<int, Dictionary<string, int>> chargesByActor = DeserializeAuthoritativeGadgetChargeState(serializedState);
        if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null)
            return;

        foreach (KeyValuePair<string, int> pair in actorCharges)
            destination[pair.Key] = Mathf.Max(0, pair.Value);
    }

    static Dictionary<int, Dictionary<string, int>> DeserializeAuthoritativeGadgetChargeState(string serializedState)
    {
        Dictionary<int, Dictionary<string, int>> chargesByActor = new Dictionary<int, Dictionary<string, int>>();
        if (string.IsNullOrWhiteSpace(serializedState))
            return chargesByActor;

        string[] actorEntries = serializedState.Split(';');
        for (int i = 0; i < actorEntries.Length; i++)
        {
            string actorEntry = actorEntries[i];
            if (string.IsNullOrWhiteSpace(actorEntry))
                continue;

            int separatorIndex = actorEntry.IndexOf('#');
            if (separatorIndex <= 0)
                continue;

            string actorRaw = actorEntry.Substring(0, separatorIndex);
            if (!int.TryParse(actorRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int actorNumber) || actorNumber <= 0)
                continue;

            string itemsRaw = actorEntry.Substring(separatorIndex + 1);
            if (string.IsNullOrWhiteSpace(itemsRaw))
                continue;

            Dictionary<string, int> actorCharges = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] itemEntries = itemsRaw.Split(',');
            for (int itemIndex = 0; itemIndex < itemEntries.Length; itemIndex++)
            {
                string itemEntry = itemEntries[itemIndex];
                if (string.IsNullOrWhiteSpace(itemEntry))
                    continue;

                int itemSeparatorIndex = itemEntry.IndexOf('=');
                if (itemSeparatorIndex <= 0 || itemSeparatorIndex >= itemEntry.Length - 1)
                    continue;

                string itemId = itemEntry.Substring(0, itemSeparatorIndex);
                string remainingRaw = itemEntry.Substring(itemSeparatorIndex + 1);
                if (string.IsNullOrWhiteSpace(itemId) ||
                    !int.TryParse(remainingRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int remainingCharges))
                {
                    continue;
                }

                actorCharges[itemId] = Mathf.Max(0, remainingCharges);
            }

            if (actorCharges.Count > 0)
                chargesByActor[actorNumber] = actorCharges;
        }

        return chargesByActor;
    }

    static string SerializeAuthoritativeGadgetChargeState(Dictionary<int, Dictionary<string, int>> chargesByActor)
    {
        if (chargesByActor == null || chargesByActor.Count == 0)
            return string.Empty;

        List<int> actorNumbers = new List<int>(chargesByActor.Keys);
        actorNumbers.Sort();

        StringBuilder builder = new StringBuilder();
        for (int actorIndex = 0; actorIndex < actorNumbers.Count; actorIndex++)
        {
            int actorNumber = actorNumbers[actorIndex];
            if (!chargesByActor.TryGetValue(actorNumber, out Dictionary<string, int> actorCharges) || actorCharges == null || actorCharges.Count == 0)
                continue;

            List<string> itemIds = new List<string>(actorCharges.Keys);
            itemIds.Sort(StringComparer.Ordinal);

            StringBuilder actorBuilder = new StringBuilder();
            for (int itemIndex = 0; itemIndex < itemIds.Count; itemIndex++)
            {
                string itemId = itemIds[itemIndex];
                if (string.IsNullOrWhiteSpace(itemId) || !actorCharges.TryGetValue(itemId, out int remainingCharges))
                    continue;

                if (actorBuilder.Length > 0)
                    actorBuilder.Append(',');

                actorBuilder.Append(itemId);
                actorBuilder.Append('=');
                actorBuilder.Append(Mathf.Max(0, remainingCharges).ToString(CultureInfo.InvariantCulture));
            }

            if (actorBuilder.Length == 0)
                continue;

            if (builder.Length > 0)
                builder.Append(';');

            builder.Append(actorNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append('#');
            builder.Append(actorBuilder);
        }

        return builder.ToString();
    }

    void EnsureBotBootstrap()
    {
        if (!EnemyBot.IsBotInstantiationData(photonView != null ? photonView.InstantiationData : null))
            return;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot == null)
            bot = gameObject.AddComponent<EnemyBot>();

        bot.InitializeFromPhotonData();
    }
}

[RequireComponent(typeof(PlayerShooting))]
public class ReloadButtonUI : MonoBehaviourPun
{
    const string ReloadButtonName = "ReloadButton";

    PlayerShooting shooting;
    GameObject buttonObject;
    Button reloadButton;
    Image backgroundImage;
    TextMeshProUGUI buttonText;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        CreateButton();
        RefreshState();
    }

    void Update()
    {
        EnsureButton();
        RefreshState();
    }

    void OnDestroy()
    {
        if (buttonObject != null)
        {
            Destroy(buttonObject);
        }
    }

    void CreateButton()
    {
        GameObject existing = GameObject.Find(ReloadButtonName);
        if (existing != null)
        {
            Destroy(existing);
        }

        GameObject canvas = GameObject.Find("Canvas");
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (canvas == null || shootJoystickObject == null)
            return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransform joystickRect = shootJoystickObject.GetComponent<RectTransform>();
        if (canvasRect == null || joystickRect == null)
            return;

        buttonObject = new GameObject(ReloadButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = joystickRect.anchorMin;
        rect.anchorMax = joystickRect.anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = joystickRect.anchoredPosition + new Vector2(0f, 216f);
        rect.sizeDelta = new Vector2(176f, 62f);

        backgroundImage = buttonObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.23f, 0.56f, 0.9f, 0.96f);
        backgroundImage.type = Image.Type.Sliced;

        reloadButton = buttonObject.GetComponent<Button>();
        reloadButton.transition = Selectable.Transition.ColorTint;
        reloadButton.targetGraphic = backgroundImage;
        reloadButton.onClick.AddListener(HandleReloadClicked);

        GameObject textObject = new GameObject("ReloadButtonText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        buttonText = textObject.GetComponent<TextMeshProUGUI>();
        buttonText.text = "RELOAD";
        buttonText.fontSize = 26f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.textWrappingMode = TextWrappingModes.NoWrap;
        buttonText.margin = new Vector4(12f, 6f, 12f, 6f);
        buttonText.color = Color.white;

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();
        if (referenceText != null)
        {
            buttonText.font = referenceText.font;
            buttonText.fontSharedMaterial = referenceText.fontSharedMaterial;
        }
    }

    void EnsureButton()
    {
        if (!photonView.IsMine)
            return;

        if (buttonObject != null && reloadButton != null && backgroundImage != null && buttonText != null)
            return;

        CreateButton();
    }

    void RefreshState()
    {
        if (shooting == null || reloadButton == null || backgroundImage == null || buttonText == null)
            return;

        bool canReload = shooting.CanManualReload;
        reloadButton.interactable = canReload;
        backgroundImage.color = canReload
            ? new Color(0.23f, 0.56f, 0.9f, 0.96f)
            : new Color(0.14f, 0.18f, 0.24f, 0.78f);
        buttonText.color = canReload
            ? Color.white
            : new Color(0.82f, 0.86f, 0.91f, 0.82f);
    }

    void HandleReloadClicked()
    {
        if (shooting == null)
            return;

        shooting.TriggerManualReload();
    }
}

[RequireComponent(typeof(PlayerShooting))]
public class GadgetButtonUI : MonoBehaviourPun
{
    sealed class GadgetButtonWidget
    {
        public string ItemId;
        public GameObject Root;
        public Button Button;
        public Image Background;
        public Image Icon;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Charges;
    }

    const string GadgetButtonRootName = "GadgetButtonsRoot";
    static Sprite circularButtonSprite;

    PlayerShooting shooting;
    GameObject rootObject;
    readonly List<GadgetButtonWidget> widgets = new List<GadgetButtonWidget>();
    string lastWidgetSignature = string.Empty;

    void Start()
    {
        shooting = GetComponent<PlayerShooting>();

        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        RebuildButtonsIfNeeded();
        RefreshState();
    }

    void Update()
    {
        RebuildButtonsIfNeeded();
        RefreshState();
    }

    void OnDestroy()
    {
        DestroyAllButtons();
    }

    void RebuildButtonsIfNeeded()
    {
        if (!photonView.IsMine || shooting == null)
            return;

        IReadOnlyList<string> itemIds = shooting.ActiveGadgetItemIds;
        string signature = BuildWidgetSignature(itemIds);
        if (signature == lastWidgetSignature && rootObject != null && widgets.Count == itemIds.Count)
            return;

        DestroyAllButtons();
        CreateButtons(itemIds);
        lastWidgetSignature = signature;
    }

    void CreateButtons(IReadOnlyList<string> itemIds)
    {
        GameObject canvas = GameObject.Find("Canvas");
        GameObject shootJoystickObject = GameObject.Find("ShootJoystickBG");
        if (canvas == null || shootJoystickObject == null)
            return;

        RectTransform joystickRect = shootJoystickObject.GetComponent<RectTransform>();
        if (joystickRect == null)
            return;

        GameObject existingRoot = GameObject.Find(GadgetButtonRootName);
        if (existingRoot != null)
            Destroy(existingRoot);

        rootObject = new GameObject(GadgetButtonRootName, typeof(RectTransform));
        rootObject.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = joystickRect.anchorMin;
        rootRect.anchorMax = joystickRect.anchorMax;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = joystickRect.anchoredPosition;
        rootRect.sizeDelta = Vector2.zero;

        for (int i = 0; i < itemIds.Count; i++)
        {
            GadgetButtonWidget widget = CreateWidget(itemIds[i], i);
            if (widget != null)
                widgets.Add(widget);
        }
    }

    GadgetButtonWidget CreateWidget(string itemId, int index)
    {
        if (rootObject == null || string.IsNullOrWhiteSpace(itemId))
            return null;

        GadgetButtonWidget widget = new GadgetButtonWidget();
        widget.ItemId = itemId;
        widget.Root = new GameObject("GadgetButton_" + itemId, typeof(RectTransform), typeof(Image), typeof(Button));
        widget.Root.transform.SetParent(rootObject.transform, false);

        RectTransform rect = widget.Root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 392f + (index * 126f));
        rect.sizeDelta = new Vector2(112f, 112f);

        widget.Background = widget.Root.GetComponent<Image>();
        widget.Background.sprite = GetCircularButtonSprite();
        widget.Background.type = Image.Type.Simple;

        widget.Button = widget.Root.GetComponent<Button>();
        widget.Button.transition = Selectable.Transition.ColorTint;
        widget.Button.targetGraphic = widget.Background;
        string capturedItemId = itemId;
        widget.Button.onClick.AddListener(() => HandleGadgetClicked(capturedItemId));

        TMP_Text referenceText = FindAnyObjectByType<TMP_Text>();

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(widget.Root.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 8f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        widget.Icon = iconObject.GetComponent<Image>();
        widget.Icon.preserveAspect = true;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(widget.Root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 14f);
        labelRect.sizeDelta = new Vector2(90f, 24f);
        widget.Label = labelObject.GetComponent<TextMeshProUGUI>();
        widget.Label.fontSize = 18f;
        widget.Label.fontStyle = FontStyles.Bold;
        widget.Label.alignment = TextAlignmentOptions.Center;
        widget.Label.textWrappingMode = TextWrappingModes.NoWrap;
        if (referenceText != null)
        {
            widget.Label.font = referenceText.font;
            widget.Label.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        GameObject chargesObject = new GameObject("Charges", typeof(RectTransform), typeof(TextMeshProUGUI));
        chargesObject.transform.SetParent(widget.Root.transform, false);
        RectTransform chargesRect = chargesObject.GetComponent<RectTransform>();
        chargesRect.anchorMin = new Vector2(0.5f, 0f);
        chargesRect.anchorMax = new Vector2(0.5f, 0f);
        chargesRect.pivot = new Vector2(0.5f, 0f);
        chargesRect.anchoredPosition = new Vector2(0f, -8f);
        chargesRect.sizeDelta = new Vector2(72f, 24f);
        widget.Charges = chargesObject.GetComponent<TextMeshProUGUI>();
        widget.Charges.fontSize = 20f;
        widget.Charges.fontStyle = FontStyles.Bold;
        widget.Charges.alignment = TextAlignmentOptions.Center;
        widget.Charges.textWrappingMode = TextWrappingModes.NoWrap;
        if (referenceText != null)
        {
            widget.Charges.font = referenceText.font;
            widget.Charges.fontSharedMaterial = referenceText.fontSharedMaterial;
        }

        return widget;
    }

    void RefreshState()
    {
        if (shooting == null)
            return;

        if (rootObject != null)
            rootObject.SetActive(widgets.Count > 0);

        for (int i = 0; i < widgets.Count; i++)
        {
            GadgetButtonWidget widget = widgets[i];
            if (widget == null || widget.Root == null)
                continue;

            int remaining = shooting.GetRemainingGadgetCharges(widget.ItemId);
            int max = shooting.GetMaxGadgetCharges(widget.ItemId);
            bool canUse = shooting.CanUseGadget(widget.ItemId);
            bool depleted = max > 0 && remaining <= 0;

            widget.Icon.sprite = shooting.GetGadgetIcon(widget.ItemId);
            widget.Icon.enabled = widget.Icon.sprite != null;
            widget.Label.text = shooting.GetGadgetButtonLabel(widget.ItemId);
            widget.Charges.text = max > 0 ? remaining.ToString() : string.Empty;

            widget.Button.interactable = canUse;
            widget.Background.color = canUse
                ? shooting.GetGadgetButtonColor(widget.ItemId)
                : new Color(0.15f, 0.19f, 0.24f, 0.82f);
            widget.Label.color = canUse
                ? Color.white
                : new Color(0.82f, 0.86f, 0.91f, 0.82f);
            widget.Charges.color = canUse
                ? Color.white
                : new Color(0.78f, 0.8f, 0.82f, 0.82f);
            widget.Icon.color = depleted
                ? new Color(0.6f, 0.6f, 0.6f, 0.72f)
                : canUse
                    ? new Color(1f, 1f, 1f, 0.88f)
                    : new Color(0.82f, 0.86f, 0.91f, 0.54f);
        }
    }

    void HandleGadgetClicked(string itemId)
    {
        if (shooting == null)
            return;

        shooting.TriggerGadgetUse(itemId);
    }

    void DestroyAllButtons()
    {
        for (int i = 0; i < widgets.Count; i++)
        {
            if (widgets[i]?.Root != null)
                Destroy(widgets[i].Root);
        }

        widgets.Clear();
        lastWidgetSignature = string.Empty;

        if (rootObject != null)
        {
            Destroy(rootObject);
            rootObject = null;
        }
    }

    static string BuildWidgetSignature(IReadOnlyList<string> itemIds)
    {
        if (itemIds == null || itemIds.Count == 0)
            return string.Empty;

        return string.Join("|", itemIds);
    }

    static Sprite GetCircularButtonSprite()
    {
        if (circularButtonSprite != null)
            return circularButtonSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GadgetButtonCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        float feather = size * 0.06f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 1f - Mathf.InverseLerp(radius - feather, radius, distance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        circularButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circularButtonSprite;
    }
}
