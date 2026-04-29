using System.Collections;
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
        RestoreControls();
        state = DockState.None;
    }

    void Update()
    {
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

        Vector3 landingPoint = bay.LandingPoint;
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

        transform.position = landingPoint;
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
        state = DockState.None;
        activeBayId = null;
        routine = null;
        RestoreControls();
    }

    [PunRPC]
    void ForceEndRepairDocking()
    {
        StopActiveRoutine();
        state = DockState.None;
        activeBayId = null;
        transform.localScale = originalScale;
        RestoreControls();
    }

    void LockControls()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

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

    void StopActiveRoutine()
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = null;
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
