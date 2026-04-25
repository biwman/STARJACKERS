using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class HideInNebulaTarget : MonoBehaviour
{
    const float NebulaStateValidationInterval = 0.35f;

    static readonly HashSet<int> LocalPlayerNebulas = new HashSet<int>();

    Renderer[] renderers;
    PhotonView photonView;
    PlayerHealth playerHealth;
    Coroutine damageRoutine;
    Dictionary<int, bool> hiddenNebulaStates = new Dictionary<int, bool>();
    HashSet<int> damagingNebulas = new HashSet<int>();
    float nextNebulaStateValidationTime;
    public bool IsHiddenForOthers => HasHiddenNebula();

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        playerHealth = GetComponent<PlayerHealth>();
        CacheRenderers();
    }

    void Update()
    {
        if (Time.time < nextNebulaStateValidationTime)
            return;

        nextNebulaStateValidationTime = Time.time + NebulaStateValidationInterval;
        ValidateNebulaContacts();
    }

    public void UpdateNebulaState(int nebulaId, bool shouldHide, bool shouldDamage)
    {
        CacheRenderers();
        hiddenNebulaStates[nebulaId] = shouldHide;
        if (shouldDamage)
            damagingNebulas.Add(nebulaId);
        else
            damagingNebulas.Remove(nebulaId);

        RefreshLocalNebulaCache();
        ApplyVisibility();

        if (playerHealth != null && photonView != null && photonView.IsMine && damageRoutine == null && damagingNebulas.Count > 0)
        {
            damageRoutine = StartCoroutine(ApplyNebulaDamage());
        }
    }

    public void RemoveNebula(int nebulaId)
    {
        hiddenNebulaStates.Remove(nebulaId);
        damagingNebulas.Remove(nebulaId);
        RefreshLocalNebulaCache();
        ApplyVisibility();

        if (damagingNebulas.Count == 0 && damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
            damageRoutine = null;
        }
    }

    public static void RemoveNebulaFromAll(int nebulaId)
    {
        HideInNebulaTarget[] targets = FindObjectsByType<HideInNebulaTarget>(FindObjectsInactive.Exclude);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].RemoveNebula(nebulaId);
        }
    }

    void CacheRenderers()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    void ApplyVisibility()
    {
        bool shouldHide = HasHiddenNebula();
        bool keepLocallyVisible = IsLocalHumanControlledCharacter();
        bool shouldBeVisible = !shouldHide || keepLocallyVisible || SharesNebulaWithLocalPlayer();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = shouldBeVisible;
            }
        }
    }

    bool IsLocalHumanControlledCharacter()
    {
        return photonView != null &&
               photonView.IsMine &&
               playerHealth != null &&
               !playerHealth.IsBotControlled &&
               !playerHealth.IsWreck;
    }

    bool HasHiddenNebula()
    {
        foreach (bool value in hiddenNebulaStates.Values)
        {
            if (value)
                return true;
        }

        return false;
    }

    void RefreshLocalNebulaCache()
    {
        if (!IsLocalHumanControlledCharacter())
            return;

        LocalPlayerNebulas.Clear();
        foreach (KeyValuePair<int, bool> state in hiddenNebulaStates)
        {
            if (state.Value)
                LocalPlayerNebulas.Add(state.Key);
        }

        RefreshAllTargetVisibility();
    }

    static void RefreshAllTargetVisibility()
    {
        HideInNebulaTarget[] targets = FindObjectsByType<HideInNebulaTarget>(FindObjectsInactive.Exclude);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].ApplyVisibility();
        }
    }

    void ValidateNebulaContacts()
    {
        if (hiddenNebulaStates.Count == 0 && damagingNebulas.Count == 0)
            return;

        List<int> nebulaIds = new List<int>(hiddenNebulaStates.Keys);
        foreach (int damagingId in damagingNebulas)
        {
            if (!nebulaIds.Contains(damagingId))
                nebulaIds.Add(damagingId);
        }

        bool changed = false;
        for (int i = 0; i < nebulaIds.Count; i++)
        {
            int nebulaId = nebulaIds[i];
            if (!NebulaField.TryGetField(nebulaId, out NebulaField field) || !field.ContainsTarget(this))
            {
                changed |= hiddenNebulaStates.Remove(nebulaId);
                changed |= damagingNebulas.Remove(nebulaId);
                continue;
            }

            bool shouldHide = field.ShouldHide(this);
            bool shouldDamage = field.ShouldDamage(this);

            if (!hiddenNebulaStates.TryGetValue(nebulaId, out bool previousHide) || previousHide != shouldHide)
            {
                hiddenNebulaStates[nebulaId] = shouldHide;
                changed = true;
            }

            bool hadDamage = damagingNebulas.Contains(nebulaId);
            if (shouldDamage && !hadDamage)
            {
                damagingNebulas.Add(nebulaId);
                changed = true;
            }
            else if (!shouldDamage && hadDamage)
            {
                damagingNebulas.Remove(nebulaId);
                changed = true;
            }
        }

        if (!changed)
            return;

        RefreshLocalNebulaCache();
        ApplyVisibility();

        if (damagingNebulas.Count == 0 && damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
            damageRoutine = null;
        }
        else if (playerHealth != null && photonView != null && photonView.IsMine && damageRoutine == null && damagingNebulas.Count > 0)
        {
            damageRoutine = StartCoroutine(ApplyNebulaDamage());
        }
    }

    bool SharesNebulaWithLocalPlayer()
    {
        if (LocalPlayerNebulas.Count == 0)
            return false;

        foreach (KeyValuePair<int, bool> state in hiddenNebulaStates)
        {
            if (state.Value && LocalPlayerNebulas.Contains(state.Key))
                return true;
        }

        return false;
    }

    public bool IsHiddenFromLocalPlayer()
    {
        return HasHiddenNebula() && !SharesNebulaWithLocalPlayer();
    }

    public bool IsHiddenFromObserver(HideInNebulaTarget observer)
    {
        return HasHiddenNebula() && !SharesNebulaWith(observer);
    }

    public bool SharesNebulaWith(HideInNebulaTarget other)
    {
        if (other == null)
            return false;

        foreach (KeyValuePair<int, bool> state in hiddenNebulaStates)
        {
            if (!state.Value)
                continue;

            if (other.hiddenNebulaStates.TryGetValue(state.Key, out bool otherHidden) && otherHidden)
                return true;
        }

        return false;
    }

    IEnumerator ApplyNebulaDamage()
    {
        while (damagingNebulas.Count > 0)
        {
            yield return new WaitForSeconds(2f);

            if (damagingNebulas.Count <= 0)
                break;

            if (playerHealth == null || photonView == null || !photonView.IsMine)
                continue;

            if (PhotonNetwork.CurrentRoom == null ||
                !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object value) ||
                value is not bool started ||
                !started)
            {
                continue;
            }

            playerHealth.photonView.RPC("TakeEnvironmentalDamage", RpcTarget.MasterClient, 1);
        }

        damageRoutine = null;
    }
}
