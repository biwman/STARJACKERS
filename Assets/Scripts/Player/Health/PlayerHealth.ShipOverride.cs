using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHealth : MonoBehaviourPun
{
    [PunRPC]
    public void BeginAvengerShipOverrideRpc(int shipSkinIndex, float launchX, float launchY, float rotationZ)
    {
        if (IsBotControlled || IsNeutralRiderControlled || IsAstronautControlled)
            return;

        int safeSkinIndex = Mathf.Clamp(shipSkinIndex, ShipCatalog.ExplorerBasicSkinIndex, ShipCatalog.MaxShipSkinIndex);
        transform.position = new Vector3(launchX, launchY, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        IsWreck = false;
        isEvacuationAnimating = false;
        destroyRequested = false;
        deathHandled = false;
        astronautSpawnedAfterDestruction = false;
        hasPendingEvacuationSummary = false;
        pendingEvacuationOutcome = "extracted";
        currentHP = Mathf.Max(1, ShipCatalog.GetBaseHp(safeSkinIndex));
        currentShield = Mathf.Max(0, ShipCatalog.GetBaseShield(safeSkinIndex));
        maxHP = currentHP;
        maxShield = currentShield;
        phaseShieldTriggered = false;
        jakeEmergencyRegenerationUsed = false;
        if (jakeEmergencyRegenerationRoutine != null)
        {
            StopCoroutine(jakeEmergencyRegenerationRoutine);
            jakeEmergencyRegenerationRoutine = null;
        }

        ShipDamageState damageState = GetComponent<ShipDamageState>();
        if (damageState == null)
            damageState = gameObject.AddComponent<ShipDamageState>();
        damageState.ClearAllLocalDamageForNewShip();

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.CancelActiveGadgetEffectsForShipLoss();

        if (GetComponent<PlayerRepairDocking>() == null)
            gameObject.AddComponent<PlayerRepairDocking>();
        if (GetComponent<PilotActiveAbilityController>() == null)
            gameObject.AddComponent<PilotActiveAbilityController>();
        if (GetComponent<RoundChatCommandUI>() == null)
            gameObject.AddComponent<RoundChatCommandUI>();
        LowHpHullSparksVfx.AttachIfNeeded(gameObject);

        if (photonView.IsMine)
        {
            PhotonNetwork.LocalPlayer.TagObject = gameObject;
            if (PlayerProfileService.HasInstance)
                PlayerProfileService.Instance.SetActiveRoundShipSkin(safeSkinIndex);

            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = true;
            if (shooting != null)
                shooting.enabled = true;

            if (hpBar != null)
            {
                hpBar.maxValue = maxHP;
                hpBar.value = currentHP;
            }
        }

        BeginSpawnInvulnerability(SpawnInvulnerabilityDuration);
        ApplyImmediateShipVisual(safeSkinIndex);
        StartCoroutine(RefreshShipVisualAfterAvengerOverride());
    }

    void ApplyImmediateShipVisual(int shipSkinIndex)
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        Sprite sprite = RuntimeSpriteUtility.LoadSprite(
            ShipCatalog.GetShipSkinResourcePath(shipSkinIndex),
            ShipCatalog.GetShipSkinEditorResourcePath(shipSkinIndex));
        if (sprite == null)
            return;

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = GameVisualTheme.PlayerSortingOrder;
        RuntimeSpriteUtility.FitRenderer(renderer, GameVisualTheme.PlayerTargetSize);
    }

    IEnumerator RefreshShipVisualAfterAvengerOverride()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.08f);
            GameVisualTheme.ApplyPlayerVisual(this);
        }

        GameVisualTheme.RequestRuntimeRefresh(this);
    }

    public bool TryRestoreShieldAuthority(float amount, bool playFullPowerAudio)
    {
        int shieldCap = GetRepairableShieldCap();
        if (!PhotonNetwork.IsMasterClient || IsWreck || isEvacuationAnimating || maxShield <= 0 || shieldCap <= 0 || currentShield <= 0 || currentShield >= shieldCap)
            return false;

        int previousShield = currentShield;
        currentShield = Mathf.Min(shieldCap, currentShield + Mathf.Max(1, Mathf.RoundToInt(amount)));
        photonView.RPC(nameof(SyncVitals), RpcTarget.All, currentHP, currentShield);

        if (playFullPowerAudio && previousShield < shieldCap && currentShield >= shieldCap)
            photonView.RPC(nameof(PlayShieldFullPowerAudio), RpcTarget.All);

        return currentShield > previousShield;
    }
}
