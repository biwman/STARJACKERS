using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class ExtractionZone : MonoBehaviourPun
{
    public float activationTime = 3f;
    public float transitionDuration = 10f;
    public float activeDuration = 15f;
    public float evacuationAnimationDuration = 4f;
    public const float PlayerInteractionGraceDistance = 0.45f;
    const float CharlieSmartExtractionBonusWindow = 30f;
    const float EvacuationEndScreenDelayBuffer = 0.35f;
    const float PortalInteractionRadiusFactor = 0.82f;
    const float ExtractionMessageSearchRetryInterval = 1f;
    const string ExtractionMessageName = "ExtractionMessage";

    static readonly Vector2[] CarrierInteractionShape =
    {
        new Vector2(0.04f, 0.47f),
        new Vector2(0.15f, 0.60f),
        new Vector2(0.43f, 0.68f),
        new Vector2(0.55f, 0.80f),
        new Vector2(0.67f, 0.78f),
        new Vector2(0.77f, 0.65f),
        new Vector2(0.94f, 0.56f),
        new Vector2(0.98f, 0.43f),
        new Vector2(0.93f, 0.28f),
        new Vector2(0.79f, 0.18f),
        new Vector2(0.61f, 0.15f),
        new Vector2(0.48f, 0.06f),
        new Vector2(0.32f, 0.16f),
        new Vector2(0.11f, 0.32f)
    };

    static readonly Vector2[] SpaceCityInteractionShape =
    {
        new Vector2(0.03f, 0.35f),
        new Vector2(0.10f, 0.25f),
        new Vector2(0.22f, 0.17f),
        new Vector2(0.36f, 0.10f),
        new Vector2(0.51f, 0.08f),
        new Vector2(0.64f, 0.14f),
        new Vector2(0.75f, 0.19f),
        new Vector2(0.88f, 0.22f),
        new Vector2(0.97f, 0.32f),
        new Vector2(0.98f, 0.47f),
        new Vector2(0.92f, 0.58f),
        new Vector2(0.78f, 0.65f),
        new Vector2(0.63f, 0.82f),
        new Vector2(0.47f, 0.80f),
        new Vector2(0.34f, 0.66f),
        new Vector2(0.18f, 0.57f),
        new Vector2(0.07f, 0.49f)
    };

    bool isActive;
    bool isBeingUsed;
    bool isTransitioning;
    bool isEvacuating;
    bool messageShowing;

    public bool IsActive => isActive;
    public bool IsTransitioning => isTransitioning;
    public bool IsEvacuating => isEvacuating;

    SpriteRenderer sr;
    ExtractionPortalVisual portalVisual;
    ExtractionCarrierVisual carrierVisual;
    ExtractionSpaceCityVisual spaceCityVisual;
    Coroutine blinkRoutine;
    Coroutine hideMessageRoutine;
    GameObject cachedMessageObject;
    float nextMessageSearchTime;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            sr.sortingOrder = GameVisualTheme.ExtractionZoneSortingOrder;
        }
        SetColor(Color.white);
        RefreshExtractionVisual();
        RefreshInteractionCollider();
        cachedMessageObject = FindExtractionMessage();
        if (cachedMessageObject != null)
            cachedMessageObject.SetActive(false);

        GameVisualTheme.RequestRuntimeRefresh();
    }

    void LateUpdate()
    {
        if (cachedMessageObject == null && Time.unscaledTime >= nextMessageSearchTime)
        {
            nextMessageSearchTime = Time.unscaledTime + ExtractionMessageSearchRetryInterval;
            cachedMessageObject = FindExtractionMessage();
        }

        if (cachedMessageObject != null && cachedMessageObject.activeSelf != messageShowing)
            cachedMessageObject.SetActive(messageShowing);
    }

    void SetColor(Color color)
    {
        if (sr != null)
            sr.color = Color.white;
    }

    void EnsurePortalVisual()
    {
        if (portalVisual == null)
            portalVisual = GetComponent<ExtractionPortalVisual>();

        if (portalVisual == null)
            portalVisual = gameObject.AddComponent<ExtractionPortalVisual>();

        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        portalVisual.Initialize(sr);
    }

    void EnsureCarrierVisual()
    {
        if (carrierVisual == null)
            carrierVisual = GetComponent<ExtractionCarrierVisual>();

        if (carrierVisual == null)
            carrierVisual = gameObject.AddComponent<ExtractionCarrierVisual>();

        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        carrierVisual.Initialize(sr);
    }

    void EnsureSpaceCityVisual()
    {
        if (spaceCityVisual == null)
            spaceCityVisual = GetComponent<ExtractionSpaceCityVisual>();

        if (spaceCityVisual == null)
            spaceCityVisual = gameObject.AddComponent<ExtractionSpaceCityVisual>();

        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        spaceCityVisual.Initialize(sr);
    }

    void EnsureExtractionVisual()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (IsCarrierExtraction())
        {
            if (portalVisual != null)
                portalVisual.SetVisible(false);
            if (spaceCityVisual != null)
                spaceCityVisual.SetVisible(false);

            EnsureCarrierVisual();
            carrierVisual.SetVisible(true);
            return;
        }

        if (IsSpaceCityExtraction())
        {
            if (portalVisual != null)
                portalVisual.SetVisible(false);
            if (carrierVisual != null)
                carrierVisual.SetVisible(false);

            EnsureSpaceCityVisual();
            spaceCityVisual.SetVisible(true);
            return;
        }

        if (carrierVisual != null)
            carrierVisual.SetVisible(false);
        if (spaceCityVisual != null)
            spaceCityVisual.SetVisible(false);

        EnsurePortalVisual();
        portalVisual.SetVisible(true);
    }

    public void RefreshPortalVisual()
    {
        RefreshExtractionVisual();
    }

    public void RefreshExtractionVisual()
    {
        EnsureExtractionVisual();

        if (IsCarrierExtraction())
        {
            if (isActive)
                carrierVisual.SetActive();
            else if (isTransitioning)
                carrierVisual.SetTransitioning();
            else
                carrierVisual.SetInactive();

            return;
        }

        if (IsSpaceCityExtraction())
        {
            if (isActive)
                spaceCityVisual.SetActive();
            else if (isTransitioning)
                spaceCityVisual.SetTransitioning();
            else
                spaceCityVisual.SetInactive();

            return;
        }

        if (isActive)
            portalVisual.SetActive();
        else if (isTransitioning)
            portalVisual.SetTransitioning();
        else
            portalVisual.SetInactive();
    }

    public void RefreshInteractionCollider()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (sr == null || sr.sprite == null)
            return;

        if (IsCarrierExtraction())
        {
            ConfigurePolygonInteractionCollider(CarrierInteractionShape);
            return;
        }

        if (IsSpaceCityExtraction())
        {
            ConfigurePolygonInteractionCollider(SpaceCityInteractionShape);
            return;
        }

        ConfigurePortalInteractionCollider();
    }

    public float GetInteractionDistanceToPoint(Vector2 worldPoint)
    {
        Collider2D[] zoneColliders = GetEnabledInteractionColliders();
        float bestDistance = float.MaxValue;

        for (int i = 0; i < zoneColliders.Length; i++)
        {
            Collider2D zoneCollider = zoneColliders[i];
            if (zoneCollider == null)
                continue;

            if (zoneCollider.OverlapPoint(worldPoint))
                return 0f;

            Vector2 closestPoint = zoneCollider.ClosestPoint(worldPoint);
            bestDistance = Mathf.Min(bestDistance, Vector2.Distance(worldPoint, closestPoint));
        }

        return bestDistance < float.MaxValue
            ? bestDistance
            : Vector2.Distance(transform.position, worldPoint);
    }

    public bool CanPlayerRequestEvacuation(PlayerHealth playerHealth)
    {
        return IsPlayerCloseEnoughForRequestedEvacuation(playerHealth);
    }

    public bool TryUse(PhotonView playerView)
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return false;

        if (playerView == null || isBeingUsed)
            return false;

        PlayerHealth playerHealth = playerView.GetComponent<PlayerHealth>();
        if (!GameTimer.IsActiveRoundPlayer(playerHealth) || !CanPlayerRequestEvacuation(playerHealth))
            return false;

        StartCoroutine(UseRoutine(playerView));
        return true;
    }

    public bool TryUseNeutralRider(PhotonView riderView)
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return false;

        if (riderView == null || isBeingUsed || isTransitioning || isEvacuating)
            return false;

        if (!NeutralRiderController.IsNeutralRider(riderView) &&
            !NeutralRiderController.IsNeutralRiderInstantiationData(riderView.InstantiationData))
        {
            return false;
        }

        PlayerHealth riderHealth = riderView.GetComponent<PlayerHealth>();
        if (riderHealth == null || riderHealth.IsWreck || riderHealth.IsEvacuationAnimating)
            return false;

        if (!CanPlayerRequestEvacuation(riderHealth))
            return false;

        StartCoroutine(NeutralRiderUseRoutine());
        return true;
    }

    IEnumerator UseRoutine(PhotonView playerView)
    {
        isBeingUsed = true;

        if (!isActive)
        {
            if (!isTransitioning)
            {
                isTransitioning = true;
                photonView.RPC(nameof(BeginTransitionStage), RpcTarget.All, transitionDuration);
                yield return new WaitForSeconds(transitionDuration);

                isTransitioning = false;
                if (!isActive)
                {
                    isActive = true;
                    photonView.RPC(nameof(ActivateZone), RpcTarget.All);
                }
            }
        }
        else
        {
            EvacuatePlayers(playerView);
        }

        isBeingUsed = false;
    }

    IEnumerator NeutralRiderUseRoutine()
    {
        isBeingUsed = true;

        if (!isActive && !isTransitioning)
        {
            isTransitioning = true;
            photonView.RPC(nameof(BeginTransitionStage), RpcTarget.All, transitionDuration);
            yield return new WaitForSeconds(transitionDuration);

            isTransitioning = false;
            if (!isActive)
            {
                isActive = true;
                photonView.RPC(nameof(ActivateZone), RpcTarget.All);
            }
        }

        isBeingUsed = false;
    }

    [PunRPC]
    void ActivateZone()
    {
        isActive = true;
        isTransitioning = false;
        isEvacuating = false;
        StopEvacBuzzerLoop();

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        RefreshExtractionVisual();
        StartCoroutine(ActiveTimer());
    }

    IEnumerator ActiveTimer()
    {
        float timer = 0f;

        while (timer < activeDuration)
        {
            if (!isActive)
                yield break;

            timer += Time.deltaTime;
            yield return null;
        }

        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
            EvacuatePlayers();
    }

    IEnumerator BlinkActive()
    {
        while (isActive)
        {
            SetColor(Color.green);
            yield return new WaitForSeconds(0.3f);
            SetColor(Color.white);
            yield return new WaitForSeconds(0.3f);
        }
    }

    IEnumerator BlinkTransition()
    {
        Color dimYellow = new Color(0.55f, 0.45f, 0.04f, 1f);
        while (isTransitioning && !isActive)
        {
            SetColor(Color.yellow);
            yield return new WaitForSeconds(0.28f);

            if (!isTransitioning || isActive)
                yield break;

            SetColor(dimYellow);
            yield return new WaitForSeconds(0.28f);
        }
    }

    void EvacuatePlayers(PhotonView requestedPlayerView = null)
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;

        if (isEvacuating)
            return;

        isEvacuating = true;

        PlayerHealth[] playersBeforeEvacuation = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        Collider2D[] hits = GetPlayersInsideZone();
        HashSet<int> processedPlayers = new HashSet<int>();

        TryEvacuateRequestedPlayer(requestedPlayerView, processedPlayers);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth playerHealth = hits[i].GetComponentInParent<PlayerHealth>();
            TryEvacuatePlayer(playerHealth, processedPlayers);
        }

        if (processedPlayers.Count <= 0)
        {
            isEvacuating = false;
            if (requestedPlayerView == null)
                photonView.RPC(nameof(ResetZone), RpcTarget.All);
            return;
        }

        photonView.RPC(nameof(BeginEvacuationState), RpcTarget.All);

        bool anyPlayerRemaining = false;
        for (int i = 0; i < playersBeforeEvacuation.Length; i++)
        {
            PlayerHealth player = playersBeforeEvacuation[i];
            if (!GameTimer.IsActiveRoundPlayer(player))
                continue;

            if (!processedPlayers.Contains(player.photonView.ViewID))
            {
                anyPlayerRemaining = true;
                break;
            }
        }

        float endGameDelay = GetEvacuationEndGameDelay();

        if (anyPlayerRemaining)
        {
            StartCoroutine(ResetZoneAfterEvacuationAnimation(endGameDelay));
            return;
        }

        GameTimer timer = FindAnyObjectByType<GameTimer>();
        if (timer != null)
            GameTimer.SetExtractionPause(timer.GetCurrentRemainingTime(), endGameDelay);

        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager != null)
            StartCoroutine(EndGameAfterEvacuationAnimation(manager, endGameDelay));
    }

    IEnumerator EndGameAfterEvacuationAnimation(GameManager manager, float delaySeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));

        if (manager != null)
            manager.EndGame("evacuation");
    }

    IEnumerator ResetZoneAfterEvacuationAnimation(float delaySeconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));
        photonView.RPC(nameof(ResetZone), RpcTarget.All);
    }

    float GetEvacuationEndGameDelay()
    {
        float animationDuration = Mathf.Max(evacuationAnimationDuration, PlayerHealth.EvacuationAnimationDurationSeconds);
        return animationDuration + EvacuationEndScreenDelayBuffer;
    }

    void TryEvacuateRequestedPlayer(PhotonView requestedPlayerView, HashSet<int> processedPlayers)
    {
        if (requestedPlayerView == null || processedPlayers == null)
            return;

        PlayerHealth requestedPlayer = requestedPlayerView.GetComponent<PlayerHealth>();
        if (requestedPlayer == null || !CanPlayerRequestEvacuation(requestedPlayer))
            return;

        TryEvacuatePlayer(requestedPlayer, processedPlayers);
    }

    bool TryEvacuatePlayer(PlayerHealth playerHealth, HashSet<int> processedPlayers)
    {
        if (!GameTimer.IsActiveRoundPlayer(playerHealth))
            return false;

        if (!CanPlayerRequestEvacuation(playerHealth))
            return false;

        PhotonView playerView = playerHealth.photonView;
        if (playerView == null || processedPlayers == null || processedPlayers.Contains(playerView.ViewID))
            return false;

        processedPlayers.Add(playerView.ViewID);

        int finalScore = RoundResultsTracker.GetKnownScore(playerView.Owner, playerView.gameObject);
        string outcome = playerHealth.IsAstronautControlled ? "evacuated" : "extracted";
        finalScore = RoundResultsTracker.RecordOutcome(playerView.Owner, finalScore, outcome);
        if (ShouldAwardCharlieLastSecondExtractionBonus(playerHealth, outcome))
            playerView.RPC(nameof(PlayerHealth.AwardCharlieLastSecondExtractionBonus), playerView.Owner);

        if (string.Equals(outcome, "extracted", System.StringComparison.OrdinalIgnoreCase) &&
            ViperRecoveryPlotController.TryEvacuateWreckWithZone(this))
        {
            playerView.RPC(nameof(PlayerHealth.NotifyViperWreckRecovered), playerView.Owner);
        }

        if (string.Equals(outcome, "extracted", System.StringComparison.OrdinalIgnoreCase) &&
            BisonIndustrialPlotController.TryEvacuateIndustrialPartsWithPlayer(this, playerHealth))
        {
            playerView.RPC(nameof(PlayerHealth.NotifyBisonIndustrialPartsDelivered), playerView.Owner);
        }

        if (string.Equals(outcome, "extracted", System.StringComparison.OrdinalIgnoreCase) &&
            InvaderInvasionPlotController.TryCompleteStageOnEvacuation(playerHealth, out int invaderStage))
        {
            playerView.RPC(nameof(PlayerHealth.NotifyInvaderImprintRecovered), playerView.Owner, invaderStage);
        }

        playerView.RPC(nameof(PlayerHealth.OnEvacuated), playerView.Owner, 0);
        playerView.RPC(nameof(PlayerHealth.NotifyFinalEvacuation), playerView.Owner, finalScore, outcome);
        Vector2 evacuationTarget = GetEvacuationTargetWorldPosition();
        playerView.RPC(nameof(PlayerHealth.BeginEvacuationSequence), RpcTarget.All, evacuationTarget.x, evacuationTarget.y);
        return true;
    }

    public Vector2 GetEvacuationTargetWorldPosition()
    {
        if (IsCarrierExtraction())
        {
            EnsureCarrierVisual();
            return carrierVisual != null ? carrierVisual.GetEvacuationTargetWorldPosition() : transform.position;
        }

        if (IsSpaceCityExtraction())
        {
            EnsureSpaceCityVisual();
            return spaceCityVisual != null ? spaceCityVisual.GetEvacuationTargetWorldPosition() : transform.position;
        }

        EnsurePortalVisual();
        return portalVisual != null ? portalVisual.GetEvacuationTargetWorldPosition() : transform.position;
    }

    bool IsCarrierExtraction()
    {
        return string.Equals(RoomSettings.GetExtractionType(), RoomSettings.ExtractionTypeCarrier, System.StringComparison.Ordinal);
    }

    bool IsSpaceCityExtraction()
    {
        return string.Equals(RoomSettings.GetExtractionType(), RoomSettings.ExtractionTypeSpaceCity, System.StringComparison.Ordinal);
    }

    bool ShouldAwardCharlieLastSecondExtractionBonus(PlayerHealth playerHealth, string outcome)
    {
        if (!string.Equals(outcome, "extracted", System.StringComparison.OrdinalIgnoreCase))
            return false;

        PhotonView playerView = playerHealth != null ? playerHealth.photonView : null;
        if (playerView == null || !PilotCatalog.IsSelectedPilot(playerView.Owner, PilotCatalog.CharlieSmartId))
            return false;

        GameTimer timer = FindAnyObjectByType<GameTimer>();
        if (timer == null)
            return false;

        return timer.GetCurrentRemainingTime() <= CharlieSmartExtractionBonusWindow;
    }

    bool IsPlayerCloseEnoughForRequestedEvacuation(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return false;

        Collider2D[] zoneColliders = GetEnabledInteractionColliders();
        Collider2D[] playerColliders = playerHealth.GetComponentsInChildren<Collider2D>();
        if (zoneColliders.Length > 0 && playerColliders != null && playerColliders.Length > 0)
        {
            if (IsAnyPlayerColliderCloseEnough(zoneColliders, playerColliders, false, out bool foundBodyCollider))
                return true;

            if (!foundBodyCollider && IsAnyPlayerColliderCloseEnough(zoneColliders, playerColliders, true, out _))
                return true;
        }

        return GetInteractionDistanceToPoint(playerHealth.transform.position) <= PlayerInteractionGraceDistance;
    }

    bool IsAnyPlayerColliderCloseEnough(Collider2D[] zoneColliders, Collider2D[] playerColliders, bool allowTriggers, out bool foundCandidate)
    {
        foundCandidate = false;

        for (int z = 0; z < zoneColliders.Length; z++)
        {
            Collider2D zoneCollider = zoneColliders[z];
            if (zoneCollider == null)
                continue;

            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled)
                    continue;

                if (!allowTriggers && playerCollider.isTrigger)
                    continue;

                foundCandidate = true;
                ColliderDistance2D distance = zoneCollider.Distance(playerCollider);
                if (distance.isOverlapped || distance.distance <= PlayerInteractionGraceDistance)
                    return true;
            }
        }

        return false;
    }

    Collider2D[] GetPlayersInsideZone()
    {
        Collider2D[] zoneColliders = GetEnabledInteractionColliders();
        if (zoneColliders.Length == 0)
            return Physics2D.OverlapCircleAll(transform.position, 1.0f);

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        List<Collider2D> hits = new List<Collider2D>();
        Collider2D[] buffer = new Collider2D[32];
        for (int z = 0; z < zoneColliders.Length; z++)
        {
            Collider2D zoneCollider = zoneColliders[z];
            if (zoneCollider == null)
                continue;

            int count = zoneCollider.Overlap(filter, buffer);
            for (int i = 0; i < count; i++)
            {
                if (buffer[i] != null)
                    hits.Add(buffer[i]);
            }
        }

        if (hits.Count <= 0)
            return System.Array.Empty<Collider2D>();

        return hits.ToArray();
    }

    void ConfigurePortalInteractionCollider()
    {
        Bounds bounds = sr.sprite.bounds;
        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
            circle = gameObject.AddComponent<CircleCollider2D>();

        circle.enabled = true;
        circle.isTrigger = true;
        circle.offset = bounds.center;
        circle.radius = Mathf.Max(0.1f, Mathf.Min(bounds.extents.x, bounds.extents.y) * PortalInteractionRadiusFactor);
        DisableOtherInteractionColliders(circle);
    }

    void ConfigurePolygonInteractionCollider(Vector2[] normalizedShape)
    {
        if (normalizedShape == null || normalizedShape.Length < 3)
            return;

        PolygonCollider2D polygon = GetComponent<PolygonCollider2D>();
        if (polygon == null)
            polygon = gameObject.AddComponent<PolygonCollider2D>();

        Bounds bounds = sr.sprite.bounds;
        polygon.enabled = true;
        polygon.isTrigger = true;
        polygon.pathCount = 1;
        polygon.SetPath(0, BuildLocalPath(bounds, normalizedShape));
        DisableOtherInteractionColliders(polygon);
    }

    Vector2[] BuildLocalPath(Bounds bounds, Vector2[] normalizedShape)
    {
        Vector2[] path = new Vector2[normalizedShape.Length];
        for (int i = 0; i < normalizedShape.Length; i++)
        {
            Vector2 point = normalizedShape[i];
            path[i] = new Vector2(
                bounds.min.x + bounds.size.x * point.x,
                bounds.min.y + bounds.size.y * point.y);
        }

        return path;
    }

    void DisableOtherInteractionColliders(Collider2D activeCollider)
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider2D = colliders[i];
            if (collider2D == null || collider2D == activeCollider)
                continue;

            collider2D.enabled = false;
        }
    }

    Collider2D[] GetEnabledInteractionColliders()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        List<Collider2D> enabledColliders = new List<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider2D = colliders[i];
            if (collider2D != null && collider2D.enabled)
                enabledColliders.Add(collider2D);
        }

        return enabledColliders.ToArray();
    }

    [PunRPC]
    void BeginEvacuationState()
    {
        isEvacuating = true;
    }

    [PunRPC]
    void ResetZone()
    {
        isActive = false;
        isBeingUsed = false;
        isTransitioning = false;
        isEvacuating = false;
        messageShowing = false;
        StopAlarmLoop();
        StopEvacBuzzerLoop();

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
        if (hideMessageRoutine != null)
        {
            StopCoroutine(hideMessageRoutine);
            hideMessageRoutine = null;
        }

        SetColor(Color.white);
        RefreshExtractionVisual();
    }

    [PunRPC]
    void ShowExtractionMessage()
    {
        if (messageShowing)
            return;

        GameObject obj = cachedMessageObject != null ? cachedMessageObject : FindExtractionMessage();
        if (obj == null)
            return;

        cachedMessageObject = obj;

        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect != null)
            rect.SetAsLastSibling();

        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (text == null)
            text = obj.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.text = "Extraction Zone Activated";
            text.fontStyle = FontStyles.Bold;
        }

        messageShowing = true;
        obj.SetActive(true);
        if (hideMessageRoutine != null)
            StopCoroutine(hideMessageRoutine);
        hideMessageRoutine = StartCoroutine(HideMessage(obj));
    }

    IEnumerator HideMessage(GameObject obj)
    {
        yield return new WaitForSeconds(5f);

        obj.SetActive(false);
        messageShowing = false;
        hideMessageRoutine = null;
    }

    GameObject FindExtractionMessage()
    {
        GameObject activeObject = GameObject.Find(ExtractionMessageName);
        if (activeObject != null && activeObject.scene.IsValid())
            return activeObject;

        GameObject[] allTexts = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            if (allTexts[i].name == ExtractionMessageName)
                return allTexts[i];
        }

        return null;
    }

    [PunRPC]
    void StartAlarmLoop()
    {
        AudioManager.Instance.StartAlarmLoop();
    }

    [PunRPC]
    void StopAlarmLoop()
    {
        AudioManager.Instance.StopAlarmLoop();
    }

    [PunRPC]
    void BeginTransitionStage(float duration)
    {
        isTransitioning = true;
        isActive = false;
        isEvacuating = false;

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        RefreshExtractionVisual();
        AudioManager.Instance.PlayEvacBuzzerLoopForDuration(duration);
        ShowExtractionMessage();
    }

    void StopEvacBuzzerLoop()
    {
        AudioManager.Instance.StopEvacBuzzerLoop();
    }
}
