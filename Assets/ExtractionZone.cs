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
    const float RequestedPlayerEvacuationGraceDistance = 1.65f;
    const float CharlieSmartExtractionBonusWindow = 30f;

    bool isActive;
    bool isBeingUsed;
    bool isTransitioning;
    bool isEvacuating;
    bool messageShowing;

    SpriteRenderer sr;
    Coroutine blinkRoutine;
    Coroutine hideMessageRoutine;
    GameObject cachedMessageObject;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            sr.sortingOrder = GameVisualTheme.ExtractionZoneSortingOrder;
        }
        SetColor(Color.red);
        cachedMessageObject = FindExtractionMessage();
        if (cachedMessageObject != null)
            cachedMessageObject.SetActive(false);

        GameVisualTheme.RequestRuntimeRefresh();
    }

    void LateUpdate()
    {
        if (cachedMessageObject == null)
            cachedMessageObject = FindExtractionMessage();

        if (cachedMessageObject != null && cachedMessageObject.activeSelf != messageShowing)
            cachedMessageObject.SetActive(messageShowing);
    }

    void SetColor(Color color)
    {
        if (sr != null)
            sr.color = color;
    }

    public void TryUse(PhotonView playerView)
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;

        if (!isBeingUsed)
            StartCoroutine(UseRoutine(playerView));
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

    [PunRPC]
    void ActivateZone()
    {
        isActive = true;
        isTransitioning = false;
        isEvacuating = false;
        StopEvacBuzzerLoop();

        if (blinkRoutine != null)
            StopCoroutine(blinkRoutine);

        blinkRoutine = StartCoroutine(BlinkActive());
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

        photonView.RPC(nameof(ResetZone), RpcTarget.All);

        if (processedPlayers.Count <= 0)
            return;

        bool anyPlayerRemaining = false;
        for (int i = 0; i < playersBeforeEvacuation.Length; i++)
        {
            PlayerHealth player = playersBeforeEvacuation[i];
            if (player == null || player.IsWreck || player.photonView == null || player.IsBotControlled || player.IsEvacuationAnimating)
                continue;

            if (!processedPlayers.Contains(player.photonView.ViewID))
            {
                anyPlayerRemaining = true;
                break;
            }
        }

        if (anyPlayerRemaining)
            return;

        GameTimer timer = FindAnyObjectByType<GameTimer>();
        if (timer != null)
            GameTimer.SetExtractionPause(timer.GetCurrentRemainingTime(), evacuationAnimationDuration);

        GameManager manager = FindAnyObjectByType<GameManager>();
        if (manager != null)
            StartCoroutine(EndGameAfterEvacuationAnimation(manager));
    }

    IEnumerator EndGameAfterEvacuationAnimation(GameManager manager)
    {
        yield return new WaitForSeconds(evacuationAnimationDuration);

        if (manager != null)
            manager.EndGame("evacuation");
    }

    void TryEvacuateRequestedPlayer(PhotonView requestedPlayerView, HashSet<int> processedPlayers)
    {
        if (requestedPlayerView == null || processedPlayers == null)
            return;

        PlayerHealth requestedPlayer = requestedPlayerView.GetComponent<PlayerHealth>();
        if (requestedPlayer == null || !IsPlayerCloseEnoughForRequestedEvacuation(requestedPlayer))
            return;

        TryEvacuatePlayer(requestedPlayer, processedPlayers);
    }

    bool TryEvacuatePlayer(PlayerHealth playerHealth, HashSet<int> processedPlayers)
    {
        if (playerHealth == null || playerHealth.IsWreck || playerHealth.IsBotControlled || playerHealth.IsEvacuationAnimating)
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

        playerView.RPC(nameof(PlayerHealth.OnEvacuated), playerView.Owner, 0);
        playerView.RPC(nameof(PlayerHealth.NotifyFinalEvacuation), playerView.Owner, finalScore, outcome);
        playerView.RPC(nameof(PlayerHealth.BeginEvacuationSequence), RpcTarget.All);
        return true;
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

        Collider2D zoneCollider = GetComponent<Collider2D>();
        Collider2D[] playerColliders = playerHealth.GetComponentsInChildren<Collider2D>();
        if (zoneCollider != null && playerColliders != null && playerColliders.Length > 0)
        {
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled)
                    continue;

                ColliderDistance2D distance = zoneCollider.Distance(playerCollider);
                if (distance.isOverlapped || distance.distance <= RequestedPlayerEvacuationGraceDistance)
                    return true;
            }
        }

        return Vector2.Distance(transform.position, playerHealth.transform.position) <= RequestedPlayerEvacuationGraceDistance + 1.4f;
    }

    Collider2D[] GetPlayersInsideZone()
    {
        Collider2D zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider == null)
            return Physics2D.OverlapCircleAll(transform.position, 1.0f);

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = false,
            useTriggers = true
        };

        Collider2D[] buffer = new Collider2D[32];
        int count = zoneCollider.Overlap(filter, buffer);
        if (count <= 0)
            return System.Array.Empty<Collider2D>();

        Collider2D[] hits = new Collider2D[count];
        System.Array.Copy(buffer, hits, count);
        return hits;
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
            StopCoroutine(blinkRoutine);
        if (hideMessageRoutine != null)
        {
            StopCoroutine(hideMessageRoutine);
            hideMessageRoutine = null;
        }

        SetColor(Color.red);
    }

    [PunRPC]
    void ShowExtractionMessage()
    {
        if (messageShowing)
            return;

        GameObject obj = cachedMessageObject != null ? cachedMessageObject : FindExtractionMessage();
        if (obj == null)
            return;

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
        GameObject[] allTexts = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allTexts.Length; i++)
        {
            if (allTexts[i].name == "ExtractionMessage")
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
            StopCoroutine(blinkRoutine);

        blinkRoutine = StartCoroutine(BlinkTransition());
        AudioManager.Instance.PlayEvacBuzzerLoopForDuration(duration);
        ShowExtractionMessage();
    }

    void StopEvacBuzzerLoop()
    {
        AudioManager.Instance.StopEvacBuzzerLoop();
    }
}
