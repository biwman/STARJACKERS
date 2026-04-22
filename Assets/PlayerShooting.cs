using System;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;

public class PlayerShooting : MonoBehaviourPun
{
    const float AutoAimRange = 13f;
    const float ManualAimThreshold = 0.35f;
    const float DefaultBulletRangeMultiplier = 15f;
    static readonly Color PlasmaBulletColor = new Color(0.15f, 1f, 0.28f, 1f);

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

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsReloading => isReloading;
    public bool CanManualReload => photonView.IsMine && IsGameStarted() && !isReloading && currentAmmo > 0 && currentAmmo < maxAmmo;
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

        Debug.Log("PlayerShooting START");
    }

    void Update()
    {
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
            if (nebulaState != null && nebulaState.IsHiddenForOthers)
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
        if (IsEquipmentSlotEnabled(0, shipSkinIndex) &&
            string.Equals(GetEquipmentItem(equipmentSlots, 0), InventoryItemCatalog.PlasmaGunId, StringComparison.Ordinal))
        {
            count++;
        }

        if (IsEquipmentSlotEnabled(1, shipSkinIndex) &&
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

    static bool IsEquipmentSlotEnabled(int slotIndex, int shipSkinIndex)
    {
        return slotIndex switch
        {
            0 => ShipCatalog.GetMainGunSlots(shipSkinIndex) >= 1,
            1 => ShipCatalog.GetMainGunSlots(shipSkinIndex) >= 2,
            2 => ShipCatalog.GetShieldSlots(shipSkinIndex) >= 1,
            3 => ShipCatalog.GetEngineSlots(shipSkinIndex) >= 1,
            4 => ShipCatalog.GetEngineSlots(shipSkinIndex) >= 2,
            5 => ShipCatalog.GetGadgetSlots(shipSkinIndex) >= 1,
            _ => false
        };
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

    public void ConfigureWeaponProfile(float configuredFireRate, int configuredMaxAmmo, float configuredReloadDuration, int configuredBulletDamage, float configuredBulletScaleMultiplier, Color configuredBulletColor, float configuredMuzzleOffsetDistance, bool configuredInfiniteAmmo, float configuredBulletSpeed = -1f, string configuredShotSoundId = "", float configuredRangeMultiplier = -1f)
    {
        customAmmoProfileActive = true;
        fireRate = Mathf.Max(0.05f, configuredFireRate);
        maxAmmo = Mathf.Max(1, configuredMaxAmmo);
        reloadDuration = Mathf.Max(0f, configuredReloadDuration);
        bulletDamage = Mathf.Max(1, configuredBulletDamage);
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
