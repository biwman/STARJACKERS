using Photon.Pun;

public static class EnemyFriendlyFirePolicy
{
    public static bool ShouldIgnoreProjectileHit(EnemyBot sourceBot, EnemyBot targetBot, string projectileEffectId)
    {
        if (targetBot == null)
            return false;

        if (IsMilitaryVanConvoyProjectile(projectileEffectId) && targetBot.Kind == EnemyBotKind.MilitaryVan)
            return sourceBot == null || sourceBot.Kind == EnemyBotKind.MilitaryVan;

        return false;
    }

    public static bool ShouldBlockLineOfFire(EnemyBot sourceBot, EnemyBot blockerBot, string projectileEffectId)
    {
        return sourceBot != null &&
               blockerBot != null &&
               sourceBot != blockerBot &&
               ShouldIgnoreProjectileHit(sourceBot, blockerBot, projectileEffectId);
    }

    public static bool ShouldReactToDamageSource(EnemyBot targetBot, int attackerViewId)
    {
        if (targetBot == null || attackerViewId <= 0)
            return false;

        if (EnemyBot.IsPlayerControlledDamageSource(attackerViewId))
            return true;

        if (targetBot.Kind == EnemyBotKind.MilitaryVan && IsNeutralRiderDamageSource(attackerViewId))
            return true;

        return !ShouldIgnoreDamageReaction(targetBot, attackerViewId) && IsExplicitHostileEnemyDamage(targetBot, attackerViewId);
    }

    public static bool ShouldIgnoreDamageReaction(EnemyBot targetBot, int attackerViewId)
    {
        EnemyBot attackerBot = ResolveEnemyBot(attackerViewId);
        return AreFriendlyEnemies(targetBot, attackerBot);
    }

    static bool IsExplicitHostileEnemyDamage(EnemyBot targetBot, int attackerViewId)
    {
        return false;
    }

    static bool IsNeutralRiderDamageSource(int attackerViewId)
    {
        if (attackerViewId <= 0)
            return false;

        PhotonView attackerView = PhotonView.Find(attackerViewId);
        if (attackerView == null)
            return false;

        PlayerHealth attackerHealth = attackerView.GetComponent<PlayerHealth>();
        if (IsActiveNeutralRider(attackerHealth))
            return true;

        if (PlayerDeployableRuntime.IsNeutralRiderOwnedDeployableData(attackerView.InstantiationData))
            return true;

        PlayerDeployableBase deployable = attackerView.GetComponent<PlayerDeployableBase>();
        if (deployable == null)
            return false;

        return deployable.OwnerShipViewId != attackerViewId &&
               IsNeutralRiderDamageSource(deployable.OwnerShipViewId);
    }

    static bool IsActiveNeutralRider(PlayerHealth attackerHealth)
    {
        return attackerHealth != null &&
               attackerHealth.IsNeutralRiderControlled &&
               !attackerHealth.IsWreck &&
               !attackerHealth.IsAstronautControlled;
    }

    static bool AreFriendlyEnemies(EnemyBot targetBot, EnemyBot attackerBot)
    {
        if (targetBot == null || attackerBot == null || targetBot == attackerBot)
            return false;

        return targetBot.Kind == EnemyBotKind.MilitaryVan &&
               attackerBot.Kind == EnemyBotKind.MilitaryVan;
    }

    static EnemyBot ResolveEnemyBot(int viewId)
    {
        if (viewId <= 0)
            return null;

        PhotonView view = PhotonView.Find(viewId);
        return view != null ? view.GetComponent<EnemyBot>() : null;
    }

    static bool IsMilitaryVanConvoyProjectile(string projectileEffectId)
    {
        return string.Equals(
            projectileEffectId ?? string.Empty,
            Bullet.MilitaryVanTracerEffectId,
            System.StringComparison.OrdinalIgnoreCase);
    }
}
