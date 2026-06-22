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
    string BuildComplexWeaponSignature(string[] equipmentSlots, int shipSkinIndex)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(shipSkinIndex);
        builder.Append(':');
        for (int slot = 0; slot < 2; slot++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(slot, shipSkinIndex))
                continue;

            if (builder[builder.Length - 1] != ':')
                builder.Append(',');

            builder.Append(slot);
            builder.Append('=');
            builder.Append(GetWeaponIdForEquipmentSlot(equipmentSlots, slot));
        }

        builder.Append('|');
        builder.Append(WeaponAttackCatalog.GetRoomSetupSignature());
        builder.Append("|ammoDamage=");
        builder.Append(GetShipDamageAmmoSignature());
        return builder.ToString();
    }

    void BuildComplexWeaponStates(string[] equipmentSlots, int shipSkinIndex, List<ComplexWeaponRuntimeState> previousStates)
    {
        for (int slot = 0; slot < 2; slot++)
        {
            if (!ShipCatalog.IsEquipmentSlotEnabled(slot, shipSkinIndex))
                continue;

            string weaponId = GetWeaponIdForEquipmentSlot(equipmentSlots, slot);
            WeaponAttackProfile profile = WeaponAttackCatalog.GetNormalAttackByWeaponId(weaponId);
            ComplexWeaponRuntimeState previous = FindPreviousComplexWeaponState(previousStates, slot, weaponId);
            int max = GetPilotAdjustedWeaponMaxAmmo(profile);
            ComplexWeaponRuntimeState state = new ComplexWeaponRuntimeState
            {
                SlotIndex = slot,
                WeaponId = weaponId,
                Profile = profile,
                MaxAmmo = max,
                CurrentAmmo = previous != null ? Mathf.Clamp(previous.CurrentAmmo, 0, max) : max,
                AmmoReloadStartedAt = previous != null ? previous.AmmoReloadStartedAt : 0f,
                NextAmmoAt = previous != null ? previous.NextAmmoAt : 0f,
                AshEmergencyReloadPending = previous != null && previous.AshEmergencyReloadPending
            };
            complexWeaponStates.Add(state);
        }

        if (complexWeaponStates.Count == 0 && ShipCatalog.GetMainGunSlots(shipSkinIndex) > 0)
        {
            WeaponAttackProfile profile = WeaponAttackCatalog.GetNormalAttackByWeaponId(WeaponAttackCatalog.SimpleGunId);
            int max = GetPilotAdjustedWeaponMaxAmmo(profile);
            complexWeaponStates.Add(new ComplexWeaponRuntimeState
            {
                SlotIndex = 0,
                WeaponId = WeaponAttackCatalog.SimpleGunId,
                Profile = profile,
                MaxAmmo = max,
                CurrentAmmo = max
            });
        }
    }

    ComplexWeaponRuntimeState FindPreviousComplexWeaponState(List<ComplexWeaponRuntimeState> previousStates, int slot, string weaponId)
    {
        if (previousStates == null)
            return null;

        for (int i = 0; i < previousStates.Count; i++)
        {
            ComplexWeaponRuntimeState state = previousStates[i];
            if (state != null &&
                state.SlotIndex == slot &&
                string.Equals(state.WeaponId, weaponId, StringComparison.Ordinal))
            {
                return state;
            }
        }

        return null;
    }

    string GetWeaponIdForEquipmentSlot(string[] equipmentSlots, int slotIndex)
    {
        return WeaponAttackCatalog.GetWeaponIdForItem(GetEquipmentItem(equipmentSlots, slotIndex));
    }

    bool HasMainGunSlotsForCurrentShip()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        int shipSkinIndex = RoomSettings.GetPlayerShipSkin(owner, 0);
        return ShipCatalog.GetMainGunSlots(shipSkinIndex) > 0;
    }

    void ClearMainGunRuntime()
    {
        if (complexWeaponStates.Count > 0)
            complexWeaponStates.Clear();

        activeComplexWeaponProfile = null;
        activeSimpleWeaponProfile = null;
        currentAmmo = 0;
        maxAmmo = 0;
        isReloading = false;
        reloadFinishTime = 0f;
        nextComplexAttackTime = 0f;
        superCharge = 0f;
        ResetComplexPressState();
    }

    ComplexWeaponRuntimeState GetActiveComplexWeaponState()
    {
        if (complexWeaponStates.Count == 0)
            return null;

        activeComplexWeaponIndex = Mathf.Clamp(activeComplexWeaponIndex, 0, complexWeaponStates.Count - 1);
        return complexWeaponStates[activeComplexWeaponIndex];
    }

    ComplexWeaponRuntimeState GetNextComplexWeaponState()
    {
        if (complexWeaponStates.Count == 0)
            return null;

        int activeIndex = Mathf.Clamp(activeComplexWeaponIndex, 0, complexWeaponStates.Count - 1);
        int nextIndex = (activeIndex + 1) % complexWeaponStates.Count;
        return complexWeaponStates[nextIndex];
    }

    void SyncActiveComplexAmmoMirror()
    {
        ComplexWeaponRuntimeState state = GetActiveComplexWeaponState();
        if (state == null)
            return;

        activeComplexWeaponProfile = state.Profile;
        maxAmmo = Mathf.Max(1, state.MaxAmmo);
        currentAmmo = Mathf.Clamp(state.CurrentAmmo, 0, maxAmmo);
        complexAmmoReloadStartedAt = state.AmmoReloadStartedAt;
        complexNextAmmoAt = state.NextAmmoAt;
    }

    void UpdateComplexAmmoReload()
    {
        if (complexWeaponStates.Count == 0)
            SyncComplexWeaponProfile();

        for (int i = 0; i < complexWeaponStates.Count; i++)
        {
            ComplexWeaponRuntimeState state = complexWeaponStates[i];
            if (state == null || state.Profile == null)
                continue;

            if (state.CurrentAmmo >= state.MaxAmmo)
            {
                state.NextAmmoAt = 0f;
                state.AmmoReloadStartedAt = 0f;
                continue;
            }

            float reloadTime = GetAdjustedAmmoReloadTime(state.Profile, state.AshEmergencyReloadPending);
            if (reloadTime <= 0f)
                continue;

            if (state.NextAmmoAt <= 0f)
            {
                state.AmmoReloadStartedAt = Time.time;
                state.NextAmmoAt = Time.time + reloadTime;
            }
            else
            {
                ShortenActiveAmmoReloadIfFaster(state, reloadTime);
            }

            while (state.CurrentAmmo < state.MaxAmmo && state.NextAmmoAt > 0f && Time.time >= state.NextAmmoAt)
            {
                state.CurrentAmmo++;
                state.AshEmergencyReloadPending = false;
                if (state.CurrentAmmo >= state.MaxAmmo)
                {
                    state.NextAmmoAt = 0f;
                    state.AmmoReloadStartedAt = 0f;
                    break;
                }

                reloadTime = GetAdjustedAmmoReloadTime(state.Profile, false);
                state.AmmoReloadStartedAt = state.NextAmmoAt;
                state.NextAmmoAt += reloadTime;
            }
        }

        SyncActiveComplexAmmoMirror();
    }

    void ShortenActiveAmmoReloadIfFaster(ComplexWeaponRuntimeState state, float desiredReloadTime)
    {
        if (state == null || desiredReloadTime <= 0f || state.NextAmmoAt <= 0f)
            return;

        float scheduledDuration = state.NextAmmoAt - state.AmmoReloadStartedAt;
        if (scheduledDuration <= 0.001f || desiredReloadTime >= scheduledDuration - 0.01f)
            return;

        float progress = Mathf.Clamp01((Time.time - state.AmmoReloadStartedAt) / scheduledDuration);
        state.AmmoReloadStartedAt = Time.time - (desiredReloadTime * progress);
        state.NextAmmoAt = state.AmmoReloadStartedAt + desiredReloadTime;
    }

    float GetComplexAmmoReloadProgress()
    {
        ComplexWeaponRuntimeState state = GetActiveComplexWeaponState();
        if (!IsComplexShootingActive || state == null || state.CurrentAmmo >= state.MaxAmmo || state.NextAmmoAt <= 0f)
            return 0f;

        float duration = Mathf.Max(0.001f, state.NextAmmoAt - state.AmmoReloadStartedAt);
        return Mathf.Clamp01((Time.time - state.AmmoReloadStartedAt) / duration);
    }

    void UpdateSuperCharge()
    {
        superCharge = Mathf.Clamp01(superCharge + (Time.deltaTime / SuperChargeTimeSeconds) * GetSuperChargeGainMultiplier());
    }
}
