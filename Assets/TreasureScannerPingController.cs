using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TreasureScannerPingController : MonoBehaviourPun
{
    const float NearDistance = 2.4f;
    const float StartupDelay = 1.2f;
    const float MinPingInterval = 0.42f;
    const float MaxPingInterval = 8.2f;
    const float MinVolume = 0.58f;
    const float MaxVolume = 0.92f;
    const float MinPitch = 0.92f;
    const float MaxPitch = 1.18f;

    AudioSource pingSource;
    float nextPingTime;
    bool trackingHiddenTreasure;

    void Start()
    {
        nextPingTime = Time.time + StartupDelay;
        EnsureAudioSource();
    }

    void OnDisable()
    {
        trackingHiddenTreasure = false;
        nextPingTime = Time.time + StartupDelay;
        if (pingSource != null)
        {
            pingSource.Stop();
            pingSource.pitch = 1f;
        }
    }

    void Update()
    {
        if (!CanRun() ||
            !IsTreasureScannerEquipped() ||
            !ObstacleSpawner.TryGetHiddenTreasureWorldPosition(out Vector2 hiddenTreasurePosition))
        {
            ResetPingCadence();
            return;
        }

        float distance = Vector2.Distance(transform.position, hiddenTreasurePosition);
        float proximity = ResolveProximity(distance);
        float interval = ResolvePingInterval(proximity);
        if (!trackingHiddenTreasure)
        {
            trackingHiddenTreasure = true;
            nextPingTime = Time.time + Mathf.Min(StartupDelay, interval);
        }
        else if (nextPingTime - Time.time > interval)
        {
            nextPingTime = Time.time + interval;
        }

        if (Time.time < nextPingTime)
            return;

        PlayPing(proximity);
        nextPingTime = Time.time + interval;
    }

    bool CanRun()
    {
        if (photonView != null && !photonView.IsMine)
            return false;

        if (GetComponent<EnemyBot>() != null ||
            NeutralRiderController.IsNeutralRider(gameObject) ||
            GetComponent<AstronautSurvivor>() != null)
        {
            return false;
        }

        PlayerHealth health = GetComponent<PlayerHealth>();
        return health == null ||
               (health.isActiveAndEnabled &&
                !health.IsWreck &&
                !health.IsEvacuationAnimating &&
                !health.IsAstronautControlled &&
                health.CurrentHP > 0);
    }

    bool IsTreasureScannerEquipped()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        string[] equipment = PlayerProfileService.GetPlayerEquipmentSlots(owner);
        return InventoryItemCatalog.HasEquippedItem(equipment, shipSkinIndex, InventoryItemCatalog.TreasureScannerId);
    }

    float ResolveProximity(float distance)
    {
        Vector2 mapSize = RoomSettings.GetGameplayMapDimensions();
        float farDistance = Mathf.Max(12f, mapSize.magnitude * 0.45f);
        return Mathf.Clamp01(Mathf.InverseLerp(farDistance, NearDistance, distance));
    }

    float ResolvePingInterval(float proximity)
    {
        float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(proximity));
        return Mathf.Lerp(MaxPingInterval, MinPingInterval, eased);
    }

    void PlayPing(float proximity)
    {
        EnsureAudioSource();
        AudioClip clip = AudioManager.Instance.TreasureScannerPingClip;
        if (clip == null || pingSource == null)
            return;

        float safeProximity = Mathf.Clamp01(proximity);
        pingSource.pitch = Mathf.Lerp(MinPitch, MaxPitch, safeProximity);
        pingSource.PlayOneShot(clip, Mathf.Lerp(MinVolume, MaxVolume, safeProximity));
    }

    void ResetPingCadence()
    {
        trackingHiddenTreasure = false;
        nextPingTime = Time.time + StartupDelay;
    }

    void EnsureAudioSource()
    {
        if (pingSource != null)
            return;

        pingSource = gameObject.AddComponent<AudioSource>();
        pingSource.loop = false;
        pingSource.playOnAwake = false;
        pingSource.spatialBlend = 0f;
        pingSource.dopplerLevel = 0f;
        pingSource.volume = 0.9f;
    }
}
