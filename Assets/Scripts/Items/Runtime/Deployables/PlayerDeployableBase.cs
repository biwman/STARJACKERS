using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public abstract class PlayerDeployableBase : MonoBehaviourPun
{
    static readonly HashSet<PlayerDeployableBase> ActiveDeployables = new HashSet<PlayerDeployableBase>();

    protected bool initialized;
    protected bool destroyed;
    protected int ownerShipViewId;
    protected int currentHp;
    protected int currentShield;
    protected SpriteRenderer spriteRenderer;
    protected Rigidbody2D body;

    public bool CanBeTargeted => initialized && !destroyed && currentHp > 0;
    public int OwnerShipViewId => ownerShipViewId;
    public bool IsComputerOwnedDeployable => PlayerDeployableRuntime.IsComputerOwnedDeployableData(photonView != null ? photonView.InstantiationData : null);
    public virtual bool CanBeTargetedByEnemyBots => CanBeTargeted && !IsComputerOwnedDeployable;
    public WeaponHitContext LastDamageContext { get; private set; }
    public float LastDamageShieldMultiplier { get; private set; } = 1f;
    public float LastDamageHpMultiplier { get; private set; } = 1f;
    public string LastDamageDebugSummary => WeaponDamageInteractionCatalog.BuildDebugSummary(LastDamageContext, LastDamageShieldMultiplier, LastDamageHpMultiplier);
    protected abstract int MaxHp { get; }
    protected abstract int MaxShield { get; }
    protected abstract float VisualTargetSize { get; }
    protected abstract float CollisionRadius { get; }
    protected abstract string SpriteResourcePath { get; }
    protected abstract string EditorSpritePath { get; }

    public static IReadOnlyCollection<PlayerDeployableBase> GetActiveDeployables()
    {
        return new List<PlayerDeployableBase>(ActiveDeployables);
    }

    protected void InitializeCommon()
    {
        if (initialized)
            return;

        initialized = true;
        currentHp = MaxHp;
        currentShield = MaxShield;
        object[] data = photonView != null ? photonView.InstantiationData : null;
        ownerShipViewId = data != null && data.Length > 1 ? ConvertToInt(data[1]) : 0;
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        ConfigureVisuals();
        ConfigurePhysics();
        IgnoreOwnerCollisions();
        DisablePlayerSpecificSystems();
        ActiveDeployables.Add(this);
    }

    protected virtual void ConfigurePhysics()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (body != null)
        {
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.isTrigger = false;
        SetWorldRadius(circle, CollisionRadius);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            box.enabled = false;
    }

    protected virtual void ConfigureVisuals()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        Sprite sprite = RuntimeSpriteUtility.LoadSprite(SpriteResourcePath, EditorSpritePath);
        if (sprite != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            RuntimeSpriteUtility.FitRenderer(spriteRenderer, VisualTargetSize);
        }

        spriteRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        spriteRenderer.sortingOrder = 40;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] != spriteRenderer)
                renderers[i].enabled = false;
        }
    }

    [PunRPC]
    public void TakeDeployableDamageAt(int shieldDamage, int hpDamage, int attackerViewId, float impactX, float impactY)
    {
        TakeDeployableDamageAtInternal(shieldDamage, hpDamage, attackerViewId, impactX, impactY, WeaponHitContext.None);
    }

    [PunRPC]
    public void TakeDeployableDamageWithContextAt(
        int shieldDamage,
        int hpDamage,
        int attackerViewId,
        float impactX,
        float impactY,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        TakeDeployableDamageAtInternal(
            shieldDamage,
            hpDamage,
            attackerViewId,
            impactX,
            impactY,
            WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource));
    }

    void TakeDeployableDamageAtInternal(int shieldDamage, int hpDamage, int attackerViewId, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted)
            return;

        if (IsComputerOwnedDeployable && attackerViewId > 0 && !EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            return;

        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);
        int rawShieldDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(Mathf.Max(0, shieldDamage), damageMultipliers.ShieldMultiplier);
        int rawHpDamage = WeaponDamageInteractionCatalog.ApplyMultiplier(Mathf.Max(0, hpDamage), damageMultipliers.HpMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "deployable-profile", hitContext, damageMultipliers, attackerViewId, rawShieldDamage, rawHpDamage);
        int absorbed = 0;
        int damage = 0;
        if (currentShield > 0)
        {
            if (rawShieldDamage > 0)
            {
                absorbed = Mathf.Min(currentShield, rawShieldDamage);
                currentShield -= absorbed;
                float overflowRatio = Mathf.Clamp01((rawShieldDamage - absorbed) / (float)rawShieldDamage);
                damage = Mathf.RoundToInt(rawHpDamage * overflowRatio);
            }
        }
        else
        {
            damage = rawHpDamage;
        }

        if (damage > 0)
            currentHp = Mathf.Max(0, currentHp - damage);

        if (absorbed <= 0 && damage <= 0)
            return;

        photonView.RPC(nameof(PlayDeployableHitRpc), RpcTarget.All, absorbed > 0, impactX, impactY);
        if (EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            OnDamageTakenByPlayer(attackerViewId);

        if (currentHp <= 0)
            DestroyOnMaster();
    }

    [PunRPC]
    public void TakeDeployableShieldOnlyDamageAt(int shieldDamage, int attackerViewId, float impactX, float impactY)
    {
        TakeDeployableShieldOnlyDamageAtInternal(shieldDamage, attackerViewId, impactX, impactY, WeaponHitContext.None);
    }

    [PunRPC]
    public void TakeDeployableShieldOnlyDamageWithContextAt(
        int shieldDamage,
        int attackerViewId,
        float impactX,
        float impactY,
        int damageType,
        int deliveryMethod,
        int deliveryFlags,
        string damageSource)
    {
        TakeDeployableShieldOnlyDamageAtInternal(
            shieldDamage,
            attackerViewId,
            impactX,
            impactY,
            WeaponHitContext.FromRpc(damageType, deliveryMethod, deliveryFlags, damageSource));
    }

    void TakeDeployableShieldOnlyDamageAtInternal(int shieldDamage, int attackerViewId, float impactX, float impactY, WeaponHitContext hitContext)
    {
        if (!PhotonNetwork.IsMasterClient || !CanBeTargeted)
            return;

        if (IsComputerOwnedDeployable && attackerViewId > 0 && !EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            return;

        WeaponDamageMultipliers damageMultipliers = WeaponDamageInteractionCatalog.ResolveMultipliers(hitContext);
        RememberDamageContext(hitContext, damageMultipliers);
        int damage = WeaponDamageInteractionCatalog.ApplyMultiplier(Mathf.Max(0, shieldDamage), damageMultipliers.ShieldMultiplier);
        WeaponDamageInteractionCatalog.LogDamageContext(this, "deployable-shield", hitContext, damageMultipliers, attackerViewId, damage, 0);
        if (damage <= 0 || currentShield <= 0)
            return;

        int absorbed = Mathf.Min(currentShield, damage);
        currentShield -= absorbed;
        photonView.RPC(nameof(PlayDeployableHitRpc), RpcTarget.All, true, impactX, impactY);
        if (EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            OnDamageTakenByPlayer(attackerViewId);
    }

    void RememberDamageContext(WeaponHitContext hitContext, WeaponDamageMultipliers damageMultipliers)
    {
        LastDamageContext = hitContext;
        LastDamageShieldMultiplier = damageMultipliers.ShieldMultiplier;
        LastDamageHpMultiplier = damageMultipliers.HpMultiplier;
    }

    public void PlayDeployableHitRpc(bool shieldHit, float x, float y)
    {
        PlayDeployableHitFeedback(shieldHit, x, y);
    }

    protected void PlayDeployableHitFeedback(bool shieldHit, float x, float y)
    {
        Vector3 point = new Vector3(x, y, transform.position.z);
        if (shieldHit)
            AudioManager.Instance.PlayShieldHitAt(point);
        else
            AudioManager.Instance.PlayHpHitAt(point);
    }

    protected void DestroyOnMaster()
    {
        if (destroyed)
            return;

        destroyed = true;
        OnDestroyedByDamage();
        photonView.RPC(nameof(PlayDeployableDestroyedRpc), RpcTarget.All);
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected void DespawnOnMaster()
    {
        if (destroyed)
            return;

        destroyed = true;
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    protected virtual void OnDestroyedByDamage()
    {
    }

    protected virtual void OnDamageTakenByPlayer(int attackerViewId)
    {
    }

    public void PlayDeployableDestroyedRpc()
    {
        PlayDeployableDestroyedFeedback();
    }

    protected void PlayDeployableDestroyedFeedback()
    {
        EnemyBot.SpawnSpaceMineDetonationEffects(transform.position, 0.9f);
    }

    void IgnoreOwnerCollisions()
    {
        if (ownerShipViewId <= 0)
            return;

        PhotonView ownerView = PhotonView.Find(ownerShipViewId);
        if (ownerView == null)
            return;

        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(true);
        Collider2D[] ownerColliders = ownerView.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D ownCollider = ownColliders[i];
            if (ownCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider2D ownerCollider = ownerColliders[j];
                if (ownerCollider != null)
                    Physics2D.IgnoreCollision(ownCollider, ownerCollider, true);
            }
        }
    }

    protected void DisablePlayerSpecificSystems()
    {
        DisableComponent<PlayerMovement>();
        DisableComponent<PlayerShooting>();
        DisableComponent<TreasureCollector>();
        DisableComponent<PlayerRepairDocking>();
        DisableComponent<PlayerHealth>();
        DisableComponent<HealthBarUI>();
        DisableComponent<ShieldBarUI>();
        DisableComponent<RoundVitalsIconHudUI>();
        DisableComponent<PlayerNicknameUI>();
        DisableComponent<BoosterBarUI>();
        DisableComponent<ShipInventoryHudUI>();
        DisableComponent<StartingShipEntryVfx>();
        DisableComponent<SpawnInvulnerabilityVfx>();
        DisableComponent<AstronautSurvivor>();

        EngineThrusterVFX thruster = GetComponent<EngineThrusterVFX>();
        if (thruster != null)
            Destroy(thruster);
    }

    void DisableComponent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null && component != this)
            component.enabled = false;
    }

    protected void SetWorldRadius(CircleCollider2D circle, float radius)
    {
        if (circle == null)
            return;

        float scale = Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y), 0.001f);
        circle.radius = Mathf.Max(0.01f, radius / scale);
        circle.offset = Vector2.zero;
    }

    protected int ConvertToInt(object value)
    {
        if (value is int intValue)
            return intValue;

        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);

        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        return 0;
    }

    protected float ConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is int intValue)
            return intValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        return 0f;
    }

    protected virtual void OnDisable()
    {
        ActiveDeployables.Remove(this);
    }

    protected virtual void OnDestroy()
    {
        ActiveDeployables.Remove(this);
    }
}
