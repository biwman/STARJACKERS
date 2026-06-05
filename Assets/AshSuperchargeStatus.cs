using Photon.Pun;
using UnityEngine;

public sealed class AshSuperchargeStatus : MonoBehaviour
{
    public const float SpeedMultiplier = 1.3f;
    public const float FireIntervalMultiplier = 1f / 1.3f;
    public const float AmmoReloadMultiplier = 1f / 1.3f;

    float activeUntil;

    public bool IsActive => Time.time < activeUntil;

    public static void Apply(GameObject target, float duration)
    {
        if (target == null || duration <= 0f)
            return;

        AshSuperchargeStatus status = target.GetComponent<AshSuperchargeStatus>();
        if (status == null)
            status = target.AddComponent<AshSuperchargeStatus>();

        status.activeUntil = Mathf.Max(status.activeUntil, Time.time + duration);
        AshSuperchargeVfx.Attach(target, duration);
    }

    public static float GetSpeedMultiplier(GameObject target)
    {
        AshSuperchargeStatus status = target != null ? target.GetComponent<AshSuperchargeStatus>() : null;
        return status != null && status.IsActive ? SpeedMultiplier : 1f;
    }

    public static float GetFireIntervalMultiplier(GameObject target)
    {
        AshSuperchargeStatus status = target != null ? target.GetComponent<AshSuperchargeStatus>() : null;
        return status != null && status.IsActive ? FireIntervalMultiplier : 1f;
    }

    public static float GetAmmoReloadMultiplier(GameObject target)
    {
        AshSuperchargeStatus status = target != null ? target.GetComponent<AshSuperchargeStatus>() : null;
        return status != null && status.IsActive ? AmmoReloadMultiplier : 1f;
    }

    void Update()
    {
        if (Time.time < activeUntil)
            return;

        Destroy(this);
    }
}

public struct AshOverloadReturnMetrics
{
    public int ComputerEnemyKills;
    public float BoosterSeconds;
    public int CargoValueAstrons;
    public int FilledCargoSlots;
    public int CargoCapacity;
    public bool CargoFull;
}

public static class AshPilotRoundTracker
{
    public const int RequiredComputerEnemyKills = 3;
    public const float RequiredBoosterSeconds = 20f;
    public const int RequiredCargoValueAstrons = 1500;

    static string trackedRoundKey = string.Empty;
    static int computerEnemyKills;
    static float boosterSeconds;

    public static int ComputerEnemyKills
    {
        get
        {
            EnsureRoundScope();
            return computerEnemyKills;
        }
    }

    public static float BoosterSeconds
    {
        get
        {
            EnsureRoundScope();
            return boosterSeconds;
        }
    }

    public static void RecordComputerEnemyKill()
    {
        EnsureRoundScope();
        computerEnemyKills = Mathf.Max(0, computerEnemyKills + 1);
    }

    public static void RecordBoosterSeconds(float seconds)
    {
        if (seconds <= 0f)
            return;

        EnsureRoundScope();
        boosterSeconds = Mathf.Max(0f, boosterSeconds + seconds);
    }

    public static bool MeetsOverloadReturnRequirements(PlayerProfileData profile, out AshOverloadReturnMetrics metrics)
    {
        EnsureRoundScope();
        metrics = BuildMetrics(profile);
        return metrics.ComputerEnemyKills >= RequiredComputerEnemyKills &&
               metrics.BoosterSeconds >= RequiredBoosterSeconds &&
               metrics.CargoValueAstrons >= RequiredCargoValueAstrons &&
               metrics.CargoFull;
    }

    public static AshOverloadReturnMetrics BuildMetrics(PlayerProfileData profile)
    {
        EnsureRoundScope();
        AshOverloadReturnMetrics metrics = new AshOverloadReturnMetrics
        {
            ComputerEnemyKills = computerEnemyKills,
            BoosterSeconds = boosterSeconds
        };

        PlayerInventoryData inventory = profile != null ? profile.Inventory : null;
        if (inventory == null)
            return metrics;

        inventory.Normalize();
        int shipSkinIndex = profile != null ? Mathf.Clamp(profile.ShipSkinIndex, 0, ShipCatalog.MaxShipSkinIndex) : 0;
        int capacity = PlayerProfileService.GetEffectiveShipInventoryCapacity(shipSkinIndex, inventory.EquipmentSlots);
        capacity = Mathf.Clamp(capacity, 0, inventory.ShipSlots.Length);

        metrics.CargoCapacity = capacity;
        metrics.CargoFull = capacity > 0;
        for (int i = 0; i < capacity; i++)
        {
            string itemId = inventory.ShipSlots[i];
            if (string.IsNullOrWhiteSpace(itemId))
            {
                metrics.CargoFull = false;
                continue;
            }

            metrics.FilledCargoSlots++;
            metrics.CargoValueAstrons += InventoryItemCatalog.GetSellValueAstrons(itemId);
        }

        return metrics;
    }

    static void EnsureRoundScope()
    {
        string roundKey = BuildRoundKey();
        if (string.Equals(trackedRoundKey, roundKey, System.StringComparison.Ordinal))
            return;

        trackedRoundKey = roundKey;
        computerEnemyKills = 0;
        boosterSeconds = 0f;
    }

    static string BuildRoundKey()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return "offline";

        string roomName = PhotonNetwork.CurrentRoom.Name ?? string.Empty;
        string startTime = "nostart";
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.StartTimeKey, out object value) && value != null)
            startTime = value.ToString();

        return roomName + "_" + startTime;
    }
}
