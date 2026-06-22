using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public partial class PlayerShooting
{
    static bool IsWeaponProfile(WeaponAttackProfile profile, string weaponId)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(weaponId))
            return false;

        return string.Equals(profile.Id, weaponId, StringComparison.Ordinal) ||
               profile.Id.StartsWith(weaponId + "_", StringComparison.Ordinal);
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

        if (IsHoldGadget(itemId))
        {
            BeginGadgetUse(itemId);
            return;
        }

        if (gadgetStates.TryGetValue(itemId, out GadgetRuntimeState state) && state != null && state.Cooldown > 0f)
            state.NextUseTime = Time.time + state.Cooldown;

        if (ShouldDebounceInstantDeploy(itemId))
            localInstantDeployPendingUntil[itemId] = Time.time + InstantDeployLocalPendingSeconds;

        photonView.RPC(nameof(RequestAuthoritativeGadgetUse), RpcTarget.MasterClient, itemId);
    }

    public void BeginGadgetUse(string itemId)
    {
        if (!CanUseGadget(itemId) || string.IsNullOrWhiteSpace(itemId))
            return;

        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
        {
            photonView.RPC(nameof(RequestStartTractorBeam), RpcTarget.MasterClient, itemId);
            return;
        }

        TriggerGadgetUse(itemId);
    }

    public void EndGadgetUse(string itemId)
    {
        if (string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal))
            photonView.RPC(nameof(RequestStopTractorBeam), RpcTarget.MasterClient, itemId);
    }

    public void CancelActiveGadgetEffectsForShipLoss()
    {
        ResetComplexPressState();
        HideAimMarker();
        CancelCloakDeviceForLocalOwner();

        if (PhotonNetwork.IsMasterClient)
        {
            StopAuthoritativeTractorBeam(true);
            StopAuthoritativeHackingDevice(true);
            StopAuthoritativeLootHookWindup();
        }
        else if (photonView != null && photonView.IsMine)
        {
            photonView.RPC(nameof(RequestStopTractorBeam), RpcTarget.MasterClient, InventoryItemCatalog.TractorBeamId);
            photonView.RPC(nameof(RequestCancelHackingDevice), RpcTarget.MasterClient);
        }
    }

    public bool IsHoldGadget(string itemId)
    {
        return string.Equals(itemId, InventoryItemCatalog.TractorBeamId, StringComparison.Ordinal);
    }

    public void ConfigureWeaponProfile(
        float configuredFireRate,
        int configuredMaxAmmo,
        float configuredReloadDuration,
        int configuredBulletDamage,
        float configuredBulletScaleMultiplier,
        Color configuredBulletColor,
        float configuredMuzzleOffsetDistance,
        bool configuredInfiniteAmmo,
        float configuredBulletSpeed = -1f,
        string configuredShotSoundId = "",
        float configuredRangeMultiplier = -1f,
        string configuredHitEffectId = "",
        float configuredFlightTime = 10f,
        WeaponDamageType configuredDamageType = WeaponDamageType.Laser,
        WeaponDeliveryMethod configuredDeliveryMethod = WeaponDeliveryMethod.DirectProjectile,
        WeaponDeliveryFlags configuredDeliveryFlags = WeaponDeliveryFlags.None)
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

        simpleUsesDamageProfile = !string.IsNullOrWhiteSpace(configuredHitEffectId);
        simpleShieldDamage = bulletDamage;
        simpleHpDamage = bulletDamage;
        simplePierces = false;
        simpleAreaDamageRadius = 0f;
        simpleHitEffectId = configuredHitEffectId ?? string.Empty;
        simpleFlightTime = Mathf.Clamp(configuredFlightTime, 0.2f, 30f);
        simpleDamageType = configuredDamageType;
        simpleDeliveryMethod = configuredDeliveryMethod;
        simpleDeliveryFlags = configuredDeliveryFlags;

        isReloading = false;
        reloadFinishTime = 0f;
        currentAmmo = maxAmmo;
    }

    [PunRPC]
    void PlayLaserSfx()
    {
        PlayShotSfx(shotSoundId ?? string.Empty);
    }

    [PunRPC]
    void PlayShotSfx(string soundId)
    {
        if (soundId == "shoot_small")
        {
            AudioManager.Instance.PlayShootSmallAt(transform.position);
            return;
        }

        if (soundId == "artillery")
        {
            AudioManager.Instance.PlayArtilleryGunAt(transform.position);
            return;
        }

        if (soundId == "gatling")
        {
            AudioManager.Instance.PlayGatlingGunAt(transform.position);
            return;
        }

        if (soundId == "corsair")
        {
            AudioManager.Instance.PlayCorsairLaserAt(transform.position);
            return;
        }

        if (soundId == "lazer1")
        {
            AudioManager.Instance.PlayLazer1At(transform.position);
            return;
        }

        if (soundId == "lazer2")
        {
            AudioManager.Instance.PlayLazer2At(transform.position);
            return;
        }

        if (soundId == "lazer3")
        {
            AudioManager.Instance.PlayLazer3At(transform.position);
            return;
        }

        if (soundId == "pirate_fighter")
        {
            AudioManager.Instance.PlayPirateFighterShotAt(transform.position);
            return;
        }

        if (soundId == "astro_cutter")
        {
            AudioManager.Instance.PlayAstroCutterAt(transform.position);
            return;
        }

        if (soundId == "rocket")
        {
            AudioManager.Instance.PlayRocketLaunchAt(transform.position);
            return;
        }

        if (soundId == "cosmic_worm")
        {
            AudioManager.Instance.PlayCosmicWormShotAt(transform.position);
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
