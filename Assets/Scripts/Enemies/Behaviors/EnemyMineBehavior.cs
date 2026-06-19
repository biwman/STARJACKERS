using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemyMineBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    EnemyExplosionProfile explosion;
    Vector2 driftDirection;
    float nextRetargetTime;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        explosion = owner.Definition != null ? owner.Definition.Explosion : null;

        TryResolveInstancedDriftDirection();

        if (driftDirection.sqrMagnitude <= 0.001f)
        {
            int seed = view != null ? view.ViewID : Random.Range(1, 9999);
            float angle = Mathf.Abs(seed * 0.173f) % (Mathf.PI * 2f);
            driftDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (driftDirection.sqrMagnitude <= 0.001f)
                driftDirection = Vector2.up;
        }
    }

    void TryResolveInstancedDriftDirection()
    {
        object[] data = view != null ? view.InstantiationData : null;
        if (data == null || data.Length < 5)
            return;

        if (!(data[1] is string marker) ||
            !string.Equals(marker, EnemyBot.ContainerShipMineMarker, System.StringComparison.Ordinal))
        {
            return;
        }

        Vector2 direction = new Vector2(ConvertToFloat(data[3]), ConvertToFloat(data[4]));
        if (direction.sqrMagnitude > 0.001f)
            driftDirection = direction.normalized;
    }

    static float ConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        if (value is int intValue)
            return intValue;

        return 0f;
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null || explosion == null)
            return;

        if (health != null && health.IsWreck)
            return;

        Vector2 desiredVelocity = driftDirection.normalized * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.08f);
        rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, 8f, 0.04f);

        if (Time.time >= nextRetargetTime)
        {
            nextRetargetTime = Time.time + 0.12f;
            if (IsAnyTargetInRange(explosion.TriggerRadius))
                bot.RequestDetonation();
        }
    }

    bool IsAnyTargetInRange(float radius)
    {
        PlayerHealth[] players = RuntimeSceneQueryCache.GetPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth candidate = players[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate == health || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.GetComponent<LureBeaconDecoy>() != null)
                continue;

            if (bot != null && bot.ShouldIgnoreMineTriggerFor(candidate))
                continue;

            EnemyBot candidateBot = candidate.GetComponent<EnemyBot>();
            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.SpaceMine)
                continue;

            if (candidateBot != null && candidateBot.Kind == EnemyBotKind.ContainerShip && (bot == null || !bot.IsPlayerPlacedMine))
                continue;

            if (Vector2.Distance(transform.position, candidate.transform.position) <= radius)
                return true;
        }

        foreach (LureBeaconDecoy beacon in LureBeaconDecoy.GetActiveBeacons())
        {
            if (beacon == null || !beacon.CanBeTargeted)
                continue;

            if (Vector2.Distance(transform.position, beacon.transform.position) <= radius)
                return true;
        }

        return false;
    }
}

