using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class HideInNebulaTarget : MonoBehaviour
{
    const float NebulaStateValidationInterval = 0.35f;
    const float NormalNebulaDamageIntervalSeconds = 3f;
    const float FireNebulaDamageIntervalSeconds = 2f;
    const float ToxicNebulaDamageIntervalSeconds = 2f;

    static readonly HashSet<int> LocalPlayerNebulas = new HashSet<int>();
    static readonly HashSet<HideInNebulaTarget> ActiveTargets = new HashSet<HideInNebulaTarget>();

    Renderer[] renderers;
    Collider2D[] boundsColliders;
    SpriteRenderer[] boundsSpriteRenderers;
    PhotonView photonView;
    PlayerHealth playerHealth;
    Coroutine damageRoutine;
    Dictionary<int, bool> hiddenNebulaStates = new Dictionary<int, bool>();
    Dictionary<int, NebulaFieldKind> hiddenNebulaKinds = new Dictionary<int, NebulaFieldKind>();
    Dictionary<int, int> damagingNebulas = new Dictionary<int, int>();
    Dictionary<int, NebulaFieldKind> damagingNebulaKinds = new Dictionary<int, NebulaFieldKind>();
    Dictionary<int, float> slowingNebulas = new Dictionary<int, float>();
    HashSet<int> fireNebulas = new HashSet<int>();
    ConcealmentIndicator concealmentIndicator;
    float nextNebulaStateValidationTime;
    float activeCloakUntil;
    public bool IsHiddenForOthers => HasHiddenNebula() || HasActiveCloak();
    public bool IsConcealed => HasHiddenNebula() || HasActiveCloak();
    public NebulaFieldKind CurrentConcealmentKind => GetCurrentConcealmentKind();
    public float CurrentNebulaSpeedMultiplier => GetCurrentSpeedMultiplier();
    public bool IsInsideFireNebula => fireNebulas.Count > 0;
    public bool IsCloaked => HasActiveCloak();

    void Awake()
    {
        photonView = GetComponent<PhotonView>();
        playerHealth = GetComponent<PlayerHealth>();
        CacheRenderers();
    }

    void OnEnable()
    {
        ActiveTargets.Add(this);
    }

    void OnDisable()
    {
        ActiveTargets.Remove(this);
    }

    void Update()
    {
        if (activeCloakUntil > 0f && Time.time >= activeCloakUntil)
            ClearCloakState();

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
        UpdateNebulaState(nebulaId, shouldHide, shouldDamage, damagePerTick, speedMultiplier, isFireNebula ? NebulaFieldKind.Fire : NebulaFieldKind.Normal);
    }

    public void UpdateNebulaState(int nebulaId, bool shouldHide, bool shouldDamage, int damagePerTick, float speedMultiplier, NebulaFieldKind fieldKind)
    {
        bool immuneToHarm = IsNebulaHarmImmuneBot();
        shouldDamage &= !immuneToHarm;
        speedMultiplier = immuneToHarm ? 1f : Mathf.Clamp(speedMultiplier, 0.05f, 1f);
        CacheRenderers();

        bool changed = false;
        if (!hiddenNebulaStates.TryGetValue(nebulaId, out bool previousHide) || previousHide != shouldHide)
        {
            hiddenNebulaStates[nebulaId] = shouldHide;
            changed = true;
        }

        if (shouldHide)
        {
            if (!hiddenNebulaKinds.TryGetValue(nebulaId, out NebulaFieldKind previousKind) || previousKind != fieldKind)
            {
                hiddenNebulaKinds[nebulaId] = fieldKind;
                changed = true;
            }
        }
        else if (hiddenNebulaKinds.Remove(nebulaId))
        {
            changed = true;
        }

        bool shouldShowFireEffect = fieldKind == NebulaFieldKind.Fire && shouldDamage;
        bool hadFireEffect = fireNebulas.Contains(nebulaId);
        if (shouldShowFireEffect && !hadFireEffect)
        {
            fireNebulas.Add(nebulaId);
            changed = true;
        }
        else if (!shouldShowFireEffect && hadFireEffect)
        {
            fireNebulas.Remove(nebulaId);
            changed = true;
        }

        int normalizedDamage = Mathf.Max(1, damagePerTick);
        if (shouldDamage)
        {
            if (!damagingNebulas.TryGetValue(nebulaId, out int previousDamage) || previousDamage != normalizedDamage)
            {
                damagingNebulas[nebulaId] = normalizedDamage;
                changed = true;
            }

            if (!damagingNebulaKinds.TryGetValue(nebulaId, out NebulaFieldKind previousDamageKind) || previousDamageKind != fieldKind)
            {
                damagingNebulaKinds[nebulaId] = fieldKind;
                changed = true;
            }
        }
        else
        {
            changed |= damagingNebulas.Remove(nebulaId);
            changed |= damagingNebulaKinds.Remove(nebulaId);
        }

        if (speedMultiplier < 0.999f)
        {
            if (!slowingNebulas.TryGetValue(nebulaId, out float previousSpeedMultiplier) ||
                !Mathf.Approximately(previousSpeedMultiplier, speedMultiplier))
            {
                slowingNebulas[nebulaId] = speedMultiplier;
                changed = true;
            }
        }
        else if (slowingNebulas.Remove(nebulaId))
        {
            changed = true;
        }

        if (!changed)
        {
            EnsureDamageRoutineState();
            return;
        }

        RefreshLocalNebulaCache();
        ApplyVisibility();
        EnsureDamageRoutineState();
    }

    void EnsureDamageRoutineState()
    {
        if (damagingNebulas.Count <= 0)
        {
            if (damageRoutine != null)
            {
                StopCoroutine(damageRoutine);
                damageRoutine = null;
            }
            return;
        }

        if (playerHealth != null && photonView != null && photonView.IsMine && damageRoutine == null)
            damageRoutine = StartCoroutine(ApplyNebulaDamage());
    }

    public void RemoveNebula(int nebulaId)
    {
        bool changed = hiddenNebulaStates.Remove(nebulaId);
        changed |= hiddenNebulaKinds.Remove(nebulaId);
        changed |= damagingNebulas.Remove(nebulaId);
        changed |= damagingNebulaKinds.Remove(nebulaId);
        changed |= slowingNebulas.Remove(nebulaId);
        changed |= fireNebulas.Remove(nebulaId);
        if (!changed)
            return;

        RefreshLocalNebulaCache();
        ApplyVisibility();
        EnsureDamageRoutineState();
    }

    public static void RemoveNebulaFromAll(int nebulaId)
    {
        foreach (HideInNebulaTarget target in ActiveTargets)
        {
            if (target != null && target.isActiveAndEnabled)
                target.RemoveNebula(nebulaId);
        }
    }

    void CacheRenderers()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    void CacheBoundsComponents()
    {
        if (boundsColliders == null || boundsColliders.Length == 0)
            boundsColliders = GetComponentsInChildren<Collider2D>(true);

        if (boundsSpriteRenderers == null || boundsSpriteRenderers.Length == 0)
            boundsSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public Bounds GetNebulaBounds()
    {
        CacheBoundsComponents();

        bool hasBounds = false;
        Bounds combined = new Bounds(transform.position, Vector3.zero);

        if (boundsColliders != null)
        {
            for (int i = 0; i < boundsColliders.Length; i++)
            {
                Collider2D collider = boundsColliders[i];
                if (collider == null ||
                    !collider.enabled ||
                    collider.isTrigger ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(collider.bounds);
                }
            }
        }

        if (hasBounds)
            return combined;

        if (boundsSpriteRenderers != null)
        {
            for (int i = 0; i < boundsSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = boundsSpriteRenderers[i];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }
        }

        return hasBounds ? combined : new Bounds(transform.position, Vector3.one);
    }

    void ApplyVisibility()
    {
        bool hasHiddenNebula = HasHiddenNebula();
        bool hasActiveCloak = HasActiveCloak();
        bool shouldHide = hasHiddenNebula || hasActiveCloak;
        bool keepLocallyVisible = IsLocalHumanControlledCharacter();
        bool scannerRevealsLocalTarget = hasHiddenNebula && !hasActiveCloak && IsRevealedByLocalShortScanner();
        bool sharesNebulaWithLocalPlayer = hasHiddenNebula && !hasActiveCloak && SharesNebulaWithLocalPlayer();
        bool shouldBeVisible = !shouldHide || keepLocallyVisible || scannerRevealsLocalTarget || sharesNebulaWithLocalPlayer;
        SpriteRenderer referenceRenderer = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = shouldBeVisible;
                if (referenceRenderer == null && renderers[i] is SpriteRenderer spriteRenderer)
                    referenceRenderer = spriteRenderer;
            }
        }

        UpdateConcealmentIndicator(shouldHide && shouldBeVisible, GetCurrentConcealmentKind(), referenceRenderer);
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

    bool HasActiveCloak()
    {
        return activeCloakUntil > Time.time;
    }

    public void ActivateCloak(float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);
        activeCloakUntil = Mathf.Max(activeCloakUntil, Time.time + safeDuration);
        CacheRenderers();
        ApplyVisibility();
        RefreshAllTargetVisibility();
    }

    public void CancelCloak()
    {
        if (activeCloakUntil <= 0f)
            return;

        ClearCloakState();
    }

    void ClearCloakState()
    {
        if (activeCloakUntil <= 0f)
            return;

        activeCloakUntil = 0f;
        ApplyVisibility();
        RefreshAllTargetVisibility();
    }

    NebulaFieldKind GetCurrentConcealmentKind()
    {
        if (HasActiveCloak())
            return NebulaFieldKind.Cloud;

        NebulaFieldKind result = NebulaFieldKind.Normal;
        foreach (KeyValuePair<int, bool> state in hiddenNebulaStates)
        {
            if (!state.Value || !hiddenNebulaKinds.TryGetValue(state.Key, out NebulaFieldKind kind))
                continue;

            if (kind == NebulaFieldKind.Fire)
                return NebulaFieldKind.Fire;

            if (kind == NebulaFieldKind.Toxic)
                result = NebulaFieldKind.Toxic;

            if (kind == NebulaFieldKind.Cloud && result == NebulaFieldKind.Normal)
                result = NebulaFieldKind.Cloud;
        }

        return result;
    }

    void UpdateConcealmentIndicator(bool visible, NebulaFieldKind kind, SpriteRenderer referenceRenderer)
    {
        if (!ShouldUseConcealmentIndicator())
        {
            if (concealmentIndicator != null)
                concealmentIndicator.SetConcealed(false, kind, referenceRenderer);
            return;
        }

        if (!visible && concealmentIndicator == null)
            return;

        if (concealmentIndicator == null)
            concealmentIndicator = GetComponent<ConcealmentIndicator>() ?? gameObject.AddComponent<ConcealmentIndicator>();

        concealmentIndicator.SetConcealed(visible, kind, referenceRenderer);
    }

    bool ShouldUseConcealmentIndicator()
    {
        return playerHealth != null &&
               !playerHealth.IsBotControlled &&
               !playerHealth.IsWreck;
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

    public static void RefreshAllTargetVisibility()
    {
        foreach (HideInNebulaTarget target in ActiveTargets)
        {
            if (target != null && target.isActiveAndEnabled)
                target.ApplyVisibility();
        }
    }

    void ValidateNebulaContacts()
    {
        if (hiddenNebulaStates.Count == 0 && hiddenNebulaKinds.Count == 0 && damagingNebulas.Count == 0 && damagingNebulaKinds.Count == 0 && slowingNebulas.Count == 0 && fireNebulas.Count == 0)
            return;

        List<int> nebulaIds = new List<int>(hiddenNebulaStates.Keys);
        foreach (int kindId in hiddenNebulaKinds.Keys)
        {
            if (!nebulaIds.Contains(kindId))
                nebulaIds.Add(kindId);
        }
        foreach (int damagingId in damagingNebulas.Keys)
        {
            if (!nebulaIds.Contains(damagingId))
                nebulaIds.Add(damagingId);
        }
        foreach (int damagingKindId in damagingNebulaKinds.Keys)
        {
            if (!nebulaIds.Contains(damagingKindId))
                nebulaIds.Add(damagingKindId);
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
                changed |= hiddenNebulaKinds.Remove(nebulaId);
                changed |= damagingNebulas.Remove(nebulaId);
                changed |= damagingNebulaKinds.Remove(nebulaId);
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

            if (shouldHide)
            {
                if (!hiddenNebulaKinds.TryGetValue(nebulaId, out NebulaFieldKind previousKind) || previousKind != field.FieldKind)
                {
                    hiddenNebulaKinds[nebulaId] = field.FieldKind;
                    changed = true;
                }
            }
            else if (hiddenNebulaKinds.Remove(nebulaId))
            {
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

            bool hadDamageKind = damagingNebulaKinds.ContainsKey(nebulaId);
            if (shouldDamage && (!hadDamageKind || damagingNebulaKinds[nebulaId] != field.FieldKind))
            {
                damagingNebulaKinds[nebulaId] = field.FieldKind;
                changed = true;
            }
            else if (!shouldDamage && hadDamageKind)
            {
                damagingNebulaKinds.Remove(nebulaId);
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

        EnsureDamageRoutineState();
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
        if (HasActiveCloak())
            return !IsLocalHumanControlledCharacter();

        return HasHiddenNebula() && !IsRevealedByLocalShortScanner() && !SharesNebulaWithLocalPlayer();
    }

    bool IsRevealedByLocalShortScanner()
    {
        return ShortScannerRevealStatus.IsActive && CanBeRevealedByShortScanner();
    }

    bool CanBeRevealedByShortScanner()
    {
        if (playerHealth == null || playerHealth.IsWreck)
            return false;

        return GetComponent<PlayerDeployableBase>() == null &&
               GetComponent<LureBeaconDecoy>() == null;
    }

    public bool IsHiddenFromObserver(HideInNebulaTarget observer)
    {
        if (HasActiveCloak())
            return true;

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
            yield return new WaitForSeconds(GetCurrentDamageIntervalSeconds());

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

    float GetCurrentDamageIntervalSeconds()
    {
        foreach (NebulaFieldKind kind in damagingNebulaKinds.Values)
        {
            if (kind == NebulaFieldKind.Fire)
                return FireNebulaDamageIntervalSeconds;

            if (kind == NebulaFieldKind.Toxic)
                return ToxicNebulaDamageIntervalSeconds;
        }

        return NormalNebulaDamageIntervalSeconds;
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
