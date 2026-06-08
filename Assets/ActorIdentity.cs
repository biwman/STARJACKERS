using Photon.Pun;
using UnityEngine;

public enum ActorTeam
{
    Unknown,
    Player,
    Enemy,
    Neutral,
    Environment
}

public enum ActorForm
{
    Unknown,
    Ship,
    Astronaut,
    Wreck,
    Deployable,
    Collectible
}

public enum ActorControl
{
    None,
    Human,
    AI
}

[DisallowMultipleComponent]
public sealed class ActorIdentity : MonoBehaviour
{
    public ActorTeam Team { get; private set; } = ActorTeam.Unknown;
    public ActorForm Form { get; private set; } = ActorForm.Unknown;
    public ActorControl Control { get; private set; } = ActorControl.None;

    public bool IsPlayer => Team == ActorTeam.Player;
    public bool IsEnemy => Team == ActorTeam.Enemy;
    public bool IsShip => Form == ActorForm.Ship;
    public bool IsAstronaut => Form == ActorForm.Astronaut;
    public bool IsWreck => Form == ActorForm.Wreck;
    public bool IsHumanControlled => Control == ActorControl.Human;
    public bool IsAiControlled => Control == ActorControl.AI;

    public bool CanUseHud => IsPlayer && IsHumanControlled && !IsWreck;
    public bool CanUsePlayerUseButton => CanUseHud && (Form == ActorForm.Ship || Form == ActorForm.Astronaut);
    public bool CanCollect => CanUseHud && Form == ActorForm.Ship;
    public bool CanDock => CanUseHud && Form == ActorForm.Ship;
    public bool CanActivateExtraction => CanUseHud && (Form == ActorForm.Ship || Form == ActorForm.Astronaut);
    public bool CanBeTargetedByEnemyShips => ((IsPlayer && IsHumanControlled) || (Team == ActorTeam.Neutral && IsAiControlled)) && Form == ActorForm.Ship;
    public bool CanBeTargetedByMonsters => CanBeTargetedByEnemyShips || (IsEnemy && IsAiControlled && Form == ActorForm.Astronaut);
    public bool CanDropAstronauts => IsEnemy && IsAiControlled && Form == ActorForm.Ship;

    void Awake()
    {
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        Resolve(gameObject, out ActorTeam team, out ActorForm form, out ActorControl control);
        Team = team;
        Form = form;
        Control = control;
    }

    public static ActorIdentity Ensure(GameObject target)
    {
        if (target == null)
            return null;

        ActorIdentity identity = target.GetComponent<ActorIdentity>();
        if (identity == null)
            identity = target.AddComponent<ActorIdentity>();

        identity.Refresh();
        return identity;
    }

    public static ActorIdentity Ensure(Component target)
    {
        return target != null ? Ensure(target.gameObject) : null;
    }

    public static void Resolve(GameObject target, out ActorTeam team, out ActorForm form, out ActorControl control)
    {
        team = ActorTeam.Unknown;
        form = ActorForm.Unknown;
        control = ActorControl.None;

        if (target == null)
            return;

        PhotonView view = target.GetComponent<PhotonView>();
        object[] instantiationData = view != null ? view.InstantiationData : null;
        bool isNeutralRider = NeutralRiderController.IsNeutralRiderInstantiationData(instantiationData) ||
                              target.GetComponent<NeutralRiderController>() != null;

        if (ViperRecoveryPlotController.IsViperWreckInstantiationData(instantiationData) ||
            target.GetComponent<ViperWreckTowTarget>() != null)
        {
            team = ActorTeam.Environment;
            form = ActorForm.Wreck;
            control = ActorControl.None;
            return;
        }

        ShipWreck wreck = target.GetComponent<ShipWreck>();
        if (wreck != null)
        {
            team = isNeutralRider
                ? ActorTeam.Neutral
                : wreck.SourceShipSkinIndex < 0 || target.GetComponent<EnemyBot>() != null || EnemyBot.IsBotInstantiationData(instantiationData)
                    ? ActorTeam.Enemy
                    : ActorTeam.Player;
            form = ActorForm.Wreck;
            control = ActorControl.None;
            return;
        }

        if (isNeutralRider)
        {
            team = ActorTeam.Neutral;
            form = ActorForm.Ship;
            control = ActorControl.AI;
            return;
        }

        if (PlayerDeployableRuntime.IsInstantiationData(instantiationData) ||
            LureBeaconDecoy.IsInstantiationData(instantiationData) ||
            target.GetComponent<PlayerDeployableBase>() != null ||
            target.GetComponent<LureBeaconDecoy>() != null)
        {
            team = ActorTeam.Player;
            form = ActorForm.Deployable;
            control = ActorControl.None;
            return;
        }

        AstronautSurvivor survivor = target.GetComponent<AstronautSurvivor>();
        if (AstronautSurvivor.IsEnemyAstronautInstantiationData(instantiationData) || (survivor != null && survivor.IsEnemySurvivor))
        {
            team = ActorTeam.Enemy;
            form = ActorForm.Astronaut;
            control = ActorControl.AI;
            return;
        }

        if (AstronautSurvivor.IsAstronautInstantiationData(instantiationData) || survivor != null)
        {
            team = ActorTeam.Player;
            form = ActorForm.Astronaut;
            control = ActorControl.Human;
            return;
        }

        if (target.GetComponent<EnemyBot>() != null || EnemyBot.IsBotInstantiationData(instantiationData))
        {
            team = ActorTeam.Enemy;
            form = ActorForm.Ship;
            control = ActorControl.AI;
            return;
        }

        if (target.GetComponent<PlayerHealth>() != null)
        {
            team = ActorTeam.Player;
            form = ActorForm.Ship;
            control = ActorControl.Human;
            return;
        }
    }

    public static bool IsEnemyBot(GameObject target)
    {
        ActorIdentity identity = Ensure(target);
        return identity != null && identity.IsEnemy && identity.IsShip;
    }

    public static bool IsAstronautActor(GameObject target)
    {
        ActorIdentity identity = Ensure(target);
        return identity != null && identity.IsAstronaut;
    }

    public static bool IsEnemyAstronautActor(GameObject target)
    {
        ActorIdentity identity = Ensure(target);
        return identity != null && identity.IsEnemy && identity.IsAstronaut;
    }

    public static bool CanUsePlayerUseButtonActor(GameObject target)
    {
        ActorIdentity identity = Ensure(target);
        return identity != null && identity.CanUsePlayerUseButton;
    }

    public static bool IsHumanPlayerActor(PlayerHealth target)
    {
        ActorIdentity identity = Ensure(target);
        return identity != null && identity.IsPlayer && identity.IsHumanControlled && !identity.IsWreck;
    }

    public static bool IsLocalHumanPlayerActor(PlayerHealth target)
    {
        if (!IsHumanPlayerActor(target))
            return false;

        return target.photonView != null && target.photonView.IsMine;
    }

    public static bool CanBeTargetedByEnemyShipsActor(PlayerHealth target)
    {
        ActorIdentity identity = Ensure(target);
        return identity != null && identity.CanBeTargetedByEnemyShips;
    }

    public static bool CanBeTargetedByMonstersActor(PlayerHealth target)
    {
        ActorIdentity identity = Ensure(target);
        return identity != null && identity.CanBeTargetedByMonsters;
    }
}
