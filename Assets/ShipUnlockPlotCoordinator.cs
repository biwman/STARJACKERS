using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

public enum ShipUnlockPlotType
{
    None = 0,
    Avenger = 1,
    Viper = 2,
    Arrow = 3,
    Bison = 4,
    Invader = 5
}

public sealed class ShipUnlockPlotCoordinator : MonoBehaviour
{
    const float ScanInterval = 0.35f;
    const float RoundStartMatchTolerance = 0.001f;

    static ShipUnlockPlotCoordinator instance;

    double handledStartTime = double.MinValue;
    float nextScanTime;

    struct Candidate
    {
        public ShipUnlockPlotType Type;
        public int Weight;

        public Candidate(ShipUnlockPlotType type, int weight)
        {
            Type = type;
            Weight = Mathf.Max(1, weight);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("ShipUnlockPlotCoordinator");
        instance = root.AddComponent<ShipUnlockPlotCoordinator>();
        DontDestroyOnLoad(root);
    }

    public static bool IsActivePlot(ShipUnlockPlotType type)
    {
        return TryGetActivePlot(out ShipUnlockPlotType activeType) && activeType == type;
    }

    public static ShipUnlockPlotType ChoosePlotForRoundStarter()
    {
        TryGetRoundStarterPlayer(out Photon.Realtime.Player roundStarter);
        return ChoosePlotForPlayer(roundStarter);
    }

    public static bool TryGetRoundStarterPlayer(out Photon.Realtime.Player roundStarter)
    {
        roundStarter = null;
        int starterActorNumber = RoomSettings.GetRoundStarterActorNumber();
        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            Photon.Realtime.Player player = players[i];
            if (player != null && starterActorNumber > 0 && player.ActorNumber == starterActorNumber)
            {
                roundStarter = player;
                return true;
            }
        }

        if (starterActorNumber <= 0)
        {
            roundStarter = PhotonNetwork.LocalPlayer != null
                ? PhotonNetwork.LocalPlayer
                : PhotonNetwork.MasterClient;
            return roundStarter != null;
        }

        return false;
    }

    public static bool IsRoundStarter(Photon.Realtime.Player player)
    {
        if (player == null)
            return false;

        int starterActorNumber = RoomSettings.GetRoundStarterActorNumber();
        if (starterActorNumber > 0)
            return player.ActorNumber == starterActorNumber;

        Photon.Realtime.Player master = PhotonNetwork.MasterClient;
        return master != null
            ? player.ActorNumber == master.ActorNumber
            : player.IsMasterClient;
    }

    public static bool TryGetActivePlot(out ShipUnlockPlotType activeType)
    {
        activeType = ShipUnlockPlotType.None;
        if (!IsRoundStarted(out double currentStartTime) || PhotonNetwork.CurrentRoom == null)
            return false;

        PhotonHashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!props.TryGetValue(RoomSettings.ShipUnlockPlotStartTimeKey, out object startValue) ||
            !TryConvertToDouble(startValue, out double storedStartTime) ||
            Mathf.Abs((float)(storedStartTime - currentStartTime)) > RoundStartMatchTolerance)
        {
            return false;
        }

        if (!props.TryGetValue(RoomSettings.ShipUnlockPlotActiveKey, out object activeValue) ||
            !(activeValue is string activeId))
        {
            return false;
        }

