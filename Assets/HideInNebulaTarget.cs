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
    Dictionary<int, int> damagingNebulas = new Dictionary<int, int>();
    Dictionary<int, float> slowingNebulas = new Dictionary<int, float>();
    HashSet<int> fireNebulas = new HashSet<int>();
    float nextNebulaStateValidationTime;
    public bool IsHiddenForOthers => HasHiddenNebula();
    public float CurrentNebulaSpeedMultiplier => GetCurrentSpeedMultiplier();
    public bool IsInsideFireNebula => fireNebulas.Count > 0;

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
        UpdateNebulaState(nebulaId, shouldHide, shouldDamage, 1, 1f, false);
    }

    public void UpdateNebulaState(int nebulaId, bool shouldHide, bool shouldDamage, int damagePerTick, float speedMultiplier, bool isFireNebula)
    {
        bool immuneToHarm = IsNebulaHarmImmuneBot();
        shouldDamage &= !immuneToHarm;
        speedMultiplier = immuneToHarm ? 1f : Mathf.Clamp(speedMultiplier, 0.05f, 1f);
        CacheRenderers();
        hiddenNebulaStates[nebulaId] = shouldHide;
        if (isFireNebula && shouldDamage)
            fireNebulas.Add(nebulaId);
        else
            fireNebulas.Remove(nebulaId);

        if (shouldDamage)
            damagingNebulas[nebulaId] = Mathf.Max(1, damagePerTick);
        else
            damagingNebulas.Remove(nebulaId);

        if (speedMultiplier < 0.999f)
            slowingNebulas[nebulaId] = speedMultiplier;
        else
            slowingNebulas.Remove(nebulaId);

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
        slowingNebulas.Remove(nebulaId);
        fireNebulas.Remove(nebulaId);
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
        if (hiddenNebulaStates.Count == 0 && damagingNebulas.Count == 0 && slowingNebulas.Count == 0 && fireNebulas.Count == 0)
            return;

        List<int> nebulaIds = new List<int>(hiddenNebulaStates.Keys);
        foreach (int damagingId in damagingNebulas.Keys)
        {
            if (!nebulaIds.Contains(damagingId))
                nebulaIds.Add(damagingId);
        }
        foreach (int slowingId in slowingNebulas.Keys)
        {
            if (!nebulaIds.Contains(slowingId))
                nebulaIds.Add(slowingId);
        }
        foreach (int fireId in fireNebulas)
        {
            if (!nebulaIds.Contains(fireId))
                nebulaIds.Add(fireId);
        }

        bool changed = false;
        for (int i = 0; i < nebulaIds.Count; i++)
        {
            int nebulaId = nebulaIds[i];
            if (!NebulaField.TryGetField(nebulaId, out NebulaField field) || !field.ContainsTarget(this))
            {
                changed |= hiddenNebulaStates.Remove(nebulaId);
                changed |= damagingNebulas.Remove(nebulaId);
                changed |= slowingNebulas.Remove(nebulaId);
                changed |= fireNebulas.Remove(nebulaId);
                continue;
            }

            bool shouldHide = field.ShouldHide(this);
            bool immuneToHarm = IsNebulaHarmImmuneBot();
            bool shouldDamage = field.ShouldDamage(this) && !immuneToHarm;
            float speedMultiplier = immuneToHarm ? 1f : field.GetSpeedMultiplierForTarget(this);
            bool isFireNebula = field.FieldKind == NebulaFieldKind.Fire;

            if (!hiddenNebulaStates.TryGetValue(nebulaId, out bool previousHide) || previousHide != shouldHide)
            {
                hiddenNebulaStates[nebulaId] = shouldHide;
                changed = true;
            }

            bool shouldShowFireNebulaEffect = isFireNebula && shouldDamage;
            bool hadFire = fireNebulas.Contains(nebulaId);
            if (shouldShowFireNebulaEffect && !hadFire)
            {
                fireNebulas.Add(nebulaId);
                changed = true;
            }
            else if (!shouldShowFireNebulaEffect && hadFire)
            {
                fireNebulas.Remove(nebulaId);
                changed = true;
            }

            bool hadDamage = damagingNebulas.ContainsKey(nebulaId);
            int damagePerTick = Mathf.Max(1, field.DamagePerTick);
            if (shouldDamage && (!hadDamage || damagingNebulas[nebulaId] != damagePerTick))
            {
                damagingNebulas[nebulaId] = damagePerTick;
                changed = true;
            }
            else if (!shouldDamage && hadDamage)
            {
                damagingNebulas.Remove(nebulaId);
                changed = true;
            }

            bool hadSlow = slowingNebulas.ContainsKey(nebulaId);
            if (speedMultiplier < 0.999f && (!hadSlow || !Mathf.Approximately(slowingNebulas[nebulaId], speedMultiplier)))
            {
                slowingNebulas[nebulaId] = Mathf.Clamp(speedMultiplier, 0.05f, 1f);
                changed = true;
            }
            else if (speedMultiplier >= 0.999f && hadSlow)
            {
                slowingNebulas.Remove(nebulaId);
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

            int damage = GetCurrentDamagePerTick();
            if (damage > 0)
                playerHealth.photonView.RPC("TakeNebulaDamage", RpcTarget.MasterClient, damage);
        }

        damageRoutine = null;
    }

    int GetCurrentDamagePerTick()
    {
        int damage = 0;
        foreach (int value in damagingNebulas.Values)
        {
            if (value > damage)
                damage = value;
        }

        return damage;
    }

    float GetCurrentSpeedMultiplier()
    {
        float multiplier = 1f;
        foreach (float value in slowingNebulas.Values)
        {
            multiplier = Mathf.Min(multiplier, Mathf.Clamp(value, 0.05f, 1f));
        }

        return multiplier;
    }

    bool IsNebulaHarmImmuneBot()
    {
        EnemyBot bot = GetComponent<EnemyBot>();
        return bot != null && (bot.Kind == EnemyBotKind.Mothership || bot.Kind == EnemyBotKind.PirateBase);
    }
}
