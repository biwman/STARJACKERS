using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerRepairDocking : MonoBehaviourPun
{
    const float HoldToLandSeconds = 1f;
    const float LandingDuration = 2f;
    const float LaunchDuration = 2f;
    const float DockedScaleMultiplier = 0.7f;
    const float RepairPerSecond = 10f;
    const float RepairTickSeconds = 0.2f;

    enum DockState
    {
        None,
        Holding,
        Landing,
        Repairing,
        Launching
    }

    DockState state;
    Coroutine routine;
    PlayerMovement movement;
    PlayerShooting shooting;
    Rigidbody2D body;
    Vector3 originalScale;
    Vector3 dockedScale;
    RigidbodyType2D originalBodyType;
    RigidbodyConstraints2D originalConstraints;
    bool originalSimulated = true;
    bool dockingPhysicsLocked;
    float repairAccumulator;
    string activeBayId;

    public bool IsBusy => state != DockState.None;
    public bool IsDamageImmune => state == DockState.Landing || state == DockState.Repairing || state == DockState.Launching;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        shooting = GetComponent<PlayerShooting>();
        body = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        dockedScale = originalScale * DockedScaleMultiplier;
    }

    void OnDisable()
    {
        if (routine != null)
            StopCoroutine(routine);
        ReleaseActiveBayOccupancy();
        RestoreDockingPhysicsLock();
        RestoreControls();
        state = DockState.None;
    }

    void Update()
    {
        if (state == DockState.Repairing)
            MaintainDockedPosition();

        if (!PhotonNetwork.IsMasterClient || state != DockState.Repairing)
            return;

        repairAccumulator += Time.deltaTime;
        while (repairAccumulator >= RepairTickSeconds)
        {
            repairAccumulator -= RepairTickSeconds;
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health == null || health.IsWreck || health.IsEvacuationAnimating)
            {
                photonView.RPC(nameof(ForceEndRepairDocking), RpcTarget.All);
                return;
            }

            health.RepairVitalsAuthority(Mathf.RoundToInt(RepairPerSecond * RepairTickSeconds));
            if (health.HasFullVitals)
            {
                photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
                return;
            }
        }
    }

    public bool TryStartUse(RepairBay bay)
    {
        if (!photonView.IsMine)
            return false;

        if (state == DockState.Repairing)
        {
            photonView.RPC(nameof(RequestRepairLaunch), RpcTarget.MasterClient);
            return true;
        }

        if (bay == null)
            return false;

        if (state != DockState.None)
            return true;

        routine = StartCoroutine(HoldToLandRoutine(bay));
        return true;
    }

    public void StopUseHold()
    {
        if (state != DockState.Holding)
            return;

        if (routine != null)
            StopCoroutine(routine);
        routine = null;
        state = DockState.None;
    }

    IEnumerator HoldToLandRoutine(RepairBay bay)
    {
        state = DockState.Holding;
        float elapsed = 0f;
        while (elapsed < HoldToLandSeconds)
        {
            if (bay == null || Vector2.Distance(transform.position, bay.LandingPoint) > RepairBay.InteractionRadius + 0.45f)
            {
                state = DockState.None;
                routine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        state = DockState.None;
        routine = null;
        photonView.RPC(nameof(RequestRepairDock), RpcTarget.MasterClient, bay.StableId);
    }

    [PunRPC]
    void RequestRepairDock(string bayId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        RepairBay bay = RepairBay.Find(bayId);
        if (health == null || bay == null || health.IsWreck || health.IsEvacuationAnimating || health.IsAstronautControlled)
            return;

        if (Vector2.Distance(transform.position, bay.LandingPoint) > RepairBay.InteractionRadius + 0.85f)
            return;

        if (!TryReserveRepairBayOccupancy(bay.StableId, photonView.Owner.ActorNumber))
            return;

        Vector3 landingPoint = bay.LandingPoint;
        activeBayId = bay.StableId;
        photonView.RPC(nameof(BeginRepairLanding), RpcTarget.All, bayId, landingPoint.x, landingPoint.y);
    }

    [PunRPC]
    void RequestRepairLaunch(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || photonView.Owner == null || info.Sender == null || info.Sender.ActorNumber != photonView.Owner.ActorNumber)
            return;

        if (state == DockState.Repairing || state == DockState.Landing)
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
    }

    [PunRPC]
    void BeginRepairLanding(string bayId, float landingX, float landingY)
    {
        StopActiveRoutine();
        activeBayId = bayId;
        routine = StartCoroutine(LandingRoutine(new Vector3(landingX, landingY, transform.position.z)));
    }

    IEnumerator LandingRoutine(Vector3 landingPoint)
    {
        state = DockState.Landing;
        LockControls();
        AudioManager.Instance.PlayRepairBayLandingAt(landingPoint);

        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < LandingDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / LandingDuration));
            transform.position = Vector3.Lerp(startPosition, landingPoint, t);
            transform.localScale = Vector3.Lerp(startScale, dockedScale, t);
            ZeroVelocity();
            yield return null;
        }

        transform.position = ResolveActiveLandingPoint(landingPoint);
        transform.localScale = dockedScale;
        ZeroVelocity();

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (PhotonNetwork.IsMasterClient && health != null && health.HasFullVitals)
        {
            photonView.RPC(nameof(BeginRepairLaunch), RpcTarget.All);
            yield break;
        }

        state = DockState.Repairing;
        repairAccumulator = 0f;
        routine = null;
    }

    [PunRPC]
    void BeginRepairLaunch()
    {
        StopActiveRoutine();
        routine = StartCoroutine(LaunchRoutine());
    }

    IEnumerator LaunchRoutine()
    {
        state = DockState.Launching;
        LockControls();
        AudioManager.Instance.PlayRepairBayStartingAt(transform.position);

        Vector3 startScale = transform.localScale;
        Vector3 startPosition = transform.position;
        Vector3 launchOffset = transform.up.sqrMagnitude > 0.001f ? transform.up.normalized * 0.75f : Vector3.up * 0.75f;
        Vector3 endPosition = startPosition + launchOffset;
        float elapsed = 0f;
        while (elapsed < LaunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / LaunchDuration));
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);
            ZeroVelocity();
            yield return null;
        }

        transform.localScale = originalScale;
        RestoreDockingPhysicsLock();
        ReleaseActiveBayOccupancy();
        state = DockState.None;
        activeBayId = null;
        routine = null;
        RestoreControls();
    }

    [PunRPC]
    void ForceEndRepairDocking()
    {
        StopActiveRoutine();
        ReleaseActiveBayOccupancy();
        state = DockState.None;
        activeBayId = null;
        transform.localScale = originalScale;
        RestoreDockingPhysicsLock();
        RestoreControls();
    }

    void LockControls()
    {
        ApplyDockingPhysicsLock();

        if (!photonView.IsMine)
            return;

        if (movement != null)
            movement.enabled = false;
        if (shooting != null)
            shooting.enabled = false;
    }

    void RestoreControls()
    {
        if (!photonView.IsMine)
            return;

        PlayerHealth health = GetComponent<PlayerHealth>();
        bool canRestore = health == null || (!health.IsWreck && !health.IsEvacuationAnimating);
        if (!canRestore)
            return;

        if (movement != null)
            movement.enabled = true;
        if (shooting != null)
            shooting.enabled = true;
    }

    void ApplyDockingPhysicsLock()
    {
        if (body == null)
            return;

        if (!dockingPhysicsLocked)
        {
            originalBodyType = body.bodyType;
            originalConstraints = body.constraints;
            originalSimulated = body.simulated;
            dockingPhysicsLocked = true;
        }

        body.simulated = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        ZeroVelocity();
    }

    void RestoreDockingPhysicsLock()
    {
        if (body == null || !dockingPhysicsLocked)
            return;

        body.bodyType = originalBodyType;
        body.constraints = originalConstraints;
        body.simulated = originalSimulated;
        ZeroVelocity();
        dockingPhysicsLocked = false;
    }

    void MaintainDockedPosition()
    {
        if (string.IsNullOrWhiteSpace(activeBayId))
            return;

        transform.position = ResolveActiveLandingPoint(transform.position);
        transform.localScale = dockedScale;
        ZeroVelocity();
    }

    Vector3 ResolveActiveLandingPoint(Vector3 fallback)
    {
        RepairBay bay = RepairBay.Find(activeBayId);
        if (bay == null)
            return fallback;

        Vector3 landingPoint = bay.LandingPoint;
        return new Vector3(landingPoint.x, landingPoint.y, fallback.z);
    }

    void StopActiveRoutine()
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = null;
    }

    void ReleaseActiveBayOccupancy()
    {
        if (!PhotonNetwork.IsMasterClient || string.IsNullOrWhiteSpace(activeBayId))
            return;

        int actorNumber = photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : -1;
        ReleaseRepairBayOccupancy(activeBayId, actorNumber);
    }

    bool TryReserveRepairBayOccupancy(string bayId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(bayId) || actorNumber <= 0)
            return false;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetRepairBayOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (IsActorOccupyingAnotherBay(occupancy, bayId, actorNumber))
        {
            PublishRepairBayOccupancy(occupancy);
            return false;
        }

        if (occupancy.TryGetValue(bayId, out int occupiedByActor) &&
            occupiedByActor > 0 &&
            occupiedByActor != actorNumber &&
            IsActorInRoom(occupiedByActor))
        {
            PublishRepairBayOccupancy(occupancy);
            return false;
        }

        occupancy[bayId] = actorNumber;
        PublishRepairBayOccupancy(occupancy);
        return true;
    }

    void ReleaseRepairBayOccupancy(string bayId, int actorNumber)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || string.IsNullOrWhiteSpace(bayId))
            return;

        Dictionary<string, int> occupancy = DeserializeRepairBayOccupancy(GetRepairBayOccupancyStateRaw());
        RemoveStaleOccupants(occupancy);

        if (occupancy.TryGetValue(bayId, out int occupiedByActor) &&
            (actorNumber <= 0 || occupiedByActor == actorNumber || !IsActorInRoom(occupiedByActor)))
        {
            occupancy.Remove(bayId);
            PublishRepairBayOccupancy(occupancy);
        }
        else
        {
            PublishRepairBayOccupancy(occupancy);
        }
    }

    static string GetRepairBayOccupancyStateRaw()
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomSettings.RepairBayOccupancyStateKey, out object value) &&
            value is string raw)
        {
            return raw;
        }

        return string.Empty;
    }

    static Dictionary<string, int> DeserializeRepairBayOccupancy(string raw)
    {
        Dictionary<string, int> occupancy = new Dictionary<string, int>(System.StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
            return occupancy;

        string[] entries = raw.Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            int separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= entry.Length - 1)
                continue;

            string bayId = entry.Substring(0, separatorIndex);
            string actorRaw = entry.Substring(separatorIndex + 1);
            if (string.IsNullOrWhiteSpace(bayId) || !int.TryParse(actorRaw, out int actorNumber) || actorNumber <= 0)
                continue;

            occupancy[bayId] = actorNumber;
        }

        return occupancy;
    }

    static void PublishRepairBayOccupancy(Dictionary<string, int> occupancy)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            [RoomSettings.RepairBayOccupancyStateKey] = SerializeRepairBayOccupancy(occupancy)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    static string SerializeRepairBayOccupancy(Dictionary<string, int> occupancy)
    {
        if (occupancy == null || occupancy.Count == 0)
            return string.Empty;

        List<string> bayIds = new List<string>(occupancy.Keys);
        bayIds.Sort(System.StringComparer.Ordinal);

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < bayIds.Count; i++)
        {
            string bayId = bayIds[i];
            if (string.IsNullOrWhiteSpace(bayId) || !occupancy.TryGetValue(bayId, out int actorNumber) || actorNumber <= 0)
                continue;

            if (builder.Length > 0)
                builder.Append(';');

            builder.Append(bayId);
            builder.Append('=');
            builder.Append(actorNumber);
        }

        return builder.ToString();
    }

    static void RemoveStaleOccupants(Dictionary<string, int> occupancy)
    {
        if (occupancy == null || occupancy.Count == 0)
            return;

        List<string> staleBayIds = null;
        foreach (KeyValuePair<string, int> pair in occupancy)
        {
            if (IsActorInRoom(pair.Value))
                continue;

            if (staleBayIds == null)
                staleBayIds = new List<string>();

            staleBayIds.Add(pair.Key);
        }

        if (staleBayIds == null)
            return;

        for (int i = 0; i < staleBayIds.Count; i++)
            occupancy.Remove(staleBayIds[i]);
    }

    static bool IsActorOccupyingAnotherBay(Dictionary<string, int> occupancy, string requestedBayId, int actorNumber)
    {
        if (occupancy == null || actorNumber <= 0)
            return false;

        foreach (KeyValuePair<string, int> pair in occupancy)
        {
            if (pair.Value != actorNumber)
                continue;

            if (!string.Equals(pair.Key, requestedBayId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static bool IsActorInRoom(int actorNumber)
    {
        if (actorNumber <= 0)
            return false;

        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].ActorNumber == actorNumber)
                return true;
        }

        return false;
    }

    void ZeroVelocity()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    float EaseInOut(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }
}
