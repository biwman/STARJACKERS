using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public abstract class EnemyBotBehaviorBase : MonoBehaviour
{
    protected EnemyBot bot;

    public virtual void Initialize(EnemyBot owner)
    {
        bot = owner;
    }

    protected static float ScaleEnemyAttackWindup(float duration)
    {
        return Mathf.Max(0.01f, duration * RoomSettings.GetEnemyAttackWindupMultiplier());
    }

    protected float ScaleEnemyAttackCooldown(float cooldown)
    {
        float effectMultiplier = bot != null
            ? ElectromagneticShockStatus.GetFireIntervalMultiplier(bot.gameObject) * AtlasSuppressionStatus.GetFireIntervalMultiplier(bot.gameObject)
            : 1f;
        return Mathf.Max(0.05f, cooldown * effectMultiplier * RoomSettings.GetEnemyAttackCooldownMultiplier());
    }

    public abstract void TickBehavior();
}

