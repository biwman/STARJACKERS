using Photon.Pun;
using UnityEngine;

public sealed class PreservedAlphaSpecimenCompanionController : MonoBehaviourPun
{
    const float FollowRightOffset = -0.92f;
    const float FollowForwardOffset = 0.48f;
    const float SideBobAmplitude = 0.055f;
    const float ForwardBobAmplitude = 0.08f;
    const float VisualTargetWorldSize = GameVisualTheme.PlayerTargetSize * 0.17f;
    const float InventoryRefreshInterval = 0.5f;
    const string MantaFrameResourcePath = "Enemies/SpaceManta";
    const string MantaSpriteResourcePath = "Enemies/SpaceManta/space_manta_flap_00";
    const string MantaEditorAssetPath = "Assets/Resources/Enemies/SpaceManta/space_manta_flap_00.png";

    PlayerHealth health;
    SpriteRenderer visualRenderer;
    EnemySpriteFrameAnimator frameAnimator;
    bool visualActive;
    bool cachedCarried;
    float nextInventoryRefreshTime;
    float animationPhase;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        animationPhase = photonView != null ? photonView.ViewID * 0.173f : Random.Range(0f, 10f);
        EnsureVisual();
    }

    void Update()
    {
        if (!CanCompanionRun())
        {
            SetVisualActive(false);
            return;
        }

        bool carried = IsPreservedAlphaSpecimenCarried();
        SetVisualActive(carried);
        if (!carried)
            return;

        UpdateVisualTransform();
    }

    public void DeactivateForShipLoss()
    {
        SetVisualActive(false);
        enabled = false;
    }

    bool CanCompanionRun()
    {
        if (health == null)
            health = GetComponent<PlayerHealth>();

        return health != null &&
               health.isActiveAndEnabled &&
               health.IsHumanShipControlled &&
               !health.IsWreck &&
               !health.IsEvacuationAnimating;
    }

    bool IsPreservedAlphaSpecimenCarried()
    {
        if (Time.unscaledTime < nextInventoryRefreshTime)
            return cachedCarried;

        nextInventoryRefreshTime = Time.unscaledTime + InventoryRefreshInterval;
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : PhotonNetwork.LocalPlayer;
        cachedCarried = PlayerProfileService.PlayerHasPreservedAlphaSpecimenInSafePocket(owner);
        return cachedCarried;
    }

    void EnsureVisual()
    {
        if (visualRenderer != null)
            return;

        GameObject visual = new GameObject("PreservedAlphaSpecimenMantaVisual");
        visual.transform.SetParent(transform, false);
        visualRenderer = visual.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = RuntimeSpriteUtility.LoadSprite(MantaSpriteResourcePath, MantaEditorAssetPath);
        visualRenderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        visualRenderer.sortingOrder = 50;
        visualRenderer.color = new Color(0.82f, 1f, 1f, 0.96f);
        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);

        frameAnimator = visual.AddComponent<EnemySpriteFrameAnimator>();
        frameAnimator.Configure(visualRenderer, MantaFrameResourcePath, 7.8f);
        frameAnimator.SetSpeedMultiplier(0.82f);
        visualActive = true;
    }

    void SetVisualActive(bool active)
    {
        EnsureVisual();
        if (visualRenderer != null)
            visualRenderer.enabled = active;

        visualActive = active;
    }

    void UpdateVisualTransform()
    {
        if (visualRenderer == null || !visualActive)
            return;

        float time = Time.time + animationPhase;
        float sideOffset = FollowRightOffset + Mathf.Sin(time * 1.75f) * SideBobAmplitude;
        float forwardOffset = FollowForwardOffset + Mathf.Sin(time * 2.35f) * ForwardBobAmplitude;
        visualRenderer.transform.position = transform.position + transform.right * sideOffset + transform.up * forwardOffset;

        Vector2 lookDirection = ((Vector2)transform.up + (Vector2)transform.right * (Mathf.Sin(time * 1.2f) * 0.22f)).normalized;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg - 90f;
            visualRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        RuntimeSpriteUtility.FitRendererWorldSize(visualRenderer, VisualTargetWorldSize);
    }
}