        activeType = ParsePlotType(activeId);
        return true;
    }

    public static string GetPlotId(ShipUnlockPlotType type)
    {
        switch (type)
        {
            case ShipUnlockPlotType.Avenger: return "avenger";
            case ShipUnlockPlotType.Viper: return "viper";
            case ShipUnlockPlotType.Arrow: return "arrow";
            case ShipUnlockPlotType.Bison: return "bison";
            case ShipUnlockPlotType.Invader: return "invader";
            default: return "none";
        }
    }

    static ShipUnlockPlotType ParsePlotType(string id)
    {
        switch (id)
        {
            case "avenger": return ShipUnlockPlotType.Avenger;
            case "viper": return ShipUnlockPlotType.Viper;
            case "arrow": return ShipUnlockPlotType.Arrow;
            case "bison": return ShipUnlockPlotType.Bison;
            case "invader": return ShipUnlockPlotType.Invader;
            default: return ShipUnlockPlotType.None;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            handledStartTime = double.MinValue;
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextScanTime)
            return;

        nextScanTime = now + ScanInterval;
        TickLifecycle();
    }

    void TickLifecycle()
    {
        if (!IsRoundStarted(out double currentStartTime))
        {
            handledStartTime = double.MinValue;
            return;
        }

        if (currentStartTime != handledStartTime)
            handledStartTime = currentStartTime;

        if (PhotonNetwork.IsMasterClient)
            EnsurePlotSelectedForRound(currentStartTime);
    }

    void EnsurePlotSelectedForRound(double currentStartTime)
    {
        if (TryGetActivePlot(out _))
            return;

        ShipUnlockPlotType selected = ChoosePlotForRoundStarter();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new PhotonHashtable
        {
            [RoomSettings.ShipUnlockPlotStartTimeKey] = currentStartTime,
            [RoomSettings.ShipUnlockPlotActiveKey] = GetPlotId(selected)
        });
    }

    static ShipUnlockPlotType ChoosePlotForPlayer(Photon.Realtime.Player roundStarter)
    {
        if (roundStarter == null)
            return ShipUnlockPlotType.None;

        if (AreShipUnlockPlotsBlockedOnSelectedMap())
            return ShipUnlockPlotType.None;

        List<Candidate> candidates = new List<Candidate>(4);
        if (IsAvengerCandidate(roundStarter))
            candidates.Add(new Candidate(ShipUnlockPlotType.Avenger, 35));

        if (IsViperCandidate(roundStarter))
            candidates.Add(new Candidate(ShipUnlockPlotType.Viper, 25));

        if (IsBisonCandidate(roundStarter))
            candidates.Add(new Candidate(ShipUnlockPlotType.Bison, 25));

        if (IsInvaderCandidate(roundStarter))
            candidates.Add(new Candidate(ShipUnlockPlotType.Invader, 30));

        if (IsArrowCandidate(roundStarter, out bool finalRunReady, out bool forcedArrow))
        {
            if (forcedArrow)
                return ShipUnlockPlotType.Arrow;

            candidates.Add(new Candidate(ShipUnlockPlotType.Arrow, finalRunReady ? 45 : 25));
        }

        if (candidates.Count == 0)
            return ShipUnlockPlotType.None;

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += candidates[i].Weight;

        int roll = UnityEngine.Random.Range(0, Mathf.Max(1, totalWeight));
        int cursor = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            cursor += candidates[i].Weight;
            if (roll < cursor)
                return candidates[i].Type;
        }

        return candidates[candidates.Count - 1].Type;
    }

    static bool AreShipUnlockPlotsBlockedOnSelectedMap()
    {
        return string.Equals(RoomSettings.GetSelectedLobbyMapId(), LobbyMapCatalog.HiddenDimensionMapId, StringComparison.Ordinal);
    }

    static bool IsAvengerCandidate(Photon.Realtime.Player player)
    {
        string mapId = RoomSettings.GetSelectedLobbyMapId();
        return RoomSettings.IsAvengerPlotEnabled() &&
               LobbyMapCatalog.IsAvengerPlotEnabledByDefault(mapId) &&
               PlayerProfileService.PlayerHasAvengerStartingCodes(player);
    }

    static bool IsViperCandidate(Photon.Realtime.Player player)
    {
        int chancePercent = RoomSettings.GetViperPlotChancePercent();
        if (!RollChance(chancePercent))
            return false;

        return PlayerProfileService.PlayerNeedsViperRecovery(player);
    }

    static bool IsArrowCandidate(Photon.Realtime.Player player, out bool finalRunReady, out bool forced)
    {
        finalRunReady = false;
        forced = false;

        if (!PlayerProfileService.PlayerNeedsArrowLicense(player))
            return false;

        if (PlayerProfileService.PlayerHasArrowRaceTokenForSelectedMap(player))
            forced = true;

        if (PlayerProfileService.PlayerIsArrowFinalRoundCandidate(player))
        {
            finalRunReady = true;
            forced = true;
        }

        if (forced)
            return true;

        int chancePercent = RoomSettings.GetArrowPlotChancePercent();
        return PlayerProfileService.PlayerNeedsArrowQualification(player) && RollChance(chancePercent);
    }

    static bool IsBisonCandidate(Photon.Realtime.Player player)
    {
        int chancePercent = RoomSettings.GetBisonPlotChancePercent();
        if (!RollChance(chancePercent))
            return false;

        return PlayerProfileService.PlayerNeedsBisonIndustrialParts(player);
    }

    static bool IsInvaderCandidate(Photon.Realtime.Player player)
    {
        int chancePercent = RoomSettings.GetInvaderPlotChancePercent();
        if (!RollChance(chancePercent))
            return false;

        return PlayerProfileService.PlayerNeedsInvaderImprints(player);
    }

    static bool RollChance(int chancePercent)
    {
        if (chancePercent >= 100)
            return true;

        return chancePercent > 0 && UnityEngine.Random.value < chancePercent / 100f;
    }

    static bool IsRoundStarted(out double currentStartTime)
    {
        currentStartTime = 0d;
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("gameStarted", out object startedValue) ||
            !(startedValue is bool started) ||
            !started)
        {
            return false;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object startValue))
            TryConvertToDouble(startValue, out currentStartTime);

        return true;
    }

    static bool TryConvertToDouble(object value, out double result)
    {
        try
        {
            switch (value)
            {
                case double d:
                    result = d;
                    return true;
                case float f:
                    result = f;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                default:
                    result = 0d;
                    return false;
            }
        }
        catch (Exception)
        {
            result = 0d;
            return false;
        }
    }

}
