using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(EnemyBot))]
public class EnemySpaceTruckBehavior : EnemyBotBehaviorBase
{
    Rigidbody2D rb;
    PhotonView view;
    PlayerHealth health;
    EnemyMovementProfile movement;
    ExtractionZone[] extractionZones = System.Array.Empty<ExtractionZone>();
    int targetZoneIndex;
    float nextZoneRefreshTime;

    public override void Initialize(EnemyBot owner)
    {
        base.Initialize(owner);
        rb = owner.GetComponent<Rigidbody2D>();
        view = owner.GetComponent<PhotonView>();
        health = owner.GetComponent<PlayerHealth>();
        movement = owner.Definition != null ? owner.Definition.Movement : null;
        RefreshExtractionZones(true);
    }

    public override void TickBehavior()
    {
        if (bot == null || view == null || !view.IsMine || rb == null || movement == null)
            return;

        if (health != null && health.IsWreck)
            return;

        if (Time.time >= nextZoneRefreshTime || extractionZones == null || extractionZones.Length == 0)
            RefreshExtractionZones(false);

        Vector2 desiredDirection = ResolveDesiredDirection();
        Vector2 desiredVelocity = desiredDirection * bot.EffectiveMoveSpeed;
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, 0.13f);

        if (desiredVelocity.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg + 270f;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, movement.TurnResponsiveness * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }
    }

    void RefreshExtractionZones(bool chooseNearest)
    {
        nextZoneRefreshTime = Time.time + Mathf.Max(0.3f, movement != null ? movement.TargetRefreshInterval : 0.45f);
        extractionZones = RuntimeSceneQueryCache.GetExtractionZones();
        if (extractionZones == null || extractionZones.Length == 0)
            return;

        if (chooseNearest)
        {
            targetZoneIndex = FindNearestZoneIndex();
            AdvanceTargetZone();
        }
        else
        {
            targetZoneIndex = Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1);
        }
    }

    Vector2 ResolveDesiredDirection()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity.normalized : Vector2.up;

        ExtractionZone targetZone = extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)];
        if (targetZone == null || !targetZone.gameObject.activeInHierarchy)
        {
            AdvanceTargetZone();
            return Vector2.up;
        }

        Vector2 toTarget = (Vector2)targetZone.transform.position - rb.position;
        if (toTarget.magnitude <= 1.4f)
        {
            AdvanceTargetZone();
            targetZone = extractionZones[Mathf.Clamp(targetZoneIndex, 0, extractionZones.Length - 1)];
            if (targetZone != null && targetZone.gameObject.activeInHierarchy)
                toTarget = (Vector2)targetZone.transform.position - rb.position;
        }

        return toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.up;
    }

    int FindNearestZoneIndex()
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < extractionZones.Length; i++)
        {
            ExtractionZone zone = extractionZones[i];
            if (zone == null || !zone.gameObject.activeInHierarchy)
                continue;

            float distance = Vector2.Distance(rb.position, zone.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void AdvanceTargetZone()
    {
        if (extractionZones == null || extractionZones.Length == 0)
            return;

        targetZoneIndex = (targetZoneIndex + 1) % extractionZones.Length;
    }
}

